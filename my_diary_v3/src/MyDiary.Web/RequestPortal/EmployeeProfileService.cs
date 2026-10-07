using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace MyDiary.Web.RequestPortal;

/// <summary>
/// Real <see cref="IEmployeeProfileService"/> that populates the Feedback
/// submission page's identity/org fields from the authenticated user's claims —
/// the same <see cref="AuthenticationStateProvider"/>/CustomAuthState source that
/// <c>CurrentUser</c> and other components already read from. Replaces the old
/// MockEmployeeProfileService's sample data.
///
/// Field mapping (claims are set in CustomAuthenticationStateProvider.BuildClaimsAsync):
///   Name         ← "employee_name" (fallback ClaimTypes.Name)
///   Zone         ← "zone_name"
///   Region       ← "region_name"
///   Branch       ← "branch_name"
///   MobileNumber ← "mobile" (fallback "phone")
///
/// Persistence note: RP_FEEDBACK_TICKET stores ZONE/REGION/BRANCH as name text
/// (see FeedbackRepo.InsertTicketAsync), so the NAMES are passed through as-is —
/// no SOL-ID translation is required. (The SOL IDs branch_solid/zone_solid/
/// region_solid are also present in AuthState should a future ID-based schema
/// ever need them.)
///
/// </summary>
public sealed class EmployeeProfileService : IEmployeeProfileService
{
    private readonly AuthenticationStateProvider _authState;

    public EmployeeProfileService(AuthenticationStateProvider authState)
    {
        _authState = authState;
    }

    public async Task<EmployeeProfile> GetProfileAsync(string empCode, CancellationToken ct = default)
    {
        var state = await _authState.GetAuthenticationStateAsync();
        var user = state.User;

        string? Claim(params string[] types)
        {
            foreach (var t in types)
            {
                var v = user.FindFirst(t)?.Value;
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            return null;
        }

        return new EmployeeProfile
        {
            EmpCode = string.IsNullOrWhiteSpace(empCode)
                ? (Claim("emplid", ClaimTypes.NameIdentifier) ?? "")
                : empCode,
            Name = Claim("employee_name", ClaimTypes.Name) ?? "",
            PfNumber = string.IsNullOrWhiteSpace(empCode)
                ? (Claim("emplid", ClaimTypes.NameIdentifier) ?? "")
                : empCode, // EmpCode and PfNumber both are same.
            Zone = Claim("zone_name"),
            Region = Claim("region_name"),
            Branch = Claim("branch_name"),
            MobileNumber = Claim("mobile", "phone"),
        };
    }
}
