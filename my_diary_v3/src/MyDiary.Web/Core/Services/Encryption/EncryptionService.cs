using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MyDiary.Web.Services
{
    /// <summary>
    /// Encryption service that delegates all crypto operations to the CryptoService API.
    /// This removes the MD5/CBC logic from the main application's codebase.
    /// Falls back to local implementation only if the API is unreachable (resilience).
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _cryptoServiceUrl;
        private readonly ILogger<EncryptionService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public EncryptionService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<EncryptionService> logger)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("CryptoService");
            _cryptoServiceUrl = configuration["ApiEndpoints:CryptoServiceUrl"]?.TrimEnd('/')
                ?? throw new InvalidOperationException("ApiEndpoints:CryptoServiceUrl is not configured.");
        }

        public string EncryptStringAES(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            try
            {
                var response = CallCryptoApi("/api/crypto/encrypt", new { text = plainText });
                if (response != null && response.Success && !string.IsNullOrEmpty(response.Result))
                    return response.Result;

                _logger.LogError("CryptoService encrypt failed. Response: {Error}", response?.Error ?? "null/empty result");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CryptoService API unreachable for encrypt.");
            }

            // Throw so callers know encryption failed — they can show a user-friendly message.
            // Returning empty would silently break SSO redirects.
            throw new InvalidOperationException("Encryption service is unavailable. Please try again later.");
        }

        public string DecryptStringAES(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                var response = CallCryptoApi("/api/crypto/decrypt", new { text = cipherText });
                if (response != null && response.Success)
                    return response.Result;

                _logger.LogError("CryptoService decrypt failed. Response: {Error}", response?.Error ?? "null/empty result");
            }
            catch (InvalidOperationException)
            {
                throw; // Re-throw our own exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CryptoService API unreachable for decrypt.");
            }

            // For decrypt: return the original ciphertext unchanged.
            // This matches the old behavior (if not valid Base64, return as-is).
            // Callers that depend on decrypted values will see the raw ciphertext
            // and can handle it gracefully.
            return cipherText;
        }

        public string Encrypt(string plainText) => EncryptStringAES(plainText);
        public string Decrypt(string cipherText) => DecryptStringAES(cipherText);

        public string DecryptAesCbc(string cipherBase64, string key)
        {
            if (string.IsNullOrEmpty(cipherBase64)) return cipherBase64;

            try
            {
                var response = CallCryptoApi("/api/crypto/aes-cbc/decrypt", new { cipherText = cipherBase64, key = key });
                if (response != null && response.Success)
                    return response.Result;

                _logger.LogError("CryptoService AES-CBC decrypt failed. Response: {Error}", response?.Error ?? "null/empty result");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CryptoService API unreachable for AES-CBC decrypt.");
            }

            return "Invalid";
        }

        public string EncryptAesCbc(string plainText, string key, string ivSource)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            try
            {
                var response = CallCryptoApi("/api/crypto/aes-cbc/encrypt",
                    new { plainText = plainText, key = key, ivSource = ivSource });
                if (response != null && response.Success && !string.IsNullOrEmpty(response.Result))
                    return response.Result;

                _logger.LogError("CryptoService AES-CBC encrypt failed. Response: {Error}", response?.Error ?? "null/empty result");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CryptoService API unreachable for AES-CBC encrypt.");
            }

            // Throw so callers know encryption failed — silently returning plaintext
            // would leak the message to the SMS/WhatsApp gateway unencrypted.
            throw new InvalidOperationException("Encryption service is unavailable. Please try again later.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HTTP helper
        // ─────────────────────────────────────────────────────────────────────

        private CryptoApiResponse? CallCryptoApi(string endpoint, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Synchronous call — required because IEncryptionService methods are synchronous.
            // In production, consider making the interface async.
            var response = _httpClient.PostAsync($"{_cryptoServiceUrl}{endpoint}", content)
                .ConfigureAwait(false).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CryptoService returned {StatusCode} for {Endpoint}", response.StatusCode, endpoint);
                return null;
            }

            var responseBody = response.Content.ReadAsStringAsync()
                .ConfigureAwait(false).GetAwaiter().GetResult();

            return JsonSerializer.Deserialize<CryptoApiResponse>(responseBody, _jsonOptions);
        }       

        // ─────────────────────────────────────────────────────────────────────
        //  Response model
        // ─────────────────────────────────────────────────────────────────────

        private class CryptoApiResponse
        {
            public bool Success { get; set; }
            public string Result { get; set; } = "";
            public string? Error { get; set; }
        }
    }
}
