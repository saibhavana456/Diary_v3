using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

/// <summary>Mirrors CcpRbacService exactly, but for the DMD Approver desk role used by the Back-Dated
/// Deposit Approval workflow. Kept fully separate from CcpRbacService/RP_RBAC_CCP_* — this is a different
/// department's vertical and must not share admin rights with CCP's.</summary>
public sealed class DmdRbacService : IDmdRbacService
{
    private readonly IDmdRbacRepo _repo;
    private readonly IRbacRepo _rbac;

    public DmdRbacService(IDmdRbacRepo repo, IRbacRepo rbac)
    {
        _repo = repo;
        _rbac = rbac;
    }

    public async Task<bool> IsDmdApproverAsync(string empCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return false;
        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return true;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        if (appAdmin is { IsActive: true }) return true;
        var a = await _repo.GetActiveApproverAsync(empCode, ct);
        return a is not null;
    }

    public Task<IReadOnlyList<DmdVerticalAdmin>> ListVerticalAdminsAsync(CancellationToken ct = default) =>
        _repo.ListVerticalAdminsAsync(ct);

    public async Task GrantVerticalAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        await _repo.UpsertVerticalAdminAsync(new DmdVerticalAdmin
        {
            EmpCode = targetEmpCode, IsActive = true, AssignedByEmp = actorEmpCode, AssignedAtUtc = DateTime.Now
        }, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.GrantCcpVerticalAdmin, Target = $"emp:{targetEmpCode}",
            Details = "DMD vertical admin grant"
        }, ct);
    }

    public async Task RevokeVerticalAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        await _repo.UpsertVerticalAdminAsync(new DmdVerticalAdmin
        {
            EmpCode = targetEmpCode, IsActive = false, AssignedByEmp = actorEmpCode, AssignedAtUtc = DateTime.Now
        }, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.RevokeCcpVerticalAdmin, Target = $"emp:{targetEmpCode}",
            Details = "DMD vertical admin revoke"
        }, ct);
    }

    public Task<IReadOnlyList<DmdApproverAssignment>> ListApproverAssignmentsAsync(CancellationToken ct = default) =>
        _repo.ListApproverAssignmentsAsync(ct);

    public async Task AssignApproverAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default)
    {
        await RequireDmdVerticalAdminAsync(actorEmpCode, ct);
        await _repo.UpsertApproverAssignmentAsync(new DmdApproverAssignment
        {
            EmpCode = targetEmpCode, IsActive = true, AssignedByEmp = actorEmpCode, AssignedAtUtc = DateTime.Now
        }, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.AssignCcpRole,
            Target = $"emp:{targetEmpCode}", Details = "DmdApprover"
        }, ct);
    }

    public async Task RevokeApproverAsync(string actorEmpCode, long assignmentId, CancellationToken ct = default)
    {
        await RequireDmdVerticalAdminAsync(actorEmpCode, ct);
        await _repo.DeactivateApproverAssignmentAsync(assignmentId, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.RevokeCcpRole, Target = $"assignment:{assignmentId}",
            Details = "DmdApprover"
        }, ct);
    }

    public async Task<bool> CanManageApproversAsync(string empCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return false;
        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return true;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        if (appAdmin is { IsActive: true }) return true;
        return await _repo.IsVerticalAdminAsync(empCode, ct);
    }

    public async Task<bool> CanManageVerticalAdminsAsync(string empCode, CancellationToken ct = default)
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
            throw new UnauthorizedAccessException("Granting a DMD Vertical Admin is restricted to Application/Super Admins.");
    }

    private async Task RequireDmdVerticalAdminAsync(string empCode, CancellationToken ct)
    {
        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        if (appAdmin is { IsActive: true }) return;
        if (await _repo.IsVerticalAdminAsync(empCode, ct)) return;
        throw new UnauthorizedAccessException("Assigning the DMD Approver role is restricted to the DMD Vertical Admin (or App/Super Admin).");
    }
}
