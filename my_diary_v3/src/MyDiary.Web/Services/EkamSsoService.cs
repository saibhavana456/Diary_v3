using System.Net.Http;
using System.Text;
using System.Text.Json;
using MyDiary.Web.Core.Extensions;

namespace MyDiary.Web.Services;

/// <summary>
/// Encrypts parameters for EKAM SSO redirection using the CryptoService RSA endpoint.
/// The base URL comes from the database (app.LoginUrl); this service only handles encryption
/// by delegating to the centralized CryptoService API.
/// </summary>
public class EkamSsoService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _cryptoServiceUrl;
    private readonly bool _isAvailable;

    public EkamSsoService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _cryptoServiceUrl = configuration["ApiEndpoints:CryptoServiceUrl"]?.TrimEnd('/');
        _isAvailable = !string.IsNullOrEmpty(_cryptoServiceUrl);

        if (!_isAvailable)
        {
            AppLogger.LogWarning("[EkamSsoService] CryptoServiceUrl not configured. EKAM SSO will be unavailable.");
        }
    }

    /// <summary>
    /// Returns true if the service is configured with a valid CryptoService URL.
    /// </summary>
    public bool IsAvailable => _isAvailable;

    /// <summary>
    /// Builds the full EKAM SSO redirect URL by appending encrypted query params to the DB URL.
    /// </summary>
    /// <param name="baseUrl">The LoginUrl from the database.</param>
    /// <param name="empNo">Employee number to encrypt.</param>
    /// <param name="urlPath">Landing page code: 'lp', 'rct', 'appraisal', 'cc'.</param>
    public string BuildEkamUrl(string baseUrl, string empNo, string urlPath = "lp")
    {
        if (!_isAvailable)
        {
            AppLogger.LogWarning("[EkamSsoService] BuildEkamUrl called but service is unavailable (CryptoService not configured).");
            return baseUrl;
        }

        try
        {
            var encEmpNo = EncryptViaApi(empNo);
            var encUrlPath = EncryptViaApi(urlPath);

            if (string.IsNullOrEmpty(encEmpNo) || string.IsNullOrEmpty(encUrlPath))
            {
                AppLogger.LogWarning("[EkamSsoService] RSA encryption returned empty. Falling back to base URL.");
                return baseUrl;
            }

            var separator = baseUrl.Contains('?') ? "&" : "?";
            var url = $"{baseUrl}{separator}emp_no={Uri.EscapeDataString(encEmpNo)}&url_path={Uri.EscapeDataString(encUrlPath)}";

            AppLogger.LogInfo($"[EkamSsoService] Built EKAM URL for emp {empNo}");
            return url;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, $"[EkamSsoService] BuildEkamUrl failed for emp: {empNo}");
            return baseUrl;
        }
    }

    private string? EncryptViaApi(string plainText)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CryptoService");
            var payload = JsonSerializer.Serialize(new { PlainText = plainText });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = client.PostAsync($"{_cryptoServiceUrl}/api/crypto/rsa/encrypt", content).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                AppLogger.LogWarning($"[EkamSsoService] CryptoService RSA encrypt returned {response.StatusCode}");
                return null;
            }

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var result = JsonSerializer.Deserialize<CryptoApiResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null || !result.Success)
            {
                AppLogger.LogWarning($"[EkamSsoService] CryptoService RSA encrypt failed: {result?.Error ?? "Unknown"}");
                return null;
            }

            return result.Result;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "[EkamSsoService] EncryptViaApi call to CryptoService failed.");
            return null;
        }
    }

    private class CryptoApiResponse
    {
        public bool Success { get; set; }
        public string Result { get; set; } = "";
        public string? Error { get; set; }
    }
}
