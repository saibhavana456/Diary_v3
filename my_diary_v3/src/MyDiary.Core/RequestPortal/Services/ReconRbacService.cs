using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

/// <summary>Mirrors DmdRbacService exactly, for the Recon Certificates workflow's own RBAC tier. Kept
/// fully separate from RP_RBAC_DMD_*/RP_RBAC_CCP_* — Recon's admin has no authority over those verticals,
/// and vice versa.</summary>
public sealed class ReconRbacService : IReconRbacService
{
    private readonly IReconRbacRepo _repo;
    private readonly IRbacRepo _rbac;

    public ReconRbacService(IReconRbacRepo repo, IRbacRepo rbac)
    {
        _repo = repo;
        _rbac = rbac;
    }

    public async Task<bool> IsReconAdminAsync(string empCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return false;
        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return true;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        if (appAdmin is { IsActive: true }) return true;
        return await _repo.IsAdminAsync(empCode, ct);
    }

    public async Task<bool> IsReconVerticalUserAsync(string empCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return false;
        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return true;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        if (appAdmin is { IsActive: true }) return true;
        var a = await _repo.GetActiveVerticalUserAsync(empCode, ct);
        return a is not null;
    }

    public Task<IReadOnlyList<ReconAdmin>> ListAdminsAsync(CancellationToken ct = default) =>
        _repo.ListAdminsAsync(ct);

    public async Task GrantAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        await _repo.UpsertAdminAsync(new ReconAdmin
        {
            EmpCode = targetEmpCode, IsActive = true, AssignedByEmp = actorEmpCode, AssignedAtUtc = DateTime.Now
        }, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.GrantCcpVerticalAdmin, Target = $"emp:{targetEmpCode}",
            Details = "Recon admin grant"
        }, ct);
    }

    public async Task RevokeAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        await _repo.UpsertAdminAsync(new ReconAdmin
        {
            EmpCode = targetEmpCode, IsActive = false, AssignedByEmp = actorEmpCode, AssignedAtUtc = DateTime.Now
        }, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.RevokeCcpVerticalAdmin, Target = $"emp:{targetEmpCode}",
            Details = "Recon admin revoke"
        }, ct);
    }

    public Task<IReadOnlyList<ReconVerticalUserAssignment>> ListVerticalUserAssignmentsAsync(CancellationToken ct = default) =>
        _repo.ListVerticalUserAssignmentsAsync(ct);

    public async Task AssignVerticalUserAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default)
    {
        await RequireReconAdminAsync(actorEmpCode, ct);
        await _repo.UpsertVerticalUserAssignmentAsync(new ReconVerticalUserAssignment
        {
            EmpCode = targetEmpCode, IsActive = true, AssignedByEmp = actorEmpCode, AssignedAtUtc = DateTime.Now
        }, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.AssignCcpRole,
            Target = $"emp:{targetEmpCode}", Details = "ReconVerticalUser"
        }, ct);
    }

    public async Task RevokeVerticalUserAsync(string actorEmpCode, long assignmentId, CancellationToken ct = default)
    {
        await RequireReconAdminAsync(actorEmpCode, ct);
        await _repo.DeactivateVerticalUserAssignmentAsync(assignmentId, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.RevokeCcpRole, Target = $"assignment:{assignmentId}",
            Details = "ReconVerticalUser"
        }, ct);
    }

    public async Task<bool> CanManageVerticalUsersAsync(string empCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return false;
        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return true;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        if (appAdmin is { IsActive: true }) return true;
        return await _repo.IsAdminAsync(empCode, ct);
    }

    public async Task<bool> CanManageAdminsAsync(string empCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return false;
        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return true;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        return appAdmin is { IsActive: true };
    }

    private async Task RequireAppAdminAsync(string empCode, CancellationToken ct)
    {
        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return;
        var a = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        if (a is null || !a.IsActive)
            throw new UnauthorizedAccessException("Granting a Recon Admin is restricted to Application/Super Admins.");
    }

    private async Task RequireReconAdminAsync(string empCode, CancellationToken ct)
    {
        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        if (appAdmin is { IsActive: true }) return;
        if (await _repo.IsAdminAsync(empCode, ct)) return;
        throw new UnauthorizedAccessException("Assigning the Recon Vertical User role is restricted to the Recon Admin (or App/Super Admin).");
    }
}
