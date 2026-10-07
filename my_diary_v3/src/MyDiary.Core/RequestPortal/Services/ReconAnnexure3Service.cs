using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

/// <summary>See IReconAnnexure3Service's own doc comment — exact port of Annexure3.razor's
/// HandleValidSubmit, plus a new server-side RBAC gate.</summary>
public sealed class ReconAnnexure3Service : IReconAnnexure3Service
{
    private readonly IReconAnnexure3Repo _repo;
    private readonly IReconRbacService _reconRbac;
    private readonly IRbacRepo _rbac;

    public ReconAnnexure3Service(IReconAnnexure3Repo repo, IReconRbacService reconRbac, IRbacRepo rbac)
    {
        _repo = repo;
        _reconRbac = reconRbac;
        _rbac = rbac;
    }

    public Task<bool> IsAlreadySubmittedAsync(string actorEmpCode, string vertical, string reportMonth, string reportYear, CancellationToken ct = default) =>
        _repo.ExistsAsync(vertical, reportMonth, reportYear, ct);

    public Task<ReconAnnexure3?> GetSubmittedAsync(string actorEmpCode, string vertical, string reportMonth, string reportYear, CancellationToken ct = default) =>
        _repo.GetAsync(vertical, reportMonth, reportYear, ct);

    public async Task SubmitAsync(string actorEmpCode, ReconAnnexure3 entry, CancellationToken ct = default)
    {
        await RequireVerticalRoleAsync(actorEmpCode, ct);

        if (await _repo.ExistsAsync(entry.Vertical, entry.ReportMonth, entry.ReportYear, ct))
            throw new ReconValidationException($"Data has already been submitted for the month {entry.ReportYear} - {entry.ReportMonth}. Entry is not allowed.");

        entry.ReportId = DateTime.Now.ToString("yyMMddhhmmssfff");
        entry.Status = "100";
        entry.EntryBy = actorEmpCode;
        entry.EntryDate = DateTime.Now;
        entry.DelFlag = "N";

        await _repo.InsertAsync(entry, ct);
    }

    private async Task RequireVerticalRoleAsync(string actorEmpCode, CancellationToken ct)
    {
        if (await _rbac.IsSuperAdminAsync(actorEmpCode, ct)) return;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(actorEmpCode, ct);
        if (appAdmin is { IsActive: true }) return;
        if (await _reconRbac.IsReconVerticalUserAsync(actorEmpCode, ct)) return;

        throw new ReconAuthorizationException("This action is restricted to Recon Vertical (CO) users (or App/Super Admin).");
    }
}
