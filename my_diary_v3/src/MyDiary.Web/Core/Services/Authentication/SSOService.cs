using Microsoft.AspNetCore.Components.Authorization;
using MyDiary.Core.Services;
using MyDiary.Web.Services;
using System.Security.Claims;
using System.Text.Json;

namespace MyDiary.Web.Core.Services.Authentication
{
    /// <summary>
    /// Service for Single Sign-On (SSO) to external applications
    /// </summary>
    public class SSOService
    {
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<SSOService> _logger;

        public SSOService(
            AuthenticationStateProvider authStateProvider,
            IEncryptionService encryptionService,
            ILogger<SSOService> logger)
        {
            _authStateProvider = authStateProvider;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        /// <summary>
        /// Generate SSO token for external application
        /// </summary>
        public async Task<string?> GenerateSSOTokenAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;

                if (user?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("Cannot generate SSO token: User not authenticated");
                    return null;
                }

                // Extract user claims
                var ssoData = new SSOTokenData
                {
                    UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "",
                    UserName = user.FindFirst(ClaimTypes.Name)?.Value ?? "",
                    Email = user.FindFirst("email")?.Value ?? "",
                    Phone = user.FindFirst("phone")?.Value ?? "",
                    Role = user.FindFirst(ClaimTypes.Role)?.Value ?? "",
                    BranchCode = user.FindFirst("br_code")?.Value ?? "",
                    RegionCode = user.FindFirst("region_code")?.Value ?? "",
                    ZoneCode = user.FindFirst("location")?.Value ?? "",
                    DepartmentId = user.FindFirst("department_id")?.Value ?? "",
                    Designation = user.FindFirst("employee_designation")?.Value ?? "",
                    Timestamp = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5) // Token valid for 5 minutes
                };

                // Serialize to JSON
                var json = JsonSerializer.Serialize(ssoData);

                // Encrypt the data
                // var encryptedToken = _encryptionService.Encrypt(json);
                var encryptedToken = _encryptionService.EncryptStringAES(ssoData.UserId + "|" + AppTime.Now.ToString());
                var decryptedToken = _encryptionService.DecryptStringAES(encryptedToken);

                _logger.LogInformation("SSO token generated for user: {UserId}", ssoData.UserId);

                return encryptedToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating SSO token");
                return null;
            }
        }

        /// <summary>
        /// Build SSO URL with encrypted token
        /// </summary>
        public async Task<string> BuildSSOUrlAsync(string targetUrl)
        {
            var token = await GenerateSSOTokenAsync();
            
            if (string.IsNullOrEmpty(token))
            {
                // If token generation fails, return the target URL without SSO
                return targetUrl;
            }

            // URL encode the token
            var encodedToken = Uri.EscapeDataString(token);

            // Append token to URL
            var separator = targetUrl.Contains('?') ? "&" : "?";
            return $"{targetUrl}{separator}data={encodedToken}";
        }
    }
    /// <summary>
    /// SSO Token Data Structure
    /// </summary>
    public class SSOTokenData
    {
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Role { get; set; } = "";
        public string BranchCode { get; set; } = "";
        public string RegionCode { get; set; } = "";
        public string ZoneCode { get; set; } = "";
        public string DepartmentId { get; set; } = "";
        public string Designation { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
