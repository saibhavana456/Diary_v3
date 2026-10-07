using Microsoft.AspNetCore.Components.Authorization;
using MyDiary.Web.Core.Services.Storage;
using MyDiary.Web.Features.Shared.Models;
using System.Security.Claims;

namespace MyDiary.Web.Core.Services.Authentication
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ISessionStorageService _sessionStorage;
        private readonly ILogger<CustomAuthenticationStateProvider> _logger;
        private const string StorageKey = "UBI_Auth_State";
        private const int SessionHours = 24;

        private AuthenticationState? _cachedAuthState;
        private DateTime _cacheExpiry = DateTime.MinValue;

        // Event to notify when authentication state changes across tabs
        public event Action? OnAuthenticationStateChanged;

        public CustomAuthenticationStateProvider(
            ISessionStorageService sessionStorage,
            ILogger<CustomAuthenticationStateProvider> logger)
        {
            _sessionStorage = sessionStorage;
            _logger = logger;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // Return cached state if still valid
            if (_cachedAuthState != null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedAuthState;
            }

            try
            {
                // Try to restore from protected storage
                var storedAuth = await GetStoredAuthenticationAsync();
                if (storedAuth != null)
                {
                    // Cache the result for 30 seconds (increased from 5)
                    _cachedAuthState = storedAuth;
                    _cacheExpiry = DateTime.UtcNow.AddSeconds(30);

                    return storedAuth;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring authentication");
            }

            var anonymousState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            // Cache anonymous state too
            _cachedAuthState = anonymousState;
            _cacheExpiry = DateTime.UtcNow.AddSeconds(30);

            return anonymousState;
        }

        public async Task MarkUserAsAuthenticated(StaffDetailsResult staff, string? loginFromRoute = null)
        {
            var claims = GetClaimsFromStaff(staff);
            var identity = new ClaimsIdentity(claims, "UBI_Authentication");
            var principal = new ClaimsPrincipal(identity);

            // Store authentication state in session storage (plain or encrypted based on LoadTestingMode config)
            await StoreAuthenticationAsync(claims);

            // Clear cache and update with new state
            _cachedAuthState = new AuthenticationState(principal);
            _cacheExpiry = DateTime.UtcNow.AddSeconds(30);

            // Notify Blazor that authentication state has changed
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));

            // Notify other components
            OnAuthenticationStateChanged?.Invoke();

            _logger.LogInformation(
                "User authenticated: {UserId} | Login from route: {Route}",
                staff.staffdetails.EMPLID,
                string.IsNullOrWhiteSpace(loginFromRoute) ? "/" : loginFromRoute);
        }

        public async Task ApplyDevBypass(TestUser testUser, string? loginFromRoute = null)
        {
            var staff = new StaffDetailsResult
            {
                userType = testUser.Role,

                staffdetails = new StaffDetailsObj
                {
                    EMPLID = testUser.Username,
                    NAME = testUser.Name,
                    UserName = testUser.Username,
                    Privilege = testUser.Privilege,
                    EMAIL = testUser.Email,
                    PHONE = testUser.Phone,
                    DEPARTMENT_Descr = testUser.DepartmentName,
                    DEPARTMENT_Descr_EXTRA = testUser.DepartmentExtraDescription,
                    EMP_DESGN_DESC = testUser.EmployeeDesignationDescription,
                    MUD_Branch_Code = testUser.BranchCode,
                    MUD_Branch_Name = testUser.BranchName,
                    MUD_Branch_Solid = testUser.BranchSolId,
                    BRSOLID = testUser.BranchSolId,
                    MUD_Region_Code = testUser.RegionCode,
                    MUD_Region_Solid = testUser.RegionSolId,
                    MUD_Region_Name = "REGION",
                    MUD_Zone_Code = testUser.ZoneCode,
                    MUD_Zone_Solid = testUser.ZoneSolId,
                    MUD_Zone_Name = "ZONE",
                    LOCATION = testUser.BranchCode,
                    MUD_Status = "Active",
                    DataAsOn = DateTime.UtcNow
                }
            };

            await MarkUserAsAuthenticated(staff, loginFromRoute);
        }

        public async Task MarkUserAsLoggedOut()
        {
            _logger.LogInformation("User logging out");

            // Clear stored authentication
            await ClearStoredAuthenticationAsync();

            // Clear cache
            _cachedAuthState = null;
            _cacheExpiry = DateTime.MinValue;

            // Set anonymous user and notify
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymous)));

            // Notify other components
            OnAuthenticationStateChanged?.Invoke();
        }

        /// <summary>
        /// Force refresh authentication state from storage (useful for checking session validity)
        /// </summary>
        public async Task<bool> RefreshAuthenticationStateAsync()
        {
            _cachedAuthState = null;
            _cacheExpiry = DateTime.MinValue;

            var authState = await GetAuthenticationStateAsync();
            return authState.User.Identity?.IsAuthenticated == true;
        }

        private List<Claim> GetClaimsFromStaff(StaffDetailsResult staff)
        {
            var s = staff?.staffdetails;
            var privilege = s?.Privilege?.ToUpper() ?? "";

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, s?.NAME ?? ""),
                new Claim(ClaimTypes.NameIdentifier, s?.EMPLID ?? ""),
                new Claim(ClaimTypes.Role, privilege),
                new Claim("privilege", privilege),
                new Claim("user_type", staff?.userType ?? ""),
                new Claim("emplid", s?.EMPLID ?? ""),
                new Claim("employee_name", s?.NAME ?? ""),
                new Claim("sex", s?.SEX ?? ""),
                new Claim("location", s?.LOCATION ?? ""),
                new Claim("department_desc", s?.DEPARTMENT_Descr ?? ""),
                new Claim("department_extra", s?.DEPARTMENT_Descr_EXTRA ?? ""),
                new Claim("staff_region_code", s?.Staff_Region_Code ?? ""),
                new Claim("staff_region_name", s?.Staff_Region_Name ?? ""),
                new Claim("staff_division_code", s?.Staff_Division_Code ?? ""),
                new Claim("staff_division_name", s?.Staff_Division_Name ?? ""),
                new Claim("employee_designation_code", s?.EMP_DESGN ?? ""),
                new Claim("employee_designation", s?.EMP_DESGN_DESC ?? ""),
                new Claim("employee_scale_code", s?.EMP_SCALE_CODE ?? ""),
                new Claim("employee_scale_name", s?.EMP_SCALE_DESCR ?? ""),
                new Claim("date_of_birth", s?.EMP_DATE_OF_BIRTH?.ToString("yyyy-MM-dd") ?? ""),
                new Claim("joining_date", s?.EMP_JOINING_DATE?.ToString("yyyy-MM-dd") ?? ""),
                new Claim("expected_end_date", s?.EXPECTED_END_DATE?.ToString("yyyy-MM-dd") ?? ""),
                new Claim("posting_date", s?.POSTING_DATE?.ToString("yyyy-MM-dd") ?? ""),
                new Claim("data_as_on", s?.DataAsOn?.ToString("yyyy-MM-dd") ?? ""),
                new Claim("last_update_date", s?.LastUpdateDate?.ToString("yyyy-MM-dd") ?? ""),
                new Claim("phone", s?.PHONE ?? ""),
                new Claim("email", s?.EMAIL ?? ""),
                new Claim("account_number", s?.ACC_NUM ?? ""),
                new Claim("branch_ec_code", s?.BRANCH_EC_CD ?? ""),
                new Claim("br_solid", s?.BRSOLID ?? ""),
                new Claim("username", s?.UserName ?? ""),
                new Claim("access", s?.Access ?? ""),
                new Claim("status", s?.MUD_Status ?? ""),
                new Claim("region_solid", s?.MUD_Region_Solid ?? ""),
                new Claim("region_code", s?.MUD_Region_Code ?? ""),
                new Claim("region_name", s?.MUD_Region_Name ?? ""),
                new Claim("zone_solid", s?.MUD_Zone_Solid ?? ""),
                new Claim("zone_code", s?.MUD_Zone_Code ?? ""),
                new Claim("zone_name", s?.MUD_Zone_Name ?? ""),
                // For CO users, MUD_Branch_Solid is "00000" — fall back to BRSOLID which has the actual value
                new Claim("branch_solid", ResolveBranchSolid(s?.MUD_Branch_Solid, s?.BRSOLID)),
                new Claim("branch_code", s?.MUD_Branch_Code ?? ""),
                new Claim("branch_name", s?.MUD_Branch_Name ?? ""),
                new Claim("mobile", s?.MUD_Mobile ?? ""),
                new Claim("last_entry_date", s?.MUD_Last_Entry_Date ?? ""),
                new Claim("last_week_start", s?.MUD_Last_Week_Start_Date?.ToString("yyyy-MM-ddTHH:mm:ss") ?? ""),
                new Claim("last_week_end", s?.MUD_Last_Week_End_Date?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "")
            };

            return claims;
        }

        /// <summary>
        /// For CO users, MUD_Branch_Solid is "00000". Use BRSOLID as fallback.
        /// </summary>
        private static string ResolveBranchSolid(string? mudBranchSolid, string? brSolid)
        {
            if (!string.IsNullOrEmpty(mudBranchSolid) && mudBranchSolid != "00000" && mudBranchSolid != "0")
                return mudBranchSolid;

            return brSolid ?? "";
        }

        private async Task StoreAuthenticationAsync(List<Claim> claims)
        {
            try
            {
                var authData = new StoredAuthData
                {
                    Claims = claims.Select(c => new StoredClaim { Type = c.Type, Value = c.Value }).ToList(),
                    ExpiresAt = DateTime.UtcNow.AddHours(SessionHours)
                };

                await _sessionStorage.SetAsync(StorageKey, authData);

                _logger.LogInformation("Authentication stored successfully for user {UserId}",
                    claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing authentication");
            }
        }

        private async Task<AuthenticationState?> GetStoredAuthenticationAsync()
        {
            try
            {
                var authData = await _sessionStorage.GetAsync<StoredAuthData>(StorageKey);

                if (authData == null)
                {
                    return null;
                }

                if (authData.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogInformation("Stored authentication expired");
                    await ClearStoredAuthenticationAsync();
                    return null;
                }

                var claims = authData.Claims.Select(c => new Claim(c.Type, c.Value)).ToList();
                var identity = new ClaimsIdentity(claims, "UBI_Authentication");
                var principal = new ClaimsPrincipal(identity);

                return new AuthenticationState(principal);
            }
            catch (InvalidOperationException)
            {
                // JS interop not available during static prerendering — this is expected.
                // Auth state will be resolved once the circuit is established.
                _logger.LogDebug("Skipping session storage read during prerender");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving stored authentication");
                return null;
            }
        }

        private async Task ClearStoredAuthenticationAsync()
        {
            try
            {
                await _sessionStorage.RemoveAsync(StorageKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error clearing stored authentication");
            }
        }

        private class StoredAuthData
        {
            public List<StoredClaim> Claims { get; set; } = new();
            public DateTime ExpiresAt { get; set; }
        }

        private class StoredClaim
        {
            public string Type { get; set; } = "";
            public string Value { get; set; } = "";
        }
    }
}
