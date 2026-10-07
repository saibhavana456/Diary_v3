using MyDiary.Core.Services;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

public sealed class CcpRbacService : ICcpRbacService
{
    private readonly ICcpRbacRepo _repo;
    private readonly IUserRepo _users;
    private readonly IRbacRepo _rbac;

    public CcpRbacService(ICcpRbacRepo repo, IUserRepo users, IRbacRepo rbac)
    {
        _repo = repo;
        _users = users;
        _rbac = rbac;
    }

    /// <summary>Priority: Super/App Admin > explicit CoLcv/CoMcv assignment > Tier-1 organisational role
    /// (Branch/Zone). A CO employee (RoleCode.CoUser/CoHead) with no explicit CoLcv/CoMcv assignment, or
    /// an RO employee, gets "" — no CCP access — rather than a silent BranchMaker default (the bug this
    /// whole resolver replaces; see ICurrentUserService.cs on the CCP side).</summary>
    public async Task<string> ResolveEoiRoleAsync(string empCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return "";

        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return "Admin";
        var appAdmin = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        if (appAdmin is { IsActive: true }) return "Admin";

        var coAssignment = await _repo.GetActiveRoleAssignmentAsync(empCode, ct);
        if (coAssignment is not null &&
            Enum.TryParse<RoleCode>(coAssignment.RoleCode, out var coRole) &&
            coRole is RoleCode.CoLcv or RoleCode.CoMcv)
        {
            return coRole.ToString();
        }

        var tier1 = await _users.GetEffectiveRolesAsync(empCode, ct);
        if (tier1.Contains(RoleCode.BranchHead)) return "BranchHead";
        if (tier1.Contains(RoleCode.BranchUser)) return "BranchMaker";
        if (tier1.Contains(RoleCode.ZoHead) || tier1.Contains(RoleCode.ZoUser)) return "ZonalHead";

        // RO users and CO users without an explicit LCV/MCV grant fall through to "" — no CCP access.
        return "";
    }

    public Task<IReadOnlyList<CcpVerticalAdmin>> ListVerticalAdminsAsync(CancellationToken ct = default) =>
        _repo.ListVerticalAdminsAsync(ct);

    public async Task GrantVerticalAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        await _repo.UpsertVerticalAdminAsync(new CcpVerticalAdmin
        {
            EmpCode = targetEmpCode, IsActive = true, AssignedByEmp = actorEmpCode, AssignedAtUtc = AppTime.Now
        }, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.GrantCcpVerticalAdmin, Target = $"emp:{targetEmpCode}"
        }, ct);
    }

    public async Task RevokeVerticalAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        await _repo.UpsertVerticalAdminAsync(new CcpVerticalAdmin
        {
            EmpCode = targetEmpCode, IsActive = false, AssignedByEmp = actorEmpCode, AssignedAtUtc = AppTime.Now
        }, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.RevokeCcpVerticalAdmin, Target = $"emp:{targetEmpCode}"
        }, ct);
    }

    public Task<IReadOnlyList<CcpRoleAssignment>> ListRoleAssignmentsAsync(CancellationToken ct = default) =>
        _repo.ListRoleAssignmentsAsync(ct);

    public async Task AssignRoleAsync(string actorEmpCode, string targetEmpCode, RoleCode role, CancellationToken ct = default)
    {
        if (role is not (RoleCode.CoLcv or RoleCode.CoMcv))
            throw new ArgumentException("Only CoLcv or CoMcv can be assigned here.", nameof(role));

        await RequireCcpVerticalAdminAsync(actorEmpCode, ct);
        await _repo.UpsertRoleAssignmentAsync(new CcpRoleAssignment
        {
            EmpCode = targetEmpCode, RoleCode = role.ToString(),
            IsActive = true, AssignedByEmp = actorEmpCode, AssignedAtUtc = AppTime.Now
        }, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.AssignCcpRole,
            Target = $"emp:{targetEmpCode}", Details = role.ToString()
        }, ct);
    }

    public async Task RevokeRoleAsync(string actorEmpCode, long assignmentId, CancellationToken ct = default)
    {
        await RequireCcpVerticalAdminAsync(actorEmpCode, ct);
        await _repo.DeactivateRoleAssignmentAsync(assignmentId, ct);
        await _rbac.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.RevokeCcpRole, Target = $"assignment:{assignmentId}"
        }, ct);
    }

    public async Task<bool> CanManageRolesAsync(string empCode, CancellationToken ct = default)
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
            throw new UnauthorizedAccessException("Granting a CCP Vertical Admin is restricted to Application/Super Admins.");
    }

    private async Task RequireCcpVerticalAdminAsync(string empCode, CancellationToken ct)
    {
        if (await _rbac.IsSuperAdminAsync(empCode, ct)) return;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(empCode, ct);
        if (appAdmin is { IsActive: true }) return;
        if (await _repo.IsVerticalAdminAsync(empCode, ct)) return;
        throw new UnauthorizedAccessException("Assigning CCP roles is restricted to the CCP Vertical Admin (or App/Super Admin).");
    }
}
