using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

public sealed record EffectivePermission(
    long ModuleId,
    string ModuleCode,
    ModuleKind Kind,
    bool CanView,
    bool CanAdd,
    bool CanModify,
    bool CanDelete,
    bool CanAuthorize);

public interface IRbacRepo
{
    // Modules + Menus
    Task<IReadOnlyList<AppModule>> ListModulesAsync(CancellationToken ct = default);
    Task<AppModule?> GetModuleAsync(long id, CancellationToken ct = default);
    Task<long> UpsertModuleAsync(AppModule m, CancellationToken ct = default);
    Task DeleteModuleAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<AppMenu>> ListMenusAsync(CancellationToken ct = default);
    Task<AppMenu?> GetMenuAsync(long id, CancellationToken ct = default);
    Task<long> UpsertMenuAsync(AppMenu menu, CancellationToken ct = default);
    Task DeleteMenuAsync(long id, CancellationToken ct = default);

    // Permissions
    Task<IReadOnlyList<RoleModulePerm>> ListRolePermsAsync(long? roleId = null, CancellationToken ct = default);
    Task UpsertRolePermAsync(RoleModulePerm p, CancellationToken ct = default);
    Task DeleteRolePermAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<UserModulePerm>> ListUserOverridesAsync(string? empCode = null, CancellationToken ct = default);
    Task UpsertUserOverrideAsync(UserModulePerm p, CancellationToken ct = default);
    Task DeleteUserOverrideAsync(long id, CancellationToken ct = default);

    // Admin / SuperAdmin assignments
    Task<IReadOnlyList<AppAdmin>> ListAppAdminsAsync(CancellationToken ct = default);
    Task<AppAdmin?> GetActiveAppAdminAsync(string empCode, CancellationToken ct = default);
    Task<long> UpsertAppAdminAsync(AppAdmin a, CancellationToken ct = default);

    Task<IReadOnlyList<SuperAdmin>> ListSuperAdminsAsync(CancellationToken ct = default);
    Task<bool> IsSuperAdminAsync(string empCode, CancellationToken ct = default);

    // FeedbackAdmin assignments � grants access to the Feedback Dashboard / list
    Task<IReadOnlyList<FeedbackAdmin>> ListFeedbackAdminsAsync(CancellationToken ct = default);
    Task<FeedbackAdmin?> GetActiveFeedbackAdminAsync(string empCode, CancellationToken ct = default);
    Task<long> UpsertFeedbackAdminAsync(FeedbackAdmin a, CancellationToken ct = default);

    // Audit
    Task AppendAuditAsync(RbacAudit row, CancellationToken ct = default);
    Task<IReadOnlyList<RbacAudit>> ListAuditAsync(int take = 200, CancellationToken ct = default);
}

/// <summary>RBAC for the Corporate Credit Portal (CCP) vertical only — deliberately separate from
/// IRbacRepo/IRbacService (which cover generic module/menu CRUD permissions). CCP's roles are
/// business-process roles (BranchMaker/BranchHead/ZonalHead/CoLcv/CoMcv), not module CanView/CanAdd/...
/// flags, so they don't fit that shape. BranchMaker/BranchHead/ZonalHead are never stored here — they're
/// derived automatically from IUserRepo.GetEffectiveRolesAsync's Tier-1 organisational roles
/// (BranchUser/BranchHead/ZoUser/ZoHead). Only CoLcv/CoMcv need explicit assignment, since HRMS has no
/// LCV/MCV distinction — see ICcpRoleResolver for how the two are combined into one final role.</summary>
public interface ICcpRbacRepo
{
    Task<IReadOnlyList<CcpVerticalAdmin>> ListVerticalAdminsAsync(CancellationToken ct = default);
    Task<bool> IsVerticalAdminAsync(string empCode, CancellationToken ct = default);
    Task UpsertVerticalAdminAsync(CcpVerticalAdmin a, CancellationToken ct = default);

    /// <summary>Active CoLcv/CoMcv assignments. One employee should have at most one active row.</summary>
    Task<IReadOnlyList<CcpRoleAssignment>> ListRoleAssignmentsAsync(CancellationToken ct = default);
    Task<CcpRoleAssignment?> GetActiveRoleAssignmentAsync(string empCode, CancellationToken ct = default);
    Task<long> UpsertRoleAssignmentAsync(CcpRoleAssignment a, CancellationToken ct = default);
    Task DeactivateRoleAssignmentAsync(long id, CancellationToken ct = default);
}

public interface ICcpRbacService
{
    /// <summary>Resolves the single effective CCP EoiRole string for an employee — "" if none applies
    /// (e.g. a CO employee with no explicit LCV/MCV assignment, or an RO employee, gets no CCP access
    /// rather than silently defaulting to BranchMaker). Combines IUserRepo.GetEffectiveRolesAsync's
    /// Tier-1/Admin roles with this vertical's explicit CoLcv/CoMcv assignment.</summary>
    Task<string> ResolveEoiRoleAsync(string empCode, CancellationToken ct = default);

    Task<IReadOnlyList<CcpVerticalAdmin>> ListVerticalAdminsAsync(CancellationToken ct = default);
    Task GrantVerticalAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default);
    Task RevokeVerticalAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default);

    /// <summary>True for SuperAdmin, AppAdmin, or an active CCP Vertical Admin — i.e. anyone allowed to
    /// call AssignRoleAsync/RevokeRoleAsync. For UI visibility checks (the write methods enforce this
    /// themselves regardless).</summary>
    Task<bool> CanManageRolesAsync(string empCode, CancellationToken ct = default);

    /// <summary>True for SuperAdmin or AppAdmin only — i.e. anyone allowed to call
    /// GrantVerticalAdminAsync/RevokeVerticalAdminAsync.</summary>
    Task<bool> CanManageVerticalAdminsAsync(string empCode, CancellationToken ct = default);

    Task<IReadOnlyList<CcpRoleAssignment>> ListRoleAssignmentsAsync(CancellationToken ct = default);
    /// <summary>Assigns CoLcv or CoMcv to an employee, replacing any existing active CCP role assignment
    /// for them. Restricted to CCP Vertical Admins (or App/Super Admin).</summary>
    Task AssignRoleAsync(string actorEmpCode, string targetEmpCode, RoleCode role, CancellationToken ct = default);
    Task RevokeRoleAsync(string actorEmpCode, long assignmentId, CancellationToken ct = default);
}

public interface IRbacService
{
    // Read
    Task<IReadOnlyList<EffectivePermission>> GetEffectiveAsync(string empCode, CancellationToken ct = default);
    Task<bool> CheckAsync(string empCode, string moduleCode, PermAction action, CancellationToken ct = default);
    Task<bool> IsSuperAdminAsync(string empCode, CancellationToken ct = default);
    Task<bool> IsAppAdminAsync(string empCode, CancellationToken ct = default);

    // SuperAdmin actions
    Task GrantAppAdminAsync(string actorEmpCode, string targetEmpCode, string? note, CancellationToken ct = default);
    Task RevokeAppAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default);
    Task SetMenuEnabledAsync(string actorEmpCode, long menuId, bool enabled, CancellationToken ct = default);

    // Feedback actions 
    Task<bool> IsFeedbackAdminAsync(string empCode, CancellationToken ct = default);
    Task GrantFeedbackAdminAsync(string actorEmpCode, string targetEmpCode, string? note, CancellationToken ct = default);
    Task RevokeFeedbackAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default);

    // AppAdmin (or SuperAdmin) actions
    Task UpsertModuleAsync(string actorEmpCode, AppModule module, CancellationToken ct = default);
    Task DeleteModuleAsync(string actorEmpCode, long moduleId, CancellationToken ct = default);
    Task UpsertMenuAsync(string actorEmpCode, AppMenu menu, CancellationToken ct = default);
    Task DeleteMenuAsync(string actorEmpCode, long menuId, CancellationToken ct = default);
    Task UpsertRolePermAsync(string actorEmpCode, RoleModulePerm perm, CancellationToken ct = default);
    Task UpsertUserOverrideAsync(string actorEmpCode, UserModulePerm perm, CancellationToken ct = default);
}
