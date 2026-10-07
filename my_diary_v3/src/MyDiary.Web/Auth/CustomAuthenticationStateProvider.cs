using Microsoft.AspNetCore.Components.Authorization;
using MyDiary.Core;
using MyDiary.Core.Abstractions;
using MyDiary.Core.Models;
using MyDiary.Web.Core.Services.Storage;
using System.Security.Claims;

namespace MyDiary.Web.Auth;

/// <summary>
/// Merged authentication state provider:
///   - Uses Project B's session-storage pattern (AES-encrypted browser storage)
///   - Enriches claims with Project A's RBAC roles from the database
///   - 24-hour session expiry, 30-second in-memory cache
/// </summary>
public sealed class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ISessionStorageService _storage;
    private readonly IUserRepository        _users;
    private readonly global::RequestPortal.Core.Abstractions.IRbacService _rbac;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger;

    private const string StorageKey   = "MD_Auth_State";
    private const int    SessionHours = 24;
    private const int    CacheSeconds = 30;

    private AuthenticationState? _cached;
    private DateTime             _cacheExpiry = DateTime.MinValue;

    public event Action? OnAuthStateChanged;

    public CustomAuthenticationStateProvider(
        ISessionStorageService storage,
        IUserRepository users,
        global::RequestPortal.Core.Abstractions.IRbacService rbac,
        ILogger<CustomAuthenticationStateProvider> logger)
    {
        _storage = storage;
        _users   = users;
        _rbac    = rbac;
        _logger  = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cached is not null && DateTime.UtcNow < _cacheExpiry)
            return _cached;

        try
        {
            var stored = await GetStoredStateAsync();
            if (stored is not null)
            {
                _cached      = stored;
                _cacheExpiry = DateTime.UtcNow.AddSeconds(CacheSeconds);
                return stored;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring authentication state");
        }

        var anon = Anonymous();
        _cached      = anon;
        _cacheExpiry = DateTime.UtcNow.AddSeconds(CacheSeconds);
        return anon;
    }

    /// <summary>
    /// Called after successful AD API validation.
    /// Builds claims from staff details, then enriches with RBAC roles from Oracle.
    /// </summary>
    public async Task MarkAuthenticatedAsync(StaffDetails staff, CancellationToken ct = default)
    {
        var claims = await BuildClaimsAsync(staff, ct);
        var identity  = new ClaimsIdentity(claims, "MD_Authentication");
        var principal = new ClaimsPrincipal(identity);

        await PersistClaimsAsync(claims);

        _cached      = new AuthenticationState(principal);
        _cacheExpiry = DateTime.UtcNow.AddSeconds(CacheSeconds);

        NotifyAuthenticationStateChanged(Task.FromResult(_cached));
        OnAuthStateChanged?.Invoke();

        _logger.LogInformation("User authenticated: {EmplId}", staff.EmplId);
    }

    public async Task MarkLoggedOutAsync()
    {
        _logger.LogInformation("User logging out");

        await _storage.RemoveAsync(StorageKey);
        _cached      = null;
        _cacheExpiry = DateTime.MinValue;

        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));
        OnAuthStateChanged?.Invoke();
    }

    public async Task<bool> RefreshAsync()
    {
        _cached      = null;
        _cacheExpiry = DateTime.MinValue;
        var state = await GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<List<Claim>> BuildClaimsAsync(StaffDetails s, CancellationToken ct)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name,           s.Name         ?? ""),
            new(ClaimTypes.NameIdentifier, s.EmplId       ?? ""),
            new(ClaimTypes.Role,           s.Privilege    ?? ""),
            new("privilege",               s.Privilege    ?? ""),
            new("user_type",               s.UserType     ?? ""),
            new("emplid",                  s.EmplId       ?? ""),
            new("employee_name",           s.Name         ?? ""),
            new("sex",                     s.Sex          ?? ""),
            new("location",                s.Location     ?? ""),
            new("email",                   s.Email        ?? ""),
            new("phone",                   s.Phone        ?? ""),
            new("department_desc",         s.DepartmentDescr      ?? ""),
            new("department_extra",        s.DepartmentDescrExtra ?? ""),
            new("staff_region_code",       s.StaffRegionCode      ?? ""),
            new("staff_region_name",       s.StaffRegionName      ?? ""),
            new("staff_division_code",     s.StaffDivisionCode    ?? ""),
            new("staff_division_name",     s.StaffDivisionName    ?? ""),
            new("employee_designation_code", s.EmpDesignation     ?? ""),
            new("employee_designation",    s.EmpDesignationDesc   ?? ""),
            new("employee_scale_code",     s.EmpScaleCode         ?? ""),
            new("employee_scale_name",     s.EmpScaleDescr        ?? ""),
            new("date_of_birth",           s.DateOfBirth?.ToString("yyyy-MM-dd")     ?? ""),
            new("joining_date",            s.JoiningDate?.ToString("yyyy-MM-dd")     ?? ""),
            new("expected_end_date",       s.ExpectedEndDate?.ToString("yyyy-MM-dd") ?? ""),
            new("posting_date",            s.PostingDate?.ToString("yyyy-MM-dd")     ?? ""),
            new("data_as_on",              s.DataAsOn?.ToString("yyyy-MM-dd")        ?? ""),
            new("last_update_date",        s.LastUpdateDate?.ToString("yyyy-MM-dd")  ?? ""),
            new("account_number",          s.AccNum         ?? ""),
            new("branch_ec_code",          s.BranchEcCode   ?? ""),
            new("br_solid",                s.BrSolid        ?? ""),
            new("username",                s.UserName       ?? ""),
            new("access",                  s.Access         ?? ""),
            new("branch_solid",            ResolveBranchSolid(s.BranchSolid, s.BrSolid)),
            new("branch_code",             s.BranchCode   ?? ""),
            new("branch_name",             s.BranchName   ?? ""),
            new("region_solid",            s.RegionSolid  ?? ""),
            new("region_code",             s.RegionCode   ?? ""),
            new("region_name",             s.RegionName   ?? ""),
            new("zone_solid",              s.ZoneSolid    ?? ""),
            new("zone_code",               s.ZoneCode     ?? ""),
            new("zone_name",               s.ZoneName     ?? ""),
            new("status",                  s.Status       ?? ""),
            new("mobile",                  s.Mobile       ?? ""),
            new("last_entry_date",         s.LastEntryDate ?? ""),
            new("last_week_start",         s.LastWeekStart?.ToString("yyyy-MM-ddTHH:mm:ss") ?? ""),
            new("last_week_end",           s.LastWeekEnd?.ToString("yyyy-MM-ddTHH:mm:ss")   ?? ""),
        };

        // Enrich with RBAC roles + admin tier from Oracle (Project A pattern).
        // Each effective role is emitted as BOTH md_role (My Diary RBAC) and
        // rp_role (Request Portal RBAC) so policies from both apps resolve.
        //
        // GetEffectiveRolesAsync and IsSuperAdminAsync are independent reads (each
        // opens its own OracleConnection), so run them concurrently rather than
        // sequentially — this was previously 3 back-to-back connection round-trips
        // on every login, a measurable chunk of login latency. IsAppAdminAsync stays
        // conditional (only needed when not already super admin), same as before.
        var empCode = s.EmplId ?? "";
        var rolesTask = GetRolesSafeAsync(empCode, ct);
        var superAdminTask = IsSuperAdminSafeAsync(empCode, ct);

        await Task.WhenAll(rolesTask, superAdminTask);

        foreach (var role in rolesTask.Result)
        {
            claims.Add(new Claim("md_role", role.ToString()));
            claims.Add(new Claim("rp_role", role.ToString()));
        }

        // Baseline role so migrated Project A & B pages are accessible to every
        // authenticated user initially (per migration requirement). RBAC can later
        // refine access via the admin screens.
        claims.Add(new Claim("rp_role", RoleCode.BranchUser.ToString()));
        claims.Add(new Claim("md_role", RoleCode.BranchUser.ToString()));

        // Project A RBAC admin tiers — checked against RP_RBAC_SUPER_ADMIN /
        // RP_RBAC_APP_ADMIN. These drive the SuperAdmin/AppAdmin authorization
        // policies and the Access-Management section of the sidebar.
        if (!string.IsNullOrWhiteSpace(empCode))
        {
            if (superAdminTask.Result)
            {
                claims.Add(new Claim("rp_super_admin", "true"));
                claims.Add(new Claim("rp_app_admin",   "true")); // super admin implies app admin
                claims.Add(new Claim("rp_role", RoleCode.SuperAdmin.ToString()));
            }
            else if (await IsAppAdminSafeAsync(empCode, ct))
            {
                claims.Add(new Claim("rp_app_admin", "true"));
                claims.Add(new Claim("rp_role", RoleCode.AppAdmin.ToString()));
            }

            // Feedback admin — independent of AppAdmin/SuperAdmin tiers; drives the
            // FeedbackAdmin policy gating the Feedback Dashboard/list (super admins
            // already pass that policy via rp_super_admin above).
            if (!superAdminTask.Result && await IsFeedbackAdminSafeAsync(empCode, ct))
            {
                claims.Add(new Claim("rp_feedback_admin", "true"));
            }
        }

        return claims;
    }

    private async Task<IReadOnlyList<RoleCode>> GetRolesSafeAsync(string emplId, CancellationToken ct)
    {
        try
        {
            return await _users.GetEffectiveRolesAsync(emplId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load RBAC roles for {EmplId} — proceeding without them", emplId);
            return Array.Empty<RoleCode>();
        }
    }

    private async Task<bool> IsSuperAdminSafeAsync(string empCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return false;
        try
        {
            return await _rbac.IsSuperAdminAsync(empCode, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve super-admin tier for {EmplId}", empCode);
            return false;
        }
    }

    private async Task<bool> IsAppAdminSafeAsync(string empCode, CancellationToken ct)
    {
        try
        {
            return await _rbac.IsAppAdminAsync(empCode, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve app-admin tier for {EmplId}", empCode);
            return false;
        }
    }

    private async Task<bool> IsFeedbackAdminSafeAsync(string empCode, CancellationToken ct)
    {
        try
        {
            return await _rbac.IsFeedbackAdminAsync(empCode, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve feedback-admin tier for {EmplId}", empCode);
            return false;
        }
    }

    private async Task PersistClaimsAsync(List<Claim> claims)
    {
        var data = new StoredAuth
        {
            Claims    = claims.Select(c => new StoredClaim { Type = c.Type, Value = c.Value }).ToList(),
            ExpiresAt = DateTime.UtcNow.AddHours(SessionHours)
        };
        await _storage.SetAsync(StorageKey, data);
    }

    private async Task<AuthenticationState?> GetStoredStateAsync()
    {
        try
        {
            var data = await _storage.GetAsync<StoredAuth>(StorageKey);
            if (data is null || data.ExpiresAt < DateTime.UtcNow)
            {
                if (data is not null)
                    await _storage.RemoveAsync(StorageKey);
                return null;
            }

            var claims    = data.Claims.Select(c => new Claim(c.Type, c.Value)).ToList();
            var identity  = new ClaimsIdentity(claims, "MD_Authentication");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (InvalidOperationException)
        {
            // JS interop unavailable during static prerendering — expected
            return null;
        }
        catch (Microsoft.JSInterop.JSDisconnectedException)
        {
            // Circuit disconnected — return null to avoid crashing the auth pipeline
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading stored auth");
            return null;
        }
    }

    private static AuthenticationState Anonymous() =>
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    // CO users may have "00000" as MUD_Branch_Solid — fall back to BRSOLID
    private static string ResolveBranchSolid(string? mudBranchSolid, string? brSolid)
    {
        if (!string.IsNullOrEmpty(mudBranchSolid)
            && mudBranchSolid != "00000"
            && mudBranchSolid != "0")
            return mudBranchSolid;
        return brSolid ?? "";
    }

    // ── Storage DTOs (never leave this class) ─────────────────────────────

    private sealed class StoredAuth
    {
        public List<StoredClaim> Claims    { get; set; } = new();
        public DateTime          ExpiresAt { get; set; }
    }

    private sealed class StoredClaim
    {
        public string Type  { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
