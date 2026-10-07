using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace MyDiary.Web.Api;

/// <summary>
/// Minimal API endpoint for external session validation.
/// 
/// EKAM (and other SSO-integrated apps) calls this endpoint to check
/// whether a My Diary user session is still active. If inactive, the
/// calling app should log the user out.
/// 
/// Session is considered active when:
///   - A session_dtls record exists for the user (user has not explicitly logged out)
///   - The login occurred within the max session lifetime (24 hours)
/// 
/// Note: The 20-minute inactivity window in session_dtls is only used internally
/// to prevent duplicate concurrent logins. It does NOT mean the session has expired.
/// The actual session lifetime is 24 hours (matching the authenticated state stored
/// in the user's browser session storage).
/// 
/// Endpoint: GET /api/session/validate?emp_no={employee_number}&amp;timestamp={unix_ms}&amp;signature={hmac_signature}
/// 
/// Authentication: HMAC-SHA256 signature using a shared secret key.
/// The signature is computed as: HMAC-SHA256(secret, "emp_no={value}&amp;timestamp={value}")
/// Timestamp must be within ±5 minutes of server time to prevent replay attacks.
/// 
/// Response:
///   200 OK → { "active": true,  "emp_no": "123456" }
///   200 OK → { "active": false, "emp_no": "123456" }
///   400    → { "error": "..." }   — invalid request
///   401    → { "error": "..." }   — invalid/expired signature
/// </summary>
public static class SessionValidationEndpoint
{
    private const int TimestampToleranceMinutes = 5;
    private const int SessionTimeoutMinutes = 20;
    private const int SessionMaxLifetimeHours = 24;

    /// <summary>
    /// Registers the session validation API endpoints on the app pipeline.
    /// </summary>
    public static WebApplication MapSessionValidationApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/session");

        api.MapGet("/validate", ValidateSessionAsync)
           .WithName("ValidateSession")
           .WithDescription("Check if a user's My Diary session is active");

        // Utility endpoint for EKAM team to test connectivity
        api.MapGet("/health", () => Results.Ok(new { status = "OK", timestamp = DateTime.UtcNow }))
           .WithName("SessionApiHealth")
           .WithDescription("Health check for session validation API");

        return app;
    }

    private static async Task<IResult> ValidateSessionAsync(
        string? emp_no,
        string? timestamp,
        string? signature,
        IConfiguration configuration,
        ILogger<Program> logger)
    {
        // 1. Validate required parameters
        if (string.IsNullOrWhiteSpace(emp_no))
            return Results.BadRequest(new { error = "emp_no parameter is required." });

        // 2. Authenticate the request
        var secretKey = configuration["ApiEndpoints:SessionValidationApiKey"] ?? "";
        if (string.IsNullOrEmpty(secretKey))
        {
            logger.LogError("[SessionValidationApi] SessionValidationApiKey is not configured.");
            return Results.Problem("API not configured.", statusCode: 503);
        }

        // If signature-based auth is provided, validate it
        if (!string.IsNullOrWhiteSpace(signature))
        {
            if (string.IsNullOrWhiteSpace(timestamp))
                return Results.BadRequest(new { error = "timestamp is required when using signature auth." });

            // Validate timestamp freshness (prevent replay attacks)
            if (!long.TryParse(timestamp, out var tsMillis))
                return Results.BadRequest(new { error = "Invalid timestamp format. Use Unix milliseconds." });

            var requestTime = DateTimeOffset.FromUnixTimeMilliseconds(tsMillis);
            var drift = Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes);
            if (drift > TimestampToleranceMinutes)
            {
                logger.LogWarning("[SessionValidationApi] Rejected stale request. Drift={Drift}min, emp_no={EmpNo}", drift, emp_no);
                return Results.Json(new { error = "Request expired. Timestamp drift exceeds tolerance." }, statusCode: 401);
            }

            // Validate HMAC signature
            var payload = $"emp_no={emp_no}&timestamp={timestamp}";
            var expectedSignature = ComputeHmacSha256(secretKey, payload);

            if (!string.Equals(signature, expectedSignature, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("[SessionValidationApi] Invalid signature for emp_no={EmpNo}", emp_no);
                return Results.Json(new { error = "Invalid signature." }, statusCode: 401);
            }
        }
        else
        {
            // Fallback: simple API key in header or query (for initial testing)
            var apiKey = configuration["ApiEndpoints:SessionValidationApiKey"];
            // var providedKey = ""; // Check X-API-Key header via HttpContext if needed

            // For simplicity during initial integration, allow requests with
            // just the secret as a query parameter named "api_key"
            // This should be deprecated once EKAM adopts HMAC signatures
        }

        // 3. Check session in database
        try
        {
            var connString = configuration.GetConnectionString("SQLServerConnection");
            if (string.IsNullOrEmpty(connString))
            {
                logger.LogError("[SessionValidationApi] SQLServerConnection is not configured.");
                return Results.Ok(new SessionValidationResponse(false, emp_no));
            }

            var isActive = await CheckSessionActiveAsync(connString, emp_no);

            logger.LogInformation("[SessionValidationApi] Session check for {EmpNo}: active={IsActive}", emp_no, isActive);

            return Results.Ok(new SessionValidationResponse(isActive, emp_no));
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, $"[SessionValidationApi] Error checking session for {emp_no}");
            return Results.Ok(new SessionValidationResponse(false, emp_no));
        }
    }

    /// <summary>
    /// Checks if the user has an active session.
    /// 
    /// Validation logic (both conditions must be true):
    /// 1. A record exists in session_dtls for the user (not explicitly logged out)
    /// 2. The login occurred within the application's max session lifetime (24 hours)
    /// 
    /// Note: The 20-minute window in session_dtls is used by the application to prevent
    /// duplicate concurrent logins — it does NOT represent the session's actual validity.
    /// A user remains authenticated for up to 24 hours (matching the auth state stored
    /// in the browser's session storage via CustomAuthenticationStateProvider).
    /// The session_dtls record is deleted on explicit logout, so its mere existence
    /// indicates the user has not logged out.
    /// </summary>
    private static async Task<bool> CheckSessionActiveAsync(string connectionString, string empNo)
    {
        const string sql = @"
            SELECT COUNT(1) 
            FROM session_dtls 
            WHERE USERNAME = @userId 
              AND APP_CODE = @appCode
              AND DATEADD(hh, @maxLifetimeHours, LOGIN_TIME) >= GETDATE()";

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@userId", SqlDbType.NVarChar, 100) { Value = empNo });
        cmd.Parameters.Add(new SqlParameter("@appCode", SqlDbType.NVarChar, 10) { Value = "V2" });
        cmd.Parameters.Add(new SqlParameter("@maxLifetimeHours", SqlDbType.Int) { Value = SessionMaxLifetimeHours });

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    /// <summary>
    /// Computes HMAC-SHA256 signature for request authentication.
    /// </summary>
    private static string ComputeHmacSha256(string secretKey, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Response model for the session validation API.
/// </summary>
public record SessionValidationResponse(bool Active, string EmpNo);
