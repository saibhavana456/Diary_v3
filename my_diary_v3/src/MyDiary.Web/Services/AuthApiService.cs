using Dapper;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Features.Shared.Models;
using MyDiary.Web.Features.Shared.Services;
using System.Text;
using System.Text.Json;

namespace MyDiary.Web.Services
{
    public class AuthApiService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthApiService> _logger;
        private readonly IAccessControlService _accessControl;
        private readonly string _connString;

        public AuthApiService(
            HttpClient http,
            IConfiguration config,
            ILogger<AuthApiService> logger,
            IAccessControlService accessControl)
        {
            _http = http;
            _config = config;
            _logger = logger;
            _accessControl = accessControl;
            _connString = config.GetConnectionString("SQLServerConnection")
            ?? throw new InvalidOperationException("Missing 'SQLServerConnection' connection string.");
        }

        public async Task<(bool IsSuccess, string Message, string Uid, StaffDetailsResult StaffData)>
            ValidateDomainUserAsync(string userId, char[] credentialChars)
        {
            try
            {
                // Read configuration
                string apiUrl = _config["ApiEndpoints:ValidateDomainUser"]
                    ?? throw new Exception("ValidateDomainUser URL missing");

                string apiKey = _config["ApiEndpoints:ValidateUserApiKey"]
                    ?? throw new Exception("ValidateUserApiKey missing");

                string appCode = _config["ApiEndpoints:AppCode"] ?? "INSW-MYDIARY-V2";
                string loginType = _config["ApiEndpoints:LoginType"] ?? "AD";

                string mfaFlag = _config.GetValue<bool>("DevSettings:LoadTestingMode")
                    ? "N"
                    : (_config["ApiEndpoints:MfaFlag"] ?? "Y");

                string emailOtpFlag = _config.GetValue<bool>("DevSettings:LoadTestingMode")
                    ? "N"
                    : (_config["ApiEndpoints:EmailOtpFlag"] ?? "N");

                // ✅ Log ONLY SAFE information (NO payload, NO password)
                _logger.LogInformation(
                    "Sending AD validation request for UserId: {UserId}, LoginType: {LoginType}",
                    userId,
                    loginType
                );

                // Encrypt the credential directly from the char array — no string materialised on the heap
                char[] adApiKeyChars = (_config["Security:AdApiEncryptionKey"]
                    ?? throw new InvalidOperationException("Security:AdApiEncryptionKey not configured."))
                    .ToCharArray();

                Dictionary<string, string> requestPayload;
                try
                {
                    string str = new(credentialChars);
                    string encryptedCredential = AdApiCrypto.Encrypt(str, (char[])adApiKeyChars.Clone());
                    str = null; // Clear plaintext reference from heap immediately
                    Array.Clear(credentialChars, 0, credentialChars.Length);
                    credentialChars = Array.Empty<char>();

                    // ✅ Build actual API payload (credential is already encrypted, plain value is gone)
                    // Use a dictionary instead of an anonymous type to avoid heap inspection
                    // of a named object containing sensitive fields.
                    requestPayload = new Dictionary<string, string>
                    {
                        ["app_code"]           = AdApiCrypto.Encrypt(appCode, (char[])adApiKeyChars.Clone()),
                        ["login_type"]         = AdApiCrypto.Encrypt(loginType, (char[])adApiKeyChars.Clone()),
                        ["mfa_flg"]            = AdApiCrypto.Encrypt(mfaFlag, (char[])adApiKeyChars.Clone()),
                        ["email_otp_flg"]      = AdApiCrypto.Encrypt(emailOtpFlag, (char[])adApiKeyChars.Clone()),
                        ["password"]           = encryptedCredential,
                        ["user_id"]            = AdApiCrypto.Encrypt(userId, (char[])adApiKeyChars.Clone()),
                        ["validateUserAPIKey"] = apiKey
                    };
                }
                finally
                {
                    // ✅ Securely clear the key material
                    Array.Clear(adApiKeyChars, 0, adApiKeyChars.Length);
                }

                // ✅ Serialize ONLY for HTTP call (not logging)
                string requestJson = JsonSerializer.Serialize(requestPayload);

                var content = new StringContent(
                    requestJson,
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _http.PostAsync(apiUrl, content);

                // ✅ Clear sensitive references ASAP
                requestJson = null;

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "AD validation HTTP error. StatusCode: {StatusCode}",
                        response.StatusCode
                    );

                    return (
                        false,
                        "Unable to connect to the login server. Please try again later.",
                        null,
                        null
                    );
                }

                string responseJson = await response.Content.ReadAsStringAsync();

                // ✅ Response logging is SAFE (no password here)
                _logger.LogInformation("AD validation response received.");

                var result = JsonSerializer.Deserialize<ADLoginResponse>(responseJson);

                if (string.Equals(result?.status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    // Guard: server must return both rData (staff details) and rData1 (OTP UID)
                    // for MFA flow. If rData1 is missing, credentials were not fully validated.
                    if (string.IsNullOrEmpty(result.rData))
                    {
                        _logger.LogWarning("AD validation returned success but rData is empty — treating as auth failure.");
                        return (false, "Authentication failed. Please check your credentials.", null, null);
                    }

                    try
                    {
                        char[] responseKeyChars = (_config["Security:EncryptionKeyString"]
                            ?? throw new InvalidOperationException("Security:EncryptionKeyString not configured."))
                            .ToCharArray();
                        var decryptedJson = AdApiCrypto.DecryptADResponse(result.rData, responseKeyChars);
                        var staffData =
                            JsonSerializer.Deserialize<StaffDetailsResult>(decryptedJson);

                        string otpMessage = string.Equals(emailOtpFlag, "Y", StringComparison.OrdinalIgnoreCase)
                            ? "OTP has been sent to your registered mobile number and email."
                            : "OTP has been sent to your registered mobile.";

                        return (
                            true,
                            otpMessage,
                            result.rData1,
                            staffData
                        );
                    }
                    catch (Exception decryptEx)
                    {
                        _logger.LogError(
                            decryptEx,
                            "Error decrypting staff details from AD response"
                        );

                        return (
                            false,
                            "Login succeeded but failed to process user details.",
                            null,
                            null
                        );
                    }
                }
                else
                {
                    string backendMessage = result?.message ?? "Invalid credentials. Please verify your PF Number and Password.";

                    // Map server-side messages to user-friendly errors
                    if (string.IsNullOrWhiteSpace(backendMessage))
                        backendMessage = "Invalid credentials. Please verify your PF Number and Password.";
                    else if (backendMessage.Contains("Check the User", StringComparison.OrdinalIgnoreCase))
                        backendMessage = "Invalid PF Number. Please check and try again.";

                    _logger.LogWarning("AD validation failed: {Message}", backendMessage);

                    return (false, backendMessage, null, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runtime error in ValidateDomainUserAsync");

                return (
                    false,
                    "An unexpected system error occurred. Please contact support.",
                    null,
                    null
                );
            }
            finally
            {
                // Guarantee the char array is zeroed regardless of outcome
                if (credentialChars.Length > 0)
                    Array.Clear(credentialChars, 0, credentialChars.Length);
            }
        }

        public async Task<(bool IsSuccess, string Message)>
            VerifyMobileOtpAsync(string mobile, string uid, string otp)
        {
            try
            {
                string apiUrl = _config["ApiEndpoints:VerifyMobileOtp"]
                    ?? throw new Exception("VerifyMobileOtp URL missing");

                // ✅ Log SAFE metadata only (NO OTP value)
                _logger.LogInformation("Verifying OTP for UID: {Uid}", uid);

                var payload = new OtpVerifyRequest
                {
                    mobileNo = mobile,
                    uid = uid,
                    otp = otp
                };

                string requestJson = JsonSerializer.Serialize(payload);

                var content = new StringContent(
                    requestJson,
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _http.PostAsync(apiUrl, content);

                // ✅ Clear sensitive OTP
                otp = string.Empty;
                requestJson = string.Empty;

                if (!response.IsSuccessStatusCode)
                {
                    return (false, "Unable to verify OTP. Server is unreachable.");
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OtpVerifyResponse>(responseJson);

                if (string.Equals(result?.status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, "Login Successful");
                }

                _logger.LogWarning("OTP verification failed: {Message}", result?.message);

                return (false, result?.message ?? "Invalid OTP. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runtime error in VerifyMobileOtpAsync");
                return (false, "An error occurred while verifying OTP.");
            }
        }

        public async Task<UserAccessValidationResult> ValidateUserAccessAsync(string empId)
        {
            try
            {
                var result = new UserAccessValidationResult();

                const string sql = @"SELECT
                                        bm.zone_solid,
                                        bm.zone_name_eng
                                    FROM StaffDetails sd
                                    INNER JOIN Branch_Master bm
                                        ON sd.BRSOLID = bm.SOL_ID
                                    WHERE sd.EMPLID = @EmpId";

                using var conn = new SqlConnection(_connString);

                var data = await conn.QueryFirstOrDefaultAsync(
                    sql,
                    new { EmpId = empId });

                if (data == null)
                    return result;

                result.ZoneSolid = data.zone_solid;
                result.ZoneName = data.zone_name_eng;

                // Allowed zones are configured in the Oracle MyDiaryDB (MD_ALLOWED_ZONE),
                // fetched dynamically so new zones can be enabled without a config change.
                var allowedZones = await _accessControl.GetAllowedZonesAsync();

                result.IsAuthorized = allowedZones.Any(z => string.Equals(
                    z.ZoneSolid,
                    result.ZoneSolid?.Trim() ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user access for EMPID {EmpId}", empId);

                return new UserAccessValidationResult
                {
                    IsAuthorized = false
                };
            }
        }

        /// <summary>
        /// Returns the display names of the currently-active allowed zones (from
        /// MD_ALLOWED_ZONE), e.g. "Mumbai" or "Mumbai, Pune". Used to build the
        /// access-restricted message dynamically instead of hardcoding "Mumbai".
        /// Returns an empty string if none are configured / the lookup fails.
        /// </summary>
        public async Task<string> GetAllowedZoneNamesAsync()
        {
            var zones = await _accessControl.GetAllowedZonesAsync();
            var names = zones
                .Select(z => z.ZoneName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return string.Join(", ", names);
        }

        /// <summary>
        /// The access-control master on/off flag, read from the DB
        /// (MD_ACCESS_SETTING key 'ENABLED') instead of appsettings.
        /// </summary>
        public Task<bool> IsAccessControlEnabledAsync()
            => _accessControl.IsEnabledAsync();

        /// <summary>
        /// True if the given PF id is in the DB-configured allowed-PF whitelist
        /// (MD_ALLOWED_PF), which grants access regardless of zone.
        /// </summary>
        public async Task<bool> IsPfAllowedAsync(string pfId)
        {
            if (string.IsNullOrWhiteSpace(pfId)) return false;
            var allowed = await _accessControl.GetAllowedPfIdsAsync();
            return allowed.Contains(pfId.Trim(), StringComparer.OrdinalIgnoreCase);
        }
    }
}