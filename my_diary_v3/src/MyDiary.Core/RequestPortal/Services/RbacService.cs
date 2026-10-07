using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

public sealed class RbacService : IRbacService
{
    private readonly IRbacRepo _repo;
    private readonly IUserRepo _users;

    public RbacService(IRbacRepo repo, IUserRepo users) { _repo = repo; _users = users; }

    public async Task<IReadOnlyList<EffectivePermission>> GetEffectiveAsync(string empCode, CancellationToken ct = default)
    {
        var modules = (await _repo.ListModulesAsync(ct)).Where(m => m.IsActive).ToList();
        var userRoleCodes = await _users.GetEffectiveRolesAsync(empCode, ct);
        var allRoles = await _users.ListRolesAsync(ct);
        var roleIds = allRoles.Where(r => userRoleCodes.Contains(r.Code)).Select(r => r.Id).ToHashSet();
        var rolePerms = await _repo.ListRolePermsAsync(ct: ct);
        var userOver  = await _repo.ListUserOverridesAsync(empCode, ct);
        var isSuper   = await _repo.IsSuperAdminAsync(empCode, ct);

        var result = new List<EffectivePermission>();
        foreach (var m in modules)
        {
            var grant = new bool[5];
            var deny  = new bool[5];
            if (isSuper) grant[0] = true;

            foreach (var rp in rolePerms.Where(p => p.ModuleId == m.Id && roleIds.Contains(p.RoleId)))
            {
                if (rp.CanView)      grant[0] = true;
                if (rp.CanAdd)       grant[1] = true;
                if (rp.CanModify)    grant[2] = true;
                if (rp.CanDelete)    grant[3] = true;
                if (rp.CanAuthorize) grant[4] = true;
            }

            foreach (var uo in userOver.Where(p => p.ModuleId == m.Id))
            {
                var bucket = uo.IsDeny ? deny : grant;
                if (uo.CanView)      bucket[0] = true;
                if (uo.CanAdd)       bucket[1] = true;
                if (uo.CanModify)    bucket[2] = true;
                if (uo.CanDelete)    bucket[3] = true;
                if (uo.CanAuthorize) bucket[4] = true;
            }

            var final = new bool[5];
            for (int i = 0; i < 5; i++) final[i] = grant[i] && !deny[i];
            if (m.Kind != ModuleKind.Transactional)
                final[1] = final[2] = final[3] = final[4] = false;

            if (final.Any(f => f))
                result.Add(new EffectivePermission(m.Id, m.Code, m.Kind, final[0], final[1], final[2], final[3], final[4]));
        }
        return result;
    }

    public async Task<bool> CheckAsync(string empCode, string moduleCode, PermAction action, CancellationToken ct = default)
    {
        var eff = await GetEffectiveAsync(empCode, ct);
        var row = eff.FirstOrDefault(e => string.Equals(e.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase));
        if (row is null) return false;
        return action switch
        {
            PermAction.View      => row.CanView,
            PermAction.Add       => row.CanAdd,
            PermAction.Modify    => row.CanModify,
            PermAction.Delete    => row.CanDelete,
            PermAction.Authorize => row.CanAuthorize,
            _                    => false
        };
    }

    public Task<bool> IsSuperAdminAsync(string empCode, CancellationToken ct = default) => _repo.IsSuperAdminAsync(empCode, ct);

    public async Task<bool> IsAppAdminAsync(string empCode, CancellationToken ct = default)
    {
        if (await _repo.IsSuperAdminAsync(empCode, ct)) return true;
        var row = await _repo.GetActiveAppAdminAsync(empCode, ct);
        return row is { IsActive: true };
    }

    public async Task GrantAppAdminAsync(string actorEmpCode, string targetEmpCode, string? note, CancellationToken ct = default)
    {
        await RequireSuperAdminAsync(actorEmpCode, ct);
        var existing = await _repo.GetActiveAppAdminAsync(targetEmpCode, ct);
        if (existing is { IsActive: true }) return;

        await _repo.UpsertAppAdminAsync(new AppAdmin
        {
            EmpCode = targetEmpCode, AssignedAtUtc = DateTime.UtcNow,
            AssignedByEmp = actorEmpCode, IsActive = true, Note = note
        }, ct);

        await _repo.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode, Action = RbacAuditAction.GrantAppAdmin,
            Target = $"emp:{targetEmpCode}", Details = note
        }, ct);
    }

    public async Task RevokeAppAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default)
    {
        await RequireSuperAdminAsync(actorEmpCode, ct);
        var existing = await _repo.GetActiveAppAdminAsync(targetEmpCode, ct);
        if (existing is null) return;
        existing.IsActive = false;
        await _repo.UpsertAppAdminAsync(existing, ct);
        await _repo.AppendAuditAsync(new RbacAudit { ActorEmpCode = actorEmpCode, Action = RbacAuditAction.RevokeAppAdmin, Target = $"emp:{targetEmpCode}" }, ct);
    }

    public async Task<bool> IsFeedbackAdminAsync(string empCode, CancellationToken ct = default)
    {
        if (await _repo.IsSuperAdminAsync(empCode, ct)) return true;
        var row = await _repo.GetActiveFeedbackAdminAsync(empCode, ct);
        return row is { IsActive: true };
    }

    public async Task GrantFeedbackAdminAsync(string actorEmpCode, string targetEmpCode, string? note, CancellationToken ct = default)
    {
        await RequireSuperAdminAsync(actorEmpCode, ct);
        var existing = await _repo.GetActiveFeedbackAdminAsync(targetEmpCode, ct);
        if (existing is { IsActive: true }) return;

        await _repo.UpsertFeedbackAdminAsync(new FeedbackAdmin
        {
            EmpCode = targetEmpCode,
            AssignedAtUtc = DateTime.UtcNow,
            AssignedByEmp = actorEmpCode,
            IsActive = true,
            Note = note
        }, ct);

        await _repo.AppendAuditAsync(new RbacAudit
        {
            ActorEmpCode = actorEmpCode,
            Action = RbacAuditAction.GrantFeedbackAdmin,
            Target = $"emp:{targetEmpCode}",
            Details = note
        }, ct);
    }

    public async Task RevokeFeedbackAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default)
    {
        await RequireSuperAdminAsync(actorEmpCode, ct);
        var existing = await _repo.GetActiveFeedbackAdminAsync(targetEmpCode, ct);
        if (existing is null) return;
        existing.IsActive = false;
        await _repo.UpsertFeedbackAdminAsync(existing, ct);
        await _repo.AppendAuditAsync(new RbacAudit { ActorEmpCode = actorEmpCode, Action = RbacAuditAction.RevokeFeedbackAdmin, Target = $"emp:{targetEmpCode}" }, ct);
    }

    public async Task SetMenuEnabledAsync(string actorEmpCode, long menuId, bool enabled, CancellationToken ct = default)
    {
        await RequireSuperAdminAsync(actorEmpCode, ct);
        var menu = await _repo.GetMenuAsync(menuId, ct) ?? throw new KeyNotFoundException($"Menu {menuId} not found");
        if (menu.IsEnabled == enabled) return;
        menu.IsEnabled = enabled;
        await _repo.UpsertMenuAsync(menu, ct);
        await _repo.AppendAuditAsync(new RbacAudit { ActorEmpCode = actorEmpCode, Action = enabled ? RbacAuditAction.EnableMenu : RbacAuditAction.DisableMenu, Target = $"menu:{menu.Code}" }, ct);
    }

    public async Task UpsertModuleAsync(string actorEmpCode, AppModule module, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        var id = await _repo.UpsertModuleAsync(module, ct);
        await _repo.AppendAuditAsync(new RbacAudit { ActorEmpCode = actorEmpCode, Action = RbacAuditAction.UpsertModule, Target = $"module:{module.Code}", Details = $"id={id} kind={module.Kind} parent={module.ParentId}" }, ct);
    }

    public async Task DeleteModuleAsync(string actorEmpCode, long moduleId, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        var m = await _repo.GetModuleAsync(moduleId, ct) ?? throw new KeyNotFoundException($"Module {moduleId} not found");
        await _repo.DeleteModuleAsync(moduleId, ct);
        await _repo.AppendAuditAsync(new RbacAudit { ActorEmpCode = actorEmpCode, Action = RbacAuditAction.DeleteModule, Target = $"module:{m.Code}" }, ct);
    }

    public async Task UpsertMenuAsync(string actorEmpCode, AppMenu menu, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        await _repo.UpsertMenuAsync(menu, ct);
        await _repo.AppendAuditAsync(new RbacAudit { ActorEmpCode = actorEmpCode, Action = RbacAuditAction.UpsertMenu, Target = $"menu:{menu.Code}", Details = $"route={menu.Route} enabled={menu.IsEnabled}" }, ct);
    }

    public async Task DeleteMenuAsync(string actorEmpCode, long menuId, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        var m = await _repo.GetMenuAsync(menuId, ct) ?? throw new KeyNotFoundException($"Menu {menuId} not found");
        await _repo.DeleteMenuAsync(menuId, ct);
        await _repo.AppendAuditAsync(new RbacAudit { ActorEmpCode = actorEmpCode, Action = RbacAuditAction.DeleteMenu, Target = $"menu:{m.Code}" }, ct);
    }

    public async Task UpsertRolePermAsync(string actorEmpCode, RoleModulePerm perm, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        perm.UpdatedByEmp = actorEmpCode;
        perm.UpdatedAtUtc = DateTime.UtcNow;
        await _repo.UpsertRolePermAsync(perm, ct);
        await _repo.AppendAuditAsync(new RbacAudit { ActorEmpCode = actorEmpCode, Action = RbacAuditAction.UpsertRolePerm, Target = $"role:{perm.RoleId} module:{perm.ModuleId}", Details = Flags(perm.CanView, perm.CanAdd, perm.CanModify, perm.CanDelete, perm.CanAuthorize) }, ct);
    }

    public async Task UpsertUserOverrideAsync(string actorEmpCode, UserModulePerm perm, CancellationToken ct = default)
    {
        await RequireAppAdminAsync(actorEmpCode, ct);
        perm.UpdatedByEmp = actorEmpCode;
        perm.UpdatedAtUtc = DateTime.UtcNow;
        await _repo.UpsertUserOverrideAsync(perm, ct);
        await _repo.AppendAuditAsync(new RbacAudit { ActorEmpCode = actorEmpCode, Action = RbacAuditAction.UpsertUserOverride, Target = $"emp:{perm.EmpCode} module:{perm.ModuleId}", Details = $"deny={perm.IsDeny} " + Flags(perm.CanView, perm.CanAdd, perm.CanModify, perm.CanDelete, perm.CanAuthorize) }, ct);
    }

    private async Task RequireSuperAdminAsync(string empCode, CancellationToken ct)
    {
        if (!await _repo.IsSuperAdminAsync(empCode, ct))
            throw new UnauthorizedAccessException("This action is restricted to SuperAdmin users.");
    }

    private async Task RequireAppAdminAsync(string empCode, CancellationToken ct)
    {
        if (await _repo.IsSuperAdminAsync(empCode, ct)) return;
        var a = await _repo.GetActiveAppAdminAsync(empCode, ct);
        if (a is null || !a.IsActive)
            throw new UnauthorizedAccessException("This action is restricted to Application Admins.");
    }

    private static string Flags(bool v, bool a, bool m, bool d, bool z) => $"V={v},A={a},M={m},D={d},Auth={z}";
}
