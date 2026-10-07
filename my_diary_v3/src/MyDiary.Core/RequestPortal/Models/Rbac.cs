namespace RequestPortal.Core.Models;

// ---------------------------------------------------------------------------
// Module tree — top-level Modules contain Sub-modules (recursive via ParentId).
// A leaf module is what menus + permissions actually map to.
// ---------------------------------------------------------------------------

public sealed class AppModule
{
    public long Id { get; set; }
    public string Code { get; set; } = "";        // unique business code (e.g. "MASTERS.USERS")
    public string Name { get; set; } = "";
    public ModuleKind Kind { get; set; } = ModuleKind.Transactional;
    public long? ParentId { get; set; }            // null = top-level module
    public string? Icon { get; set; }              // MudBlazor icon name
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

// FeedbackAdmin — EmpCode-keyed, granted by SuperAdmin only. Grants access to the
// Feedback Dashboard / Feedback ticket list (SuperAdmin already has access via
// AuthorizationPolicies.FeedbackAdmin without needing a row here).
public sealed class FeedbackAdmin
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; }
    public string AssignedByEmp { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
}

// ---------------------------------------------------------------------------
// Menu entries — actual UI navigation rows. Always live UNDER a leaf module.
// SuperAdmin can globally enable/disable a menu (IsEnabled); ordinary admins
// can only map roles to it, not toggle its visibility entirely.
// ---------------------------------------------------------------------------

public sealed class AppMenu
{
    public long Id { get; set; }
    public long ModuleId { get; set; }              // leaf module (sub-module)
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Icon { get; set; }
    public string Route { get; set; } = "";         // e.g. "requests/mine"
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;     // SuperAdmin toggle
    public bool IsActive { get; set; } = true;      // soft-delete
}

// ---------------------------------------------------------------------------
// (Role, Module) permission grant. Boolean flags for each action.
// Non-transactional modules ignore the Add/Modify/Delete/Authorize flags;
// only CanView matters for them.
// ---------------------------------------------------------------------------

public sealed class RoleModulePerm
{
    public long Id { get; set; }
    public long RoleId { get; set; }
    public long ModuleId { get; set; }
    public bool CanView { get; set; }
    public bool CanAdd { get; set; }
    public bool CanModify { get; set; }
    public bool CanDelete { get; set; }
    public bool CanAuthorize { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByEmp { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Per-user fine-grained override. IsDeny = true means EXPLICITLY remove that
// permission even if a role grants it. Otherwise it adds permissions on top
// of the user's role grants.
// ---------------------------------------------------------------------------

// Per-user fine-grained override. EmpCode replaces UserId — identity from HRMS view.
public sealed class UserModulePerm
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public long ModuleId { get; set; }
    public bool IsDeny { get; set; }
    public bool CanView { get; set; }
    public bool CanAdd { get; set; }
    public bool CanModify { get; set; }
    public bool CanDelete { get; set; }
    public bool CanAuthorize { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByEmp { get; set; } = "";
}

// AppAdmin — EmpCode-keyed, granted by SuperAdmin only.
public sealed class AppAdmin
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; }
    public string AssignedByEmp { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
}

// SuperAdmin — EmpCode-keyed, max 2, locked in code (RbacConstants.MaxSuperAdmins).
public sealed class SuperAdmin
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; }
    public string AssignedBy { get; set; } = "BOOT";
}

// Vertical Admin assignment (per vertical, assigned by App/Super Admin).
public sealed class VerticalAdminAssignment
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public string VerticalCode { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string AssignedByEmp { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}

// Vertical process level (VERT_L1..L5) assignment (per vertical, by CO_VERT_ADMIN).
public sealed class VerticalLevelAssignment
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public string VerticalCode { get; set; } = "";
    public string LevelCode { get; set; } = "";  // "VertL1" .. "VertL5"
    public bool IsActive { get; set; } = true;
    public string AssignedByEmp { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}

// Tamper-resistant audit log for every RBAC mutation.
public sealed class RbacAudit
{
    public long Id { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string ActorEmpCode { get; set; } = "";
    public RbacAuditAction Action { get; set; }
    public string Target { get; set; } = "";
    public string? Details { get; set; }
}

public static class RbacConstants
{
    public const int MaxSuperAdmins = 2;
}

/// <summary>Delegated admin for the Corporate Credit Portal (CCP) vertical only — kept as its own table
/// rather than reusing RP_RBAC_VERTICAL_ADMIN, since that table's existing read side (UserRepo.
/// GetEffectiveRolesAsync) grants the shared CoVertAdmin role globally with no VERTICAL_CODE filter; a
/// dedicated table avoids a CCP admin grant leaking CoVertAdmin into unrelated Request Portal verticals
/// (or vice versa) until that pre-existing scoping gap is fixed properly.</summary>
public sealed class CcpVerticalAdmin
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string AssignedByEmp { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>One employee's current CCP desk assignment — RoleCode.CoLcv or RoleCode.CoMcv (stored as its
/// enum name). A new assignment deactivates any prior active row for that employee (application-enforced
/// in CcpRbacRepo, not a DB constraint — same pattern RP_RBAC_APP_ADMIN's MERGE upsert uses).</summary>
public sealed class CcpRoleAssignment
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string AssignedByEmp { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}
