using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

/// <summary>Byte-for-byte the same shape as IDmdRbacRepo, over two fully separate tables
/// (RP_RBAC_RECON_ADMIN, RP_RBAC_RECON_ROLE) — see db/recon/001_recon_rbac.sql.</summary>
public interface IReconRbacRepo
{
    Task<IReadOnlyList<ReconAdmin>> ListAdminsAsync(CancellationToken ct = default);
    Task<bool> IsAdminAsync(string empCode, CancellationToken ct = default);
    Task UpsertAdminAsync(ReconAdmin a, CancellationToken ct = default);

    Task<IReadOnlyList<ReconVerticalUserAssignment>> ListVerticalUserAssignmentsAsync(CancellationToken ct = default);
    Task<ReconVerticalUserAssignment?> GetActiveVerticalUserAsync(string empCode, CancellationToken ct = default);
    Task<long> UpsertVerticalUserAssignmentAsync(ReconVerticalUserAssignment a, CancellationToken ct = default);
    Task DeactivateVerticalUserAssignmentAsync(long id, CancellationToken ct = default);
}

/// <summary>Mirrors IDmdRbacService exactly, for the Recon Certificates workflow's own RBAC tier.
/// ReconAdmin is granted only by App/Super Admin; ReconAdmin (or App/Super Admin) then assigns/revokes
/// the ReconVerticalUser (CO/Vertical desk) role. Branch/RO/ZO reuse the existing organisational roles
/// directly — nothing Recon-specific is needed for those levels.</summary>
public interface IReconRbacService
{
    Task<bool> IsReconVerticalUserAsync(string empCode, CancellationToken ct = default);
    Task<bool> IsReconAdminAsync(string empCode, CancellationToken ct = default);

    Task<IReadOnlyList<ReconAdmin>> ListAdminsAsync(CancellationToken ct = default);
    Task GrantAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default);
    Task RevokeAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default);

    Task<IReadOnlyList<ReconVerticalUserAssignment>> ListVerticalUserAssignmentsAsync(CancellationToken ct = default);
    Task AssignVerticalUserAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default);
    Task RevokeVerticalUserAsync(string actorEmpCode, long assignmentId, CancellationToken ct = default);

    Task<bool> CanManageVerticalUsersAsync(string empCode, CancellationToken ct = default);
    Task<bool> CanManageAdminsAsync(string empCode, CancellationToken ct = default);
}
