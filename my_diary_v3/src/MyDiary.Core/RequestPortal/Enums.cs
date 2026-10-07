namespace RequestPortal.Core;

// ── Organisational hierarchy ──────────────────────────────────────────────
public enum SolIdType
{
    Branch          = 1,
    RegionalOffice  = 2,
    ZonalOffice     = 3,
    CoVertical      = 4
}

public enum EmployeePosition
{
    Officer        = 1,  // Junior staff
    SeniorOfficer  = 2,  // Senior staff / deputy
    Head           = 3,  // Head of the Sol ID unit
    VerticalAdmin  = 4   // CO Vertical admin (special allocation)
}

// ── Portal module used for sidebar context switching ─────────────────────
public enum PortalModule
{
    MainApp          = 0,
    RequestPortal    = 1,   // My Requests, Handler Inbox, Team Queue
    Notifications    = 2,   // Alerts, Broadcast, Templates
    Reports          = 3,   // SLA Report, Inquiry, Dashboards
    AccessManagement = 4,   // RBAC: Modules, Menus, Permissions, Role Allocation
    AdminPortal      = 5,   // Masters: Employee, Branch, Config, Audit
    Analytics        = 6    // (future) MIS, Volume, Productivity
}

public enum RequestStatus
{
    Draft = 0,
    Submitted = 1,
    InProgress = 2,
    ClarificationSought = 3,
    ReturnedToEmployee = 4,
    Resolved = 5,
    Closed = 6,
    Reopened = 7,
    Cancelled = 8
}

public enum RoleCode
{
    // ── Tier 1: Organisational roles (DERIVED at login from VW_STAFF_USER_SUMMARY) ──
    BranchUser   = 1,   // USER_LEVEL=BRANCH, IS_HEAD=0
    BranchHead   = 2,   // USER_LEVEL=BRANCH, IS_HEAD=1
    RoUser       = 3,   // USER_LEVEL=RO,     IS_HEAD=0
    RoHead       = 4,   // USER_LEVEL=RO,     IS_HEAD=1
    ZoUser       = 5,   // USER_LEVEL=ZO,     IS_HEAD=0
    ZoHead       = 6,   // USER_LEVEL=ZO,     IS_HEAD=1
    CoUser       = 7,   // USER_LEVEL=CO,     IS_HEAD=0
    CoHead       = 8,   // USER_LEVEL=CO,     IS_HEAD=1
    CoVertAdmin  = 9,   // assigned via RP_RBAC_VERTICAL_ADMIN (per vertical)
    AppAdmin     = 10,  // USER_LEVEL=ADMIN  OR  RP_RBAC_APP_ADMIN
    // ── Tier 2: Super Admin (from RP_RBAC_SUPER_ADMIN, max 2) ────────────────
    SuperAdmin   = 11,
    // ── Tier 3: Vertical process levels (from RP_RBAC_VERTICAL_LEVEL) ────────
    VertL1       = 12,  // Vertical Level 1 — entry processor
    VertL2       = 13,  // Vertical Level 2 — + delegation + reports
    VertL3       = 14,  // Vertical Level 3 — + team modify + MIS
    VertL4       = 15,  // Vertical Level 4 — + authorize + broadcast
    VertL5       = 16,  // Vertical Level 5 — oversight (view-only)
    // ── Tier 4: Corporate Credit Portal (CCP) desk roles ─────────────────────
    // BranchMaker/BranchHead/ZonalHead are NOT here — they reuse BranchUser/BranchHead/ZoUser/ZoHead
    // above directly (Tier 1, attribute-derived). CoLcv/CoMcv can't be attribute-derived (HRMS has no
    // LCV/MCV split), so they're explicitly assigned via RP_RBAC_VERTICAL_LEVEL with VERTICAL_CODE='CCP'
    // — see CcpRbacRepo / AccessManagement/CcpRoleAllocation.razor.
    CoLcv        = 17,  // CO desk — Large Corporate Vertical
    CoMcv        = 18,  // CO desk — Mid Corporate Vertical
}

/// <summary>
/// Derives the primary organisational role from the HRMS view fields.
/// Returns AppAdmin for ADMIN-level users; caller adds CoVertAdmin / VertL* separately.
/// </summary>
public static class RoleDerivation
{
    public static RoleCode FromView(string userLevel, bool isHead) =>
        userLevel.ToUpperInvariant() switch
        {
            "BRANCH" => isHead ? RoleCode.BranchHead : RoleCode.BranchUser,
            "RO"     => isHead ? RoleCode.RoHead     : RoleCode.RoUser,
            "ZO"     => isHead ? RoleCode.ZoHead     : RoleCode.ZoUser,
            "CO"     => isHead ? RoleCode.CoHead     : RoleCode.CoUser,
            "ADMIN"  => RoleCode.AppAdmin,
            _        => RoleCode.BranchUser
        };
}

public enum NotifChannel
{
    Email = 1,
    Sms = 2
}

public enum NotifEvent
{
    Created,
    Assigned,
    Forwarded,
    Escalated,
    ClarificationSought,
    ClarificationProvided,
    Resolved,
    Reopened,
    Closed,
    FeedbackRequested
}

public enum NotifOutboxStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public enum CloseReason
{
    Resolved = 0,
    NotFeasible = 1,
    NotAsPerPolicy = 2,
    OutOfScope = 3,
    DuplicateRequest = 4,
    InsufficientInformation = 5,
    WithdrawnByUser = 6,
    NoActionRequired = 7,
    ResolvedByOtherMeans = 8
}

/// <summary>
/// Kind of module — controls which permission flags make sense.
/// Transactional modules expose Add / Modify / Delete / Authorize.
/// Inquiry / Report / MIS / Dashboard expose only View.
/// </summary>
public enum ModuleKind
{
    Transactional = 1,
    Inquiry       = 2,
    Report        = 3,
    MIS           = 4,
    Dashboard     = 5
}

/// <summary>
/// Atomic permission flag on a (role, module) or (user, module) edge.
/// Stored as boolean columns; this enum is only used when CALLING into the
/// permission service ("does user X have Modify on module Y?").
/// </summary>
public enum PermAction
{
    View      = 1,
    Add       = 2,
    Modify    = 3,
    Delete    = 4,
    Authorize = 5
}

public enum RbacAuditAction
{
    GrantAppAdmin,
    RevokeAppAdmin,
    UpsertModule,
    DeleteModule,
    UpsertMenu,
    DeleteMenu,
    EnableMenu,
    DisableMenu,
    UpsertRolePerm,
    RevokeRolePerm,
    UpsertUserOverride,
    RevokeUserOverride,
    GrantFeedbackAdmin,
    RevokeFeedbackAdmin,
    GrantCcpVerticalAdmin,
    RevokeCcpVerticalAdmin,
    AssignCcpRole,
    RevokeCcpRole
}

public enum AuditAction
{
    Create,
    Update,
    Delete,
    Submit,
    Forward,
    Escalate,
    Resolve,
    Reopen,
    Close,
    SeekClarification,
    ProvideClarification,
    Cancel,
    Login,
    Logout
}
