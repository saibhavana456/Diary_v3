using RequestPortal.Core;
using Microsoft.AspNetCore.Components;

namespace RequestPortal.Web.Services;

/// <summary>
/// Scoped service (one per Blazor circuit) that tracks which portal module
/// is currently active and notifies subscribers when it changes.
/// </summary>
public sealed class ModuleContext
{
    private PortalModule _active = PortalModule.MainApp;

    public PortalModule Active => _active;

    public string ActiveLabel => _active switch
    {
        PortalModule.RequestPortal    => "Request Portal",
        PortalModule.Notifications    => "Notifications",
        PortalModule.Reports          => "Reports",
        PortalModule.AccessManagement => "Access Management",
        PortalModule.AdminPortal      => "Admin Portal",
        PortalModule.Analytics        => "Analytics",
        _                             => "Main App"
    };

    /// <summary>Fired on the Blazor circuit whenever the active module changes.</summary>
    public event Action? Changed;

    public void SwitchTo(PortalModule module)
    {
        if (_active == module) return;
        _active = module;
        Changed?.Invoke();
    }

    /// <summary>
    /// Auto-detect the active module from the current absolute URI.
    /// Neutral routes (home, profile) do not change the current module.
    /// </summary>
    public void SyncFromRoute(string absoluteUri, NavigationManager nav)
    {
        var rel = nav.ToBaseRelativePath(absoluteUri)
                     .Split('?', '#')[0]
                     .Trim('/')
                     .ToLowerInvariant();

        var m = Classify(rel);
        if (m.HasValue) SwitchTo(m.Value);
    }

    public static PortalModule? Classify(string rel)
    {
        // Notifications-module routes (even though they sit under /admin/)
        if (rel is "admin/broadcast" or "admin/templates" ||
            rel.StartsWith("admin/broadcast/") || rel.StartsWith("admin/templates/"))
            return PortalModule.Notifications;

        // Access Management (RBAC + people/role masters)
        if (rel.StartsWith("admin/rbac") ||
            rel.StartsWith("admin/page-config") ||
            rel == "masters/role-allocation" ||
            rel.StartsWith("masters/role-allocation/") ||
            rel.StartsWith("masters/users") ||
            rel.StartsWith("masters/roles"))
            return PortalModule.AccessManagement;

        // Admin Portal (masters, config, audit, ops admin)
        if (rel.StartsWith("admin/") ||
            rel.StartsWith("masters/employees") ||
            rel.StartsWith("masters/branches") ||
            rel.StartsWith("masters/units"))
            return PortalModule.AdminPortal;

        // Request Portal
        if (rel.StartsWith("requests") || rel.StartsWith("handler/") || rel.StartsWith("oversight/"))
            return PortalModule.RequestPortal;

        // Reports
        if (rel.StartsWith("reports/") || rel.StartsWith("inquiry/"))
            return PortalModule.Reports;

        // Notifications
        if (rel == "notifications")
            return PortalModule.Notifications;

        // Main App routes (app/*)
        if (rel.StartsWith("app/") || rel == "app")
            return PortalModule.MainApp;

        // Neutral routes — leave module unchanged
        return null;
    }
}
