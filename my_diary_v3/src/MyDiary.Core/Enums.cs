namespace MyDiary.Core;

// ── Organisational hierarchy ──────────────────────────────────────────────
public enum SolIdType
{
    Branch         = 1,
    RegionalOffice = 2,
    ZonalOffice    = 3,
    CoVertical     = 4
}

public enum EmployeePosition
{
    Officer        = 1,
    SeniorOfficer  = 2,
    Head           = 3,
    VerticalAdmin  = 4
}

// ── Portal module used for sidebar context switching ─────────────────────
public enum PortalModule
{
    MainApp          = 0,
    RequestPortal    = 1,
    Notifications    = 2,
    Reports          = 3,
    AccessManagement = 4,
    AdminPortal      = 5,
    Analytics        = 6
}

public enum RequestStatus
{
    Draft               = 0,
    Submitted           = 1,
    InProgress          = 2,
    ClarificationSought = 3,
    ReturnedToEmployee  = 4,
    Resolved            = 5,
    Closed              = 6,
    Reopened            = 7,
    Cancelled           = 8
}

public enum RoleCode
{
    // Organisational roles
    BranchUser  = 1,
    BranchHead  = 2,
    RoUser      = 3,
    RoHead      = 4,
    ZoUser      = 5,
    ZoHead      = 6,
    CoUser      = 7,
    CoHead      = 8,
    CoVertAdmin = 9,
    AppAdmin    = 10,
    SuperAdmin  = 11,
    // Vertical process levels
    VertL1      = 12,
    VertL2      = 13,
    VertL3      = 14,
    VertL4      = 15,
    VertL5      = 16,
}

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
    Sms   = 2,
    InApp = 3
}

public enum NotifOutboxStatus
{
    Pending = 0,
    Sent    = 1,
    Failed  = 2
}

public enum CloseReason
{
    Resolved               = 0,
    NotFeasible            = 1,
    NotAsPerPolicy         = 2,
    OutOfScope             = 3,
    DuplicateRequest       = 4,
    InsufficientInformation = 5,
    WithdrawnByUser        = 6,
    NoActionRequired       = 7,
    ResolvedByOtherMeans   = 8
}

public enum ModuleKind
{
    Transactional = 1,
    Inquiry       = 2,
    Report        = 3,
    MIS           = 4,
    Dashboard     = 5
}

public enum PermAction
{
    View      = 1,
    Add       = 2,
    Modify    = 3,
    Delete    = 4,
    Authorize = 5
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

// ── User privilege levels from My Diary (B) authentication ───────────────
public enum UserPrivilege
{
    Branch = 1,
    Region = 2,
    Zone   = 3,
    CO     = 4
}
