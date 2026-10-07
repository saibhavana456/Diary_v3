using RequestPortal.Core;
using RequestPortal.Core.Models;

namespace RequestPortal.Web.Demo;

public sealed class DemoStore
{
    public readonly object Sync = new();
    private long _seq;
    public long NextId() { lock (Sync) return ++_seq; }

    public List<RequestType> RequestTypes { get; } = new();
    public List<UnitType> UnitTypes { get; } = new();
    public List<Unit> Units { get; } = new();
    public List<Vertical> Verticals { get; } = new();
    public List<Department> Departments { get; } = new();
    public List<Activity> Activities { get; } = new();
    public List<HolidayCalendar> HolidayCalendars { get; } = new();
    public List<Holiday> Holidays { get; } = new();
    public List<SlaConfig> SlaConfigs { get; } = new();
    public List<NotifTemplate> NotifTemplates { get; } = new();
    public List<RoutingRule> RoutingRules { get; } = new();
    public List<RoutingAssignee> RoutingAssignees { get; } = new();

    // ── Organisational masters (mirror of VW_STAFF_USER_SUMMARY) ───────────
    public List<SolIdRecord>          SolIds          { get; } = new();
    public List<EmployeeMaster>       EmployeeMasters { get; } = new();
    public List<RoleAllocationRecord> RoleAllocations { get; } = new();

    public List<AppUser> Users { get; } = new();
    public List<AppRole> Roles { get; } = new();
    public List<UserRole> UserRoles { get; } = new();
    public List<Delegation> Delegations { get; } = new();

    public List<Request> Requests { get; } = new();
    public List<RequestAssignee> RequestAssignees { get; } = new();
    public List<RequestAction> RequestActions { get; } = new();
    public List<Clarification> Clarifications { get; } = new();
    public List<SlaPause> SlaPauses { get; } = new();
    public List<Feedback> Feedbacks { get; } = new();
    public List<Attachment> Attachments { get; } = new();
    public List<AuditLog> AuditLogs { get; } = new();
    public List<NotifOutboxItem> NotifOutbox { get; } = new();
    public List<NotificationItem> Notifications { get; } = new();

    // RBAC
    public List<AppModule>               Modules                    { get; } = new();
    public List<AppMenu>                 Menus                      { get; } = new();
    public List<RoleModulePerm>          RolePerms                  { get; } = new();
    public List<UserModulePerm>          UserOverrides               { get; } = new();
    public List<AppAdmin>                AppAdmins                  { get; } = new();
    public List<SuperAdmin>              SuperAdmins                { get; } = new();
    public List<VerticalAdminAssignment> VerticalAdminAssignments   { get; } = new();
    public List<VerticalLevelAssignment> VerticalLevelAssignments   { get; } = new();
    public List<RbacAudit>               RbacAudits                 { get; } = new();

    public Dictionary<string, (string FileName, string Mime, byte[] Data)> AttachmentFiles { get; } = new();

    public string? GetRoleName(RoleCode code) => Roles.FirstOrDefault(r => r.Code == code)?.Name;

    public AppUser? GetUserByEmpCode(string empCode) =>
        Users.FirstOrDefault(u => string.Equals(u.EmpCode, empCode, StringComparison.OrdinalIgnoreCase));

    public EmployeeMaster? GetEmployeeMaster(long userId) =>
        EmployeeMasters.FirstOrDefault(e => e.UserId == userId);

    // Derives the primary organisational role from Sol ID hierarchy + position.
    public static RoleCode DeriveRole(SolIdType solType, EmployeePosition position) =>
        (solType, position) switch
        {
            (SolIdType.Branch,         EmployeePosition.Officer)       => RoleCode.BranchUser,
            (SolIdType.Branch,         EmployeePosition.SeniorOfficer) => RoleCode.BranchUser,
            (SolIdType.Branch,         EmployeePosition.Head)          => RoleCode.BranchHead,
            (SolIdType.RegionalOffice, EmployeePosition.Officer)       => RoleCode.RoUser,
            (SolIdType.RegionalOffice, EmployeePosition.SeniorOfficer) => RoleCode.RoUser,
            (SolIdType.RegionalOffice, EmployeePosition.Head)          => RoleCode.RoHead,
            (SolIdType.ZonalOffice,    EmployeePosition.Officer)       => RoleCode.ZoUser,
            (SolIdType.ZonalOffice,    EmployeePosition.SeniorOfficer) => RoleCode.ZoUser,
            (SolIdType.ZonalOffice,    EmployeePosition.Head)          => RoleCode.ZoHead,
            (SolIdType.CoVertical,     EmployeePosition.Officer)       => RoleCode.CoUser,
            (SolIdType.CoVertical,     EmployeePosition.SeniorOfficer) => RoleCode.CoUser,
            (SolIdType.CoVertical,     EmployeePosition.Head)          => RoleCode.CoHead,
            (SolIdType.CoVertical,     EmployeePosition.VerticalAdmin) => RoleCode.CoVertAdmin,
            _                                                           => RoleCode.BranchUser
        };

    public static string SolIdTypeLabel(SolIdType t) => t switch
    {
        SolIdType.Branch         => "Branch",
        SolIdType.RegionalOffice => "Regional Office",
        SolIdType.ZonalOffice    => "Zonal Office",
        SolIdType.CoVertical     => "CO Vertical",
        _                        => t.ToString()
    };

    public static string PositionLabel(EmployeePosition p) => p switch
    {
        EmployeePosition.Officer       => "Officer",
        EmployeePosition.SeniorOfficer => "Senior Officer",
        EmployeePosition.Head          => "Head",
        EmployeePosition.VerticalAdmin => "Vertical Admin",
        _                              => p.ToString()
    };

    public static string RoleLabel(RoleCode r) => r switch
    {
        RoleCode.BranchUser   => "Branch User",
        RoleCode.BranchHead   => "Branch Head",
        RoleCode.RoUser       => "RO Officer",
        RoleCode.RoHead       => "RO Head",
        RoleCode.ZoUser       => "ZO Officer",
        RoleCode.ZoHead       => "ZO Head",
        RoleCode.CoUser       => "CO Officer",
        RoleCode.CoHead       => "CO Head",
        RoleCode.CoVertAdmin  => "CO Vert Admin",
        RoleCode.VertL1       => "Vert L1",
        RoleCode.VertL2       => "Vert L2",
        RoleCode.VertL3       => "Vert L3",
        RoleCode.VertL4       => "Vert L4",
        RoleCode.VertL5       => "Vert L5",
        RoleCode.AppAdmin     => "App Admin",
        RoleCode.SuperAdmin   => "Super Admin",
        _                     => r.ToString()
    };

    public static string RoleLabelFromString(string code) =>
        Enum.TryParse<RoleCode>(code, out var r) ? RoleLabel(r) : code;
}
