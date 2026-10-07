using MyDiary.Core;
using MyDiary.Core.Models;
using MyDiary.Core.Abstractions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyDiary.Data.SqlServer;

/// <summary>
/// Manages user session records in SQL Server (session_dtls table).
/// Writes APP_CODE = 'V2' on all session operations so sessions from
/// V2 and V3 are distinguishable in the shared table.
/// </summary>
public sealed class SessionRepository : ISessionRepository
{
    private const string AppCode = "V2";

    private readonly string _connString;
    private readonly ILogger<SessionRepository> _logger;

    public SessionRepository(IConfiguration config, ILogger<SessionRepository> logger)
    {
        _connString = config.GetConnectionString("SQLServerConnection")
            ?? throw new InvalidOperationException("Missing 'SQLServerConnection' connection string.");
        _logger = logger;
    }

    public async Task<bool> CanLoginAsync(string userId, CancellationToken ct = default)
    {
        // Only check sessions from this application (V3).
        // V2 sessions (APP_CODE IS NULL or 'V2') are ignored.
        const string sql = """
            SELECT COUNT(1)
            FROM session_dtls
            WHERE USERNAME = @UserId
              AND APP_CODE = @AppCode
              AND DATEADD(minute, 20, LOGIN_TIME) >= GETDATE()
            """;

        try
        {
            await using var conn = new SqlConnection(_connString);
            var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { UserId = userId, AppCode }, cancellationToken: ct));
            return count == 0; // true = no active session → login is allowed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking login eligibility for UserId={UserId}, AppCode={AppCode}", userId, AppCode);
            throw;
        }
    }

    public async Task AddSessionAsync(string userId, string ipAddress, CancellationToken ct = default)
    {
        // Upsert: USERNAME + APP_CODE is the logical key.
        // Replace any existing V2 session for the user.
        const string sql = """
            UPDATE session_dtls
               SET IP_ADDRESS = @IpAddress, LOGIN_TIME = GETDATE()
             WHERE USERNAME = @UserId AND APP_CODE = @AppCode;
            IF @@ROWCOUNT = 0
                INSERT INTO session_dtls (USERNAME, IP_ADDRESS, LOGIN_TIME, APP_CODE)
                VALUES (@UserId, @IpAddress, GETDATE(), @AppCode);
            """;

        try
        {
            await using var conn = new SqlConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, IpAddress = ipAddress, AppCode }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating session for UserId={UserId}, AppCode={AppCode}", userId, AppCode);
            throw;
        }
    }

    public async Task RemoveSessionAsync(string userId, CancellationToken ct = default)
    {
        // Only remove the V3 session — don't interfere with V2 sessions.
        const string sql = "DELETE FROM session_dtls WHERE USERNAME = @UserId AND APP_CODE = @AppCode";

        try
        {
            await using var conn = new SqlConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, AppCode }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing session for UserId={UserId}, AppCode={AppCode}", userId, AppCode);
            throw;
        }
    }
}
