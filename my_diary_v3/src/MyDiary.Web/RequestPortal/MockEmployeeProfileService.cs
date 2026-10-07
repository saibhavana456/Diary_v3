using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace MyDiary.Web.RequestPortal;

/// <summary>
/// TODO(UAT integration): this is a MOCK data source for the Feedback popup's
/// auto-filled identity/org fields (PF number, Zone, Region, Branch, mobile
/// number). Per the user's explicit instruction (2026-08-19), it currently
/// returns deterministic sample values keyed by EmpCode rather than a real
/// lookup — "its available in the uat... currently you use sample data and
/// mark in the code that this needs to be brought from [the real source]".
///
/// Replace this class's body with a real lookup once the UAT source is
/// identified (likely an HR feed or an org-hierarchy table analogous to
/// RP_CSBE_ORG_EMPLOYEE, which today only carries EmpCode/Name/Designation/
/// Scale/RoleName/SolId/SolName — no PF number, Zone, Region, or mobile
/// number columns, so it is NOT a drop-in source as-is). Keep the interface
/// (IEmployeeProfileService) the same so FeedbackHomeCard.razor and the MIS
/// Feedback pages don't need to change.
/// </summary>
public sealed class MockEmployeeProfileService : IEmployeeProfileService
{
    public Task<EmployeeProfile> GetProfileAsync(string empCode, CancellationToken ct = default)
    {
        // Deterministic per EmpCode so the same user always sees the same sample
        // values across sessions (not random each load) — makes it obviously fake
        // to a tester ("SAMPLE-...") while behaving consistently.
        var zones = new[] { "Mumbai Zone", "Delhi Zone", "Chennai Zone", "Kolkata Zone", "Bengaluru Zone" };
        var regions = new[] { "Mumbai Central", "Mumbai South", "Thane", "Pune" };
        var branches = new[] { "Fort Branch", "Andheri Branch", "Bandra Branch", "Vashi Branch" };

        var seed = Math.Abs((empCode ?? "").GetHashCode());
        var profile = new EmployeeProfile
        {
            EmpCode = empCode ?? "",
            Name = "Test User",                      // TODO: real name from HR source
            PfNumber = $"SAMPLE-PF-{seed % 900000 + 100000}",
            Zone = zones[seed % zones.Length],
            Region = regions[seed % regions.Length],
            Branch = branches[seed % branches.Length],
            MobileNumber = $"98{seed % 100000000:D8}",
        };
        return Task.FromResult(profile);
    }
}
