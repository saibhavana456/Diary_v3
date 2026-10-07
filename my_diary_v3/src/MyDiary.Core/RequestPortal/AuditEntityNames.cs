namespace RequestPortal.Core;

/// <summary>
/// Canonical entity-name constants used when writing to IAuditService.
/// Using constants (rather than ad-hoc strings) ensures the Setup Audit Log
/// page can filter reliably and that names stay consistent when migrating
/// to the Master Application.
/// </summary>
public static class AuditEntityNames
{
    // ── Master Data ──────────────────────────────────────────────────────────
    public const string RequestType     = "RP_REQUEST_TYPE";
    public const string UnitType        = "RP_UNIT_TYPE";
    public const string Vertical        = "RP_VERTICAL";
    public const string Unit            = "RP_UNIT";
    public const string Department      = "RP_DEPARTMENT";
    public const string Activity        = "RP_ACTIVITY";

    // ── Request Config ───────────────────────────────────────────────────────
    public const string SlaConfig       = "RP_SLA_CONFIG";
    public const string RoutingRule     = "RP_ROUTING_RULE";
    public const string RoutingAssignee = "RP_ROUTING_ASSIGNEE";
    public const string HolidayCalendar = "RP_HOLIDAY_CALENDAR";
    public const string Holiday         = "RP_HOLIDAY";
    public const string NotifTemplate   = "RP_NOTIF_TEMPLATE";
    public const string Delegation      = "RP_DELEGATION";
    public const string PageInfo        = "RP_PAGE_INFO";
    public const string MomType         = "RP_M_MOM_TYPE";
    public const string ReportDef       = "RP_M_REPORT_DEF";

    // ── RBAC / Access Management ─────────────────────────────────────────────
    public const string RolePermission  = "RP_ROLE_PERM";
    public const string UserPermission  = "RP_USER_PERM";
    public const string AppMenu         = "RP_MENU";
    public const string AppModule       = "RP_MODULE";

    // ── Action labels ────────────────────────────────────────────────────────
    public const string ActionCreate    = "CREATE";
    public const string ActionUpdate    = "UPDATE";
    public const string ActionDelete    = "DELETE";
    public const string ActionActivate  = "ACTIVATE";
    public const string ActionDeactivate= "DEACTIVATE";

    /// <summary>All entity names that represent setup / configuration changes
    /// (used by the Setup Audit Log page to pre-fill the entity filter).</summary>
    public static readonly IReadOnlyList<string> SetupEntities = new[]
    {
        RequestType, UnitType, Vertical, Unit, Department, Activity,
        SlaConfig, RoutingRule, RoutingAssignee, HolidayCalendar, Holiday,
        NotifTemplate, Delegation, PageInfo, MomType, ReportDef,
        RolePermission, UserPermission, AppMenu, AppModule,
    };
}
