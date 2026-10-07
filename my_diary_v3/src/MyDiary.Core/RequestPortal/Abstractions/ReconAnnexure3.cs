using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

public interface IReconAnnexure3Repo
{
    /// <summary>Exact port of Annexure3.razor's existingEntry query.</summary>
    Task<bool> ExistsAsync(string vertical, string reportMonth, string reportYear, CancellationToken ct = default);
    Task<ReconAnnexure3?> GetAsync(string vertical, string reportMonth, string reportYear, CancellationToken ct = default);
    Task InsertAsync(ReconAnnexure3 entry, CancellationToken ct = default);
}

/// <summary>Exact port of Annexure3.razor's HandleValidSubmit — one submission per Vertical/month/year,
/// STATUS always '100', no draft, no approval chain. The only addition relative to source is
/// RequireVerticalRoleAsync (server-side RBAC via RoleCode.ReconVerticalUser/ReconAdmin, or App/Super
/// Admin) — source had no server-side role check at all.</summary>
public interface IReconAnnexure3Service
{
    Task<bool> IsAlreadySubmittedAsync(string actorEmpCode, string vertical, string reportMonth, string reportYear, CancellationToken ct = default);
    Task<ReconAnnexure3?> GetSubmittedAsync(string actorEmpCode, string vertical, string reportMonth, string reportYear, CancellationToken ct = default);
    Task SubmitAsync(string actorEmpCode, ReconAnnexure3 entry, CancellationToken ct = default);
}
