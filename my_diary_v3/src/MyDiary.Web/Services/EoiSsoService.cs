using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyDiary.Web.Core.Extensions;

namespace MyDiary.Web.Services;

/// <summary>
/// Builds the SSO launch URL for standalone microservices that use the same encrypted-query-string
/// handoff as the my_diary-Branch-Visit reference app (Auth:TokenQueryParamName = "data", decrypted by
/// the target app via the same CryptoService this app already calls for EKAM/VCard SSO — see
/// EkamSsoService for the identical encrypt-call pattern this mirrors).
/// Used for both the standalone "BranchVisit" microservice card and the new Corporate EOI Portal card.
/// </summary>
public class EoiSsoService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string? _cryptoServiceUrl;
    private readonly bool _isAvailable;

    // EoiSsoService is registered AddSingleton (see Program.cs), but IRbacService (RequestPortal.Core) is
    // Scoped — so BuildUnionYouthLaunchUrl opens its own short-lived ASYNC scope (CreateAsyncScope) per
    // call via _scopeFactory to resolve it, rather than taking IRbacService as a constructor dependency
    // (which would be a captive-dependency DI error). The scope MUST be disposed asynchronously because it
    // holds AdoUnitOfWork (IAsyncDisposable-only).
    public EoiSsoService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IServiceScopeFactory scopeFactory)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _cryptoServiceUrl = configuration["ApiEndpoints:CryptoServiceUrl"]?.TrimEnd('/');
        _isAvailable = !string.IsNullOrEmpty(_cryptoServiceUrl);

        if (!_isAvailable)
        {
            AppLogger.LogWarning("[EoiSsoService] CryptoServiceUrl not configured. Standalone-app SSO will be unavailable.");
        }
    }

    public bool IsAvailable => _isAvailable;

    /// <summary>Builds "&lt;baseUrl&gt;?data=&lt;encrypted-emp-id&gt;" — the target app decrypts this same
    /// value via the Bank's CryptoService and treats it as the employee id (see CorporateEoi's
    /// CurrentUserService / docs/eoi/EOI-Portal-External-Integrations.md for the payload-shape caveat).</summary>
    public string BuildLaunchUrl(string baseUrl, string empNo)
    {
        if (!_isAvailable)
        {
            AppLogger.LogWarning("[EoiSsoService] BuildLaunchUrl called but service is unavailable (CryptoService not configured).");
            return baseUrl;
        }

        try
        {
            var encEmpNo = EncryptViaApi(empNo);
            if (string.IsNullOrEmpty(encEmpNo))
            {
                AppLogger.LogWarning("[EoiSsoService] encryption returned empty. Falling back to base URL.");
                return baseUrl;
            }

            var separator = baseUrl.Contains('?') ? "&" : "?";
            return $"{baseUrl}{separator}data={Uri.EscapeDataString(encEmpNo)}";
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, $"[EoiSsoService] BuildLaunchUrl failed for emp: {empNo}");
            return baseUrl;
        }
    }

    /// <summary>Builds "&lt;baseUrl&gt;?data=&lt;encrypted-profile&gt;" for CCP specifically — unlike
    /// BuildLaunchUrl above (which only ever encrypts the bare employee id, still used for BranchVisit,
    /// left untouched here to avoid any risk to that already-working app), this encrypts a full
    /// query-string payload (EmployeeId, EmployeeName, Designation, Branch/Region/Zone, and the resolved
    /// EoiRole) built from the caller's claims + <paramref name="eoiRole"/>. CCP's ParsePayload reads
    /// these exact keys — see ICurrentUserService.cs on the CCP side. No external service (the Bank's
    /// CryptoService included) can supply EoiRole; it must be resolved here (via ICcpRbacService) and
    /// baked into the payload before encryption.</summary>
    public string BuildCcpLaunchUrl(string baseUrl, ClaimsPrincipal user, string eoiRole)
    {
        if (!_isAvailable)
        {
            AppLogger.LogWarning("[EoiSsoService] BuildCcpLaunchUrl called but service is unavailable (CryptoService not configured).");
            return baseUrl;
        }

        string Claim(string type) => user.FindFirst(type)?.Value ?? "";

        var fields = new (string Key, string Value)[]
        {
            ("EmployeeId", Claim("emplid")),
            ("Name", Claim(ClaimTypes.Name)),
            ("Designation", Claim("employee_designation")),
            ("BranchSolid", Claim("branch_solid")),
            ("BranchName", Claim("branch_name")),
            ("BranchCode", Claim("branch_code")),
            ("RegionSolid", Claim("region_solid")),
            ("ZoneSolid", Claim("zone_solid")),
            ("ZoneCode", Claim("zone_code")),
            ("ZoneName", Claim("zone_name")),
            ("EoiRole", eoiRole)
        };
        var payload = string.Join("&", fields.Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value)}"));

        // RISK — unverified: RSA has a hard plaintext size limit (~214-245 bytes for a 2048-bit key,
        // depending on padding), and this payload (name + branch + zone names, URL-escaped) can plausibly
        // exceed that for long names, unlike the short bare-empNo payload BuildLaunchUrl sends today. If
        // the Bank's CryptoService's /rsa/encrypt rejects or truncates an oversized plaintext, this method
        // silently falls back to the bare baseUrl below — test against the real CryptoService with a
        // long-name employee before relying on this in production; switch to short/coded values (e.g.
        // BranchSolid instead of BranchName) or a hybrid AES+RSA scheme if it doesn't fit.
        try
        {
            var encPayload = EncryptViaApi(payload);
            if (string.IsNullOrEmpty(encPayload))
            {
                AppLogger.LogWarning("[EoiSsoService] encryption returned empty for CCP payload. Falling back to base URL.");
                return baseUrl;
            }

            var separator = baseUrl.Contains('?') ? "&" : "?";
            return $"{baseUrl}{separator}data={Uri.EscapeDataString(encPayload)}";
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, $"[EoiSsoService] BuildCcpLaunchUrl failed for emp: {Claim("emplid")}");
            return baseUrl;
        }
    }

    /// <summary>Builds "&lt;baseUrl&gt;?data=&lt;encrypted-profile&gt;" for the Union Youth microservice
    /// ("Yuva Banking Mitra" registration portal) — a NEW method, separate from BuildCcpLaunchUrl (not
    /// modified, to avoid any risk to CCP), since this app also needs a real profile rather than a bare
    /// employee id, but has no role concept (no EoiRole field in its payload; every authenticated employee
    /// gets the same one-page registration/report view — see UnionYouth's CurrentUserService.ParsePayload,
    /// which reads the same key names as CCP minus EoiRole, plus RegionName since Union Youth's report
    /// wants a human-readable region label CCP doesn't carry).
    ///
    /// IsSuperAdmin resolution: identical pattern to EoiRole for CCP (BuildCcpLaunchUrl above) — resolved
    /// HERE, server-side, via RequestPortal.Core's IRbacService.IsSuperAdminAsync(empCode), which checks
    /// the RP_RBAC_SUPER_ADMIN table (the SAME mechanism/table my_diary_v3_1's own RBAC admin screens use).
    /// Union Youth trusts this flag from the token and never queries RP_RBAC_SUPER_ADMIN itself.</summary>
    /// <summary>Resolves the IsSuperAdmin flag for the Union Youth payload — split out and made properly
    /// async (unlike the old in-line `.GetAwaiter().GetResult()`, which deadlocked the Blazor Server
    /// circuit: a blocking sync-over-async call on the same SynchronizationContext the async continuation
    /// needed to resume on). Call this with `await` from Razor code-behind BEFORE calling
    /// BuildUnionYouthLaunchUrl, exactly the way ApplicationGrid1.razor already awaits
    /// CcpRbacService.ResolveEoiRoleAsync before calling BuildCcpLaunchUrl.</summary>
    public async Task<bool> ResolveUnionYouthIsSuperAdminAsync(string empId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var rbac = scope.ServiceProvider.GetRequiredService<global::RequestPortal.Core.Abstractions.IRbacService>();
            return await rbac.IsSuperAdminAsync(empId);
        }
        catch (Exception ex)
        {
            // Fail CLOSED, not open: if the RBAC lookup itself errors, treat the employee as a normal user
            // rather than risk granting the SuperAdmin dashboard on a fluke.
            AppLogger.LogError(ex, $"[EoiSsoService] IsSuperAdminAsync lookup failed for emp: {empId}; defaulting to false.");
            return false;
        }
    }

    /// This method is async because it opens a DI scope to resolve the Scoped IRbacService from this
    /// Singleton service. The scope MUST be created and disposed asynchronously — CreateAsyncScope() +
    /// `await using` — because it transitively holds AdoUnitOfWork, which implements only IAsyncDisposable;
    /// a synchronous `using` scope dispose throws "type only implements IAsyncDisposable. Use DisposeAsync
    /// to dispose the container."</summary>
    public string BuildUnionYouthLaunchUrl(string baseUrl, ClaimsPrincipal user, bool isSuperAdmin)
    {
        if (!_isAvailable)
        {
            AppLogger.LogWarning("[EoiSsoService] BuildUnionYouthLaunchUrl called but service is unavailable (CryptoService not configured).");
            return baseUrl;
        }

        string Claim(string type) => user.FindFirst(type)?.Value ?? "";
        var empId = Claim("emplid");

        var fields = new (string Key, string Value)[]
        {
            ("EmployeeId", empId),
            ("Name", Claim(ClaimTypes.Name)),
            ("Designation", Claim("employee_designation")),
            ("BranchSolid", Claim("branch_solid")),
            ("BranchName", Claim("branch_name")),
            ("BranchCode", Claim("branch_code")),
            ("RegionSolid", Claim("region_solid")),
            ("RegionName", Claim("region_name")),
            ("ZoneSolid", Claim("zone_solid")),
            ("ZoneCode", Claim("zone_code")),
            ("ZoneName", Claim("zone_name")),
            ("IsSuperAdmin", isSuperAdmin ? "true" : "false")
        };
        var payload = string.Join("&", fields.Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value)}"));

        try
        {
            var encPayload = EncryptViaApi(payload);
            if (string.IsNullOrEmpty(encPayload))
            {
                AppLogger.LogWarning("[EoiSsoService] encryption returned empty for Union Youth payload. Falling back to base URL.");
                return baseUrl;
            }

            var separator = baseUrl.Contains('?') ? "&" : "?";
            return $"{baseUrl}{separator}data={Uri.EscapeDataString(encPayload)}";
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, $"[EoiSsoService] BuildUnionYouthLaunchUrl failed for emp: {empId}");
            return baseUrl;
        }
    }
    public string BuildExecutiveBranchVisitUrl(string baseUrl, ClaimsPrincipal user)
    {
        if (!_isAvailable)
        {
            AppLogger.LogWarning(
                "[EoiSsoService] BuildExecutiveBranchVisitUrl called but CryptoService is unavailable.");

            return baseUrl;
        }

        string Claim(string type)
            => user.FindFirst(type)?.Value ?? "";

        var fields = new (string Key, string Value)[]
        {
            ("EmployeeId",    Claim("emplid")),
            ("EmployeeName",  Claim("employee_name")),
            ("Designation",   Claim("employee_designation")),
            ("Privilege",     Claim("privilege").ToUpper()),
            ("BranchSolId",   Claim("branch_solid")),
            ("BranchName",    Claim("branch_name")),
            ("RegionSolId",   Claim("region_solid")),
            ("RegionName",    Claim("region_name")),
            ("ZoneSolId",     Claim("zone_solid")),
            ("ZoneName",      Claim("zone_name"))
        };

        var payload = string.Join("&",
            fields.Select(f =>
                $"{f.Key}={Uri.EscapeDataString(f.Value)}"));

        try
        {
            var encryptedPayload = EncryptViaApi(payload);

            if (string.IsNullOrWhiteSpace(encryptedPayload))
                return baseUrl;

            var separator = baseUrl.Contains('?') ? "&" : "?";

            return $"{baseUrl}{separator}data={Uri.EscapeDataString(encryptedPayload)}";
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex,
                "[EoiSsoService] Executive Branch Visit launch failed.");

            return baseUrl;
        }
    }
    private string? EncryptViaApi(string plainText)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CryptoService");
            var payload = JsonSerializer.Serialize(new { text = plainText });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = client.PostAsync($"{_cryptoServiceUrl}/api/crypto/encrypt", content).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                AppLogger.LogWarning($"[EoiSsoService] CryptoService encrypt returned {response.StatusCode}");
                return null;
            }

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var result = JsonSerializer.Deserialize<CryptoApiResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result?.Success == true ? result.Result : null;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "[EoiSsoService] EncryptViaApi failed.");
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
