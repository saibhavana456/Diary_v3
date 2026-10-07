namespace MyDiary.Web.Services;

/// <summary>Top-level "modules" of the merged portal (sidebar switch groups).</summary>
public enum AppModule
{
    Main,
    Focus360,
    Assurance360,
    KnowledgeHub,
    Mis,
    Applications,
    RequestPortal,
    Notifications,
    Reports,
    AccessManagement,
    AdminPortal,
    Masters,
    ApiAdmin,
    StaffCorner
}

/// <summary>Scoped (per-circuit) service tracking the active module.</summary>
public sealed class AppModuleNav
{
    public AppModule Active { get; private set; } = AppModule.Main;

    public string ActiveLabel => Active switch
    {
        AppModule.Focus360         => "Business",
        AppModule.Assurance360     => "Assurance",
        AppModule.KnowledgeHub     => "Knowledge Hub",
        AppModule.Mis              => "MIS",
        AppModule.Applications     => "Applications",
        AppModule.RequestPortal    => "Request Portal",
        AppModule.Notifications    => "Notifications",
        AppModule.Reports          => "Reports",
        AppModule.AccessManagement => "Access Management",
        AppModule.AdminPortal      => "Admin Portal",
        AppModule.Masters          => "Masters",
        AppModule.ApiAdmin         => "API Admin",
        AppModule.StaffCorner      => "Staff Corner",
        _                          => "Main"
    };

    public static string DefaultRoute(AppModule m) => m switch
    {
        AppModule.Focus360         => "dashboard_new_1",
        AppModule.StaffCorner      => "union-hub",
        AppModule.Assurance360     => "assurance-snapshot",
        AppModule.KnowledgeHub     => "circulars",
        AppModule.Mis              => "MIS",
        AppModule.Applications     => "app-portal1",
        AppModule.RequestPortal    => "requests/mine",
        AppModule.Notifications    => "notifications",
        AppModule.Reports          => "reports",
        AppModule.AccessManagement => "admin/rbac/role-permissions",
        // NOT "admin/masters" — Classify() below maps that exact path to AppModule.Masters
        // (it's Masters' own "Request Types & Units" link), so using it as AdminPortal's
        // landing route caused SwitchTo(AdminPortal) to immediately get overridden back to
        // Masters by the SyncFromRoute that fires right after navigating — Admin Portal's
        // own menu was never actually reachable, it always bounced straight to Masters.
        AppModule.AdminPortal      => "admin/classification-map",
        AppModule.Masters          => "masters/employees",
        AppModule.ApiAdmin         => "admin/api-help",
        _                          => "home"
    };

    public event Action? Changed;

    public void SwitchTo(AppModule module)
    {
        if (Active == module) return;
        Active = module;
        Changed?.Invoke();
    }

    public void SyncFromRoute(string relativePath)
    {
        var rel = (relativePath ?? "").Split('?', '#')[0].Trim('/').ToLowerInvariant();
        var m = Classify(rel);
        if (m.HasValue) SwitchTo(m.Value);
    }

    private static AppModule? Classify(string rel)
    {
        if (rel.StartsWith("requests") || rel.StartsWith("handler") || rel.StartsWith("oversight") ||
            rel.StartsWith("dashboards/handler") || rel == "reports/sla" || rel.StartsWith("inquiry"))
            return AppModule.RequestPortal;
        if (rel == "reports" || rel == "reports/developer" || rel == "reports/mom-developer" || rel.StartsWith("csbe/"))
            return AppModule.Reports;
        if (rel == "notifications" || rel == "admin/broadcast" || rel == "admin/templates"
            || rel.StartsWith("feedback-admin") || rel == "feedback-dashboard" ||
            rel == "feedback/new" || rel.StartsWith("my-feedback"))
            //return AppModule.Notifications;
        if (rel == ("union-hub") || rel == ("union-hub/video-launch") || rel == ("itpolicies"))
            return AppModule.StaffCorner;
        if (rel.StartsWith("masters/") || rel == "admin/masters")
            return AppModule.Masters;
        if (rel.StartsWith("admin/api-help"))
            return AppModule.ApiAdmin;
        if (rel.StartsWith("admin/rbac") || rel.StartsWith("admin/page-config"))
            return AppModule.AccessManagement;
        if (rel.StartsWith("admin/"))
            return AppModule.AdminPortal;
        if (rel is "app-portal1" or "applications")
            return AppModule.Applications;
        if (rel is "dashboard_new_1" or "focus360" or "business-new" or "business-360" or "businesshub" || rel.StartsWith("business/command-center"))
            return AppModule.Focus360;
        if (rel is "assurance-snapshot" or "dashboards2" or "assurance-360" || rel.StartsWith("mom/") || rel.StartsWith("executive-branch-visit"))
            return AppModule.Assurance360;
        if (rel is "circulars" or "policies" or "faq-sop" or "emanual" or "code-of-ethics"
                || rel.StartsWith("ecircular") || rel.StartsWith("knowledge-hub") || rel.StartsWith("dept-page"))
            return AppModule.KnowledgeHub;
        if (rel == "qlik-dashboard" || rel == "mis" || rel.StartsWith("mis/"))
            return AppModule.Mis;
        if (rel is "" or "home" or "home-dashboard" or "landing_dashboard_v2_1" or "departmentsnew" or "downloads2"
                or "atmindent" || rel.StartsWith("atmindent"))
            return AppModule.Main;
        return null;
    }
}
