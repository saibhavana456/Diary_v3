using MyDiary.Core.Services;
using PageInfoRecord = RequestPortal.Web.Features.PageInfo.Models.PageInfo;
using PageInfoForm   = RequestPortal.Web.Features.PageInfo.Models.PageInfoForm;

namespace RequestPortal.Web.Features.PageInfo.Services;

/// <summary>
/// Demo implementation — serves in-memory page metadata for all portal pages.
/// In production this is replaced by OraclePageInfoService (RP_PAGE_INFO table).
/// </summary>
public sealed class DemoPageInfoService : IPageInfoService
{
    // ── In-memory store (seeded at startup, mutable during demo session) ───────
    private readonly Dictionary<string, PageInfoRecord> _store = Seed().ToDictionary(p => p.PageKey);

    public Task<PageInfoRecord?> GetAsync(string pageKey)
        => Task.FromResult(_store.TryGetValue(pageKey, out var p) ? p : null);

    public Task<List<PageInfoRecord>> GetAllAsync()
        => Task.FromResult(_store.Values.OrderBy(p => p.PageName).ToList());

    public Task<bool> UpsertAsync(PageInfoForm f, bool isNew)
    {
        var record = new PageInfoRecord(
            f.PageKey, f.PageName, f.PageType, f.Description,
            f.OwnerVertical, f.ModuleOwner, f.IpNumber, f.MailId,
            _store.TryGetValue(f.PageKey, out var ex) ? ex.CreatedDate : AppTime.Today,
            f.Version, f.Status);
        _store[f.PageKey] = record;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string pageKey)
    {
        _store.Remove(pageKey);
        return Task.FromResult(true);
    }

    // ── Seed data — all portal pages ──────────────────────────────────────────
    private static IEnumerable<PageInfoRecord> Seed()
    {
        var dt = new DateTime(2024, 4, 1);

        PageInfoRecord P(string key, string name, string type, string desc,
                         string vertical, string owner, string ip, string mail,
                         string ver = "v1.0", string status = "ACTIVE", DateTime? created = null)
            => new(key, name, type, desc, vertical, owner, ip, mail, created ?? dt, ver, status);

        // ── Main App / AppModule ──────────────────────────────────────────────
        yield return P("app-home",
            "App Home", "Dashboard",
            "Landing page for the Main App module. Displays a personalised welcome banner, quick-access KPI summary tiles and shortcuts to all portal modules for the logged-in user.",
            "IT / Digital Banking", "IT Team", "IP-RP-2024-0001", "it.support@company.in");

        yield return P("kpi-dashboard",
            "KPI Dashboard", "Dashboard",
            "Branch-level Key Performance Indicators dashboard. Shows request volumes, SLA adherence, handler workload and trend charts. Accessible to L3, L4, L5 and Admin roles.",
            "Retail Banking", "Branch Analytics", "IP-RP-2024-0002", "analytics@company.in");

        yield return P("all-dashboards",
            "All Dashboards", "Dashboard",
            "Navigation hub that lists all available dashboards in the portal filtered by the user's role. L3+ and Admin see KPI and Oversight dashboards; L1/L2 see handler view only.",
            "IT / Digital Banking", "IT Team", "IP-RP-2024-0003", "it.support@company.in");

        yield return P("focus360",
            "Focus 360", "Dashboard",
            "Comprehensive 360° branch performance snapshot with parameter-driven gap analysis across Financial Inclusion, Jansavarth, Digital Banking, Third Party Products, CASA, Credit and Compliance verticals. Exports to Excel and PDF.",
            "Retail Banking", "Branch Analytics", "IP-RP-2024-0004", "analytics@company.in", "v2.0");

        yield return P("branch-overview",
            "Branch Overview", "Dashboard",
            "Real-time branch-level overview sourced from the BANKDASH schema. Displays KPI scorecards, monthly net-deposit bar chart, active loan portfolio breakdown, account-mix table and the 8 most recent transactions.",
            "Retail Banking", "Branch Analytics", "IP-RP-2025-0001", "analytics@company.in");

        yield return P("branch-performance",
            "Branch Performance", "Dashboard",
            "Monthly branch performance report covering new account acquisitions, deposit vs loan disbursement dual-bar chart, transaction volume trend, new-account acquisition bars and a CD-ratio detail table for the last 6 months.",
            "Retail Banking", "Branch Analytics", "IP-RP-2025-0002", "analytics@company.in");

        // ── Requests ──────────────────────────────────────────────────────────
        yield return P("requests-new",
            "New Request", "Upload",
            "Multi-step wizard to raise a new service request. Users select a request type, fill in required fields, attach supporting documents and submit for handler review. SLA clock starts on submission.",
            "Operations", "Ops Team", "IP-RP-2024-0010", "ops@company.in");

        yield return P("requests-mine",
            "My Requests", "Enquiry",
            "Personal request tracker listing all requests submitted by the current user with real-time status, SLA countdown and one-click drill-down to the full detail view.",
            "Operations", "Ops Team", "IP-RP-2024-0011", "ops@company.in");

        yield return P("requests-detail",
            "Request Detail", "Enquiry",
            "Full detail view for a single service request including form data, attachments, complete activity timeline, handler notes and escalation history.",
            "Operations", "Ops Team", "IP-RP-2024-0012", "ops@company.in");

        // ── Handler ───────────────────────────────────────────────────────────
        yield return P("handler-inbox",
            "Handler Inbox", "Approve",
            "Inbox for assigned request handlers. Displays pending and in-progress requests sorted by SLA urgency. Supports bulk assignment, action dialogs and priority escalation.",
            "Operations", "Ops Team", "IP-RP-2024-0020", "ops@company.in");

        // ── Dashboards ────────────────────────────────────────────────────────
        yield return P("handler-dashboard",
            "Handler Dashboard", "Dashboard",
            "Personal handler performance dashboard showing open workload, SLA countdown gauges, closed-today count and historical completion trend for the logged-in handler.",
            "Operations", "Ops Team", "IP-RP-2024-0031", "ops@company.in");

        yield return P("oversight-dashboard",
            "Oversight Dashboard", "Dashboard",
            "Supervisory view for L5 and Admin roles. Aggregates cross-team request metrics, SLA breach alerts, handler workload distribution and department-wise volume charts.",
            "Operations", "Ops Team", "IP-RP-2024-0030", "ops@company.in");

        // ── Masters ───────────────────────────────────────────────────────────
        yield return P("masters-users",
            "Users Master", "Maintenance",
            "Create, edit and deactivate portal user accounts. Maps employees to login identities and assigns initial role. Supports bulk import from HR extract.",
            "HR / IT", "HR Admin", "IP-RP-2024-0040", "hr.admin@company.in");

        yield return P("masters-roles",
            "Roles Master", "Maintenance",
            "Define and manage portal roles (Admin, L1–L5, Employee). Each role determines menu visibility, approval rights and RBAC permissions across all modules.",
            "IT", "IT Admin", "IP-RP-2024-0041", "it.admin@company.in");

        yield return P("masters-employees",
            "Employee Master", "Maintenance",
            "Employee directory master with staff code, designation, branch, mobile and email. Source of truth for handler assignment, delegation and SLA notification routing.",
            "HR", "HR Admin", "IP-RP-2024-0042", "hr.admin@company.in");

        yield return P("masters-branches",
            "Branch / Sol ID Master", "Maintenance",
            "Branch master with Sol ID, IFSC code, branch name, address, zone and region. Used for routing rules, Focus 360 parameterisation and branch-level reporting.",
            "Operations", "Ops Admin", "IP-RP-2024-0043", "ops.admin@company.in");

        yield return P("masters-units",
            "Units Master", "Maintenance",
            "Business units and departments master. Units are used in request routing rules to direct service requests to the correct handling team.",
            "Operations", "Ops Admin", "IP-RP-2024-0044", "ops.admin@company.in");

        yield return P("masters-role-allocation",
            "Role Allocation", "Maintenance",
            "Assign one or more portal roles to employees. Changes take effect on next login. Supports bulk allocation and role-expiry dates for temporary access.",
            "IT", "IT Admin", "IP-RP-2024-0045", "it.admin@company.in");

        // ── Admin ─────────────────────────────────────────────────────────────
        yield return P("admin-all-requests",
            "All Requests (Admin)", "Enquiry",
            "Admin-level view of every request across all users, teams and departments. Supports advanced filters by status, category, date range, handler and branch. Export to Excel available.",
            "Operations", "Ops Admin", "IP-RP-2024-0050", "ops.admin@company.in");

        yield return P("admin-templates",
            "Notification Templates", "Maintenance",
            "Configure email and SMS notification templates for request lifecycle events (submitted, assigned, approved, rejected, escalated). Supports token substitution.",
            "Operations", "Ops Admin", "IP-RP-2024-0051", "ops.admin@company.in");

        yield return P("admin-routing",
            "Routing Rules", "Maintenance",
            "Define auto-routing rules that assign incoming requests to specific handler units or individuals based on request type, category, branch or priority.",
            "Operations", "Ops Admin", "IP-RP-2024-0052", "ops.admin@company.in");

        yield return P("admin-sla",
            "SLA Configuration", "Maintenance",
            "Configure Service Level Agreement timelines per request category and priority. Supports business-day calculations using the Holiday Calendar. Breach alerts trigger at configurable thresholds.",
            "Operations", "Ops Admin", "IP-RP-2024-0053", "ops.admin@company.in");

        yield return P("admin-holidays",
            "Holiday Calendar", "Maintenance",
            "Manage the organisation's holiday calendar used for SLA business-day calculations. Supports multiple calendar groups (national, state, regional) and recurring annual holidays.",
            "HR", "HR Admin", "IP-RP-2024-0054", "hr.admin@company.in");

        yield return P("admin-delegations",
            "Delegations", "Maintenance",
            "Configure delegation rules for when a handler is on leave or unavailable. Requests are automatically re-routed to the delegate for the specified date range.",
            "Operations", "Ops Admin", "IP-RP-2024-0055", "ops.admin@company.in");

        yield return P("admin-broadcast",
            "Broadcast", "Maintenance",
            "Compose and send portal-wide or targeted announcements to all users or specific role groups. Broadcasts appear in the Notification bell and optionally via email.",
            "IT", "IT Admin", "IP-RP-2024-0056", "it.admin@company.in");

        yield return P("admin-audit-search",
            "Audit Search", "Report",
            "Full audit trail search across all portal actions — request changes, role assignments, login events and admin operations. Supports date-range filters and export to Excel.",
            "Compliance", "Compliance Team", "IP-RP-2024-0057", "compliance@company.in");

        yield return P("admin-masters",
            "Request Types & Units", "Maintenance",
            "Manage dropdown and reference data masters including request categories, sub-categories, priorities and business units used throughout the portal's forms and routing engine.",
            "Operations", "Ops Admin", "IP-RP-2024-0058", "ops.admin@company.in");

        yield return P("admin-api-help",
            "API Help", "Enquiry",
            "Developer reference for the Request Portal REST API. Documents endpoints, authentication (Bearer token), request/response schemas and integration examples for external systems.",
            "IT", "IT Admin", "IP-RP-2024-0059", "it.admin@company.in");

        // ── RBAC / Access Management ──────────────────────────────────────────
        yield return P("rbac-menus",
            "Menu Management", "Maintenance",
            "Add, edit, reorder and toggle the visibility of navigation menu items across all portal modules. Enable/disable is SuperAdmin-only. Changes take effect immediately without restart.",
            "IT", "IT Admin", "IP-RP-2024-0060", "it.admin@company.in");

        yield return P("rbac-modules",
            "Module Management", "Maintenance",
            "Configure portal modules and sub-modules used in the RBAC permission matrix. Each module maps to a code (e.g. MAINAPP.KPI) that controls access via role-permission rules.",
            "IT", "IT Admin", "IP-RP-2024-0061", "it.admin@company.in");

        yield return P("rbac-role-permissions",
            "Role Permissions", "Maintenance",
            "Define granular permissions (View, Create, Edit, Delete, Approve) for each role across every portal module. Changes are applied to all users holding the affected role on next page load.",
            "IT", "IT Admin", "IP-RP-2024-0062", "it.admin@company.in");

        yield return P("rbac-admin-rights",
            "Admin Rights", "Maintenance",
            "Grant or revoke Application Admin and Super Admin privileges for specific users. SuperAdmin access is required to modify this page. All changes are logged in the RBAC Audit Log.",
            "IT", "IT Admin", "IP-RP-2024-0063", "it.admin@company.in");

        yield return P("rbac-audit-log",
            "RBAC Audit Log", "Report",
            "Immutable audit log of all role assignment changes, permission modifications and admin-rights grants within the RBAC system. Searchable by user, action type and date range.",
            "Compliance", "Compliance Team", "IP-RP-2024-0064", "compliance@company.in");

        // ── Page Configuration (self-referential) ─────────────────────────────
        yield return P("admin-page-config",
            "Page Configuration", "Maintenance",
            "CRUD interface for managing RP_PAGE_INFO records — the metadata displayed in the floating ⓘ info panel on every portal page. Fields include page type, owner vertical, module owner, IP number, contact email, version and status.",
            "IT", "IT Admin", "IP-RP-2025-0003", "it.admin@company.in");

        // ── Setup Audit Log ───────────────────────────────────────────────────
        yield return P("admin-setup-audit",
            "Setup Audit Log", "Report",
            "Tamper-evident chronological audit trail for every create, edit, delete and activate/deactivate action performed on Admin setup screens — Masters, SLA, Routing Rules, Holiday Calendars, Notification Templates, Delegations and Page Config. Every entry captures the actor login ID, timestamp, entity, record ID, and a before/after JSON snapshot. Backed by an SHA-256 hash chain for integrity verification.",
            "IT", "IT Admin", "IP-RP-2025-0004", "it.admin@company.in");

        // ── Reports ───────────────────────────────────────────────────────────
        yield return P("reports-sla",
            "SLA Breach Report", "Report",
            "SLA compliance report with breach analysis by request category, priority and handler team. Highlights breached, at-risk and compliant requests. Supports date-range filters and Excel export.",
            "Operations", "Ops Admin", "IP-RP-2024-0070", "ops.admin@company.in");

        // ── Other ─────────────────────────────────────────────────────────────
        yield return P("notifications",
            "Notifications", "Enquiry",
            "Centralised notification centre displaying all portal alerts, approvals, escalations and system messages for the logged-in user. Supports unread filter and mark-all-read.",
            "IT", "IT Admin", "IP-RP-2024-0080", "it.admin@company.in");

        yield return P("profile-user",
            "User Profile", "Maintenance",
            "View and update personal profile details including display name, contact number, email preferences and notification settings. Branch and role information is read-only and managed by Admin.",
            "HR / IT", "IT Admin", "IP-RP-2024-0081", "it.admin@company.in");

        yield return P("operations-team-queue",
            "Team Queue", "Enquiry",
            "Operations team queue showing all pending and in-progress work items assigned to the team, sorted by SLA urgency. L2+ handlers can reassign within the team from this view.",
            "Operations", "Ops Team", "IP-RP-2024-0082", "ops@company.in");

        yield return P("inquiry-requests",
            "Request Inquiry", "Enquiry",
            "Read-only search interface for looking up request status by reference number, applicant name or date range. Accessible to all authenticated users for self-service tracking.",
            "Operations", "Ops Team", "IP-RP-2024-0083", "ops@company.in");

        // ── Request Portal keys referenced by pages but previously unseeded ─────
        yield return P("escalation-matrix",
            "Escalation Matrix", "Enquiry",
            "Level-wise (L1–L5) escalation contacts for a selected activity/unit. Cascading filters (request type, unit type, unit, vertical, department, activity) drive the matrix shown to all staff.",
            "Operations", "Ops Team", "IP-RP-2025-0010", "ops@company.in");

        yield return P("escalation-matrix-admin",
            "Escalation Matrix — Manage", "Maintenance",
            "Admin screen to define and maintain the L1–L5 escalation members for each activity — PF number, name, scale, role, email, mobile, IP and generic mailbox.",
            "Operations", "Ops Admin", "IP-RP-2025-0011", "ops.admin@company.in");

        yield return P("classification-map",
            "Classification Mapping", "Maintenance",
            "Interlink master that maps request types to unit types and units to verticals, driving the cascading classification used across New Request, Escalation Matrix and routing.",
            "Operations", "Ops Admin", "IP-RP-2025-0012", "ops.admin@company.in");

        yield return P("admin-api-help-uccrmc",
            "API Help — UCCRMC Alerts", "Enquiry",
            "Developer reference for the UCCRMC alert-ingestion API — endpoint, X-Api-Key authentication, payload schema and sample requests used by the external command centre to raise alerts.",
            "IT", "IT Admin", "IP-RP-2025-0013", "it.admin@company.in");

        // ── My Diary modules / sub-modules (menus & sub-menus) ──────────────────
        yield return P("md-home",
            "Home", "Dashboard",
            "Employee home — corporate events feed, staff details, branch profile and quick actions (New Request, My Requests, Escalation Matrix).",
            "IT / Digital Banking", "MyDiary Team", "IP-MD-2025-0001", "mydiary@unionbankofindia.bank.in");

        yield return P("md-home-exec",
            "Home (Executive)", "Dashboard",
            "Executive home dashboard — bank-wide Business KPIs, Advances & Deposits performance/growth/portfolio charts, and operational snapshot cards (Digital Business, Operations, Third Party Products, Financial Inclusion).",
            "Strategy / Analytics", "MyDiary Team", "IP-MD-2025-0002", "mydiary@unionbankofindia.bank.in", "v2.0");

        yield return P("md-home-legacy",
            "Home (Legacy)", "Dashboard",
            "Previous-generation landing dashboard retained for reference during the transition to the redesigned Home pages.",
            "IT / Digital Banking", "MyDiary Team", "IP-MD-2025-0003", "mydiary@unionbankofindia.bank.in");

        yield return P("md-departments",
            "Departments", "Enquiry",
            "Directory of bank departments with quick links to each department's resources, downloads, circulars and policies.",
            "IT / Digital Banking", "MyDiary Team", "IP-MD-2025-0004", "mydiary@unionbankofindia.bank.in");

        yield return P("md-ecirculars",
            "e-Circulars", "Enquiry",
            "Searchable repository of bank e-Circulars sourced from the DMS, browsable by department and year.",
            "Compliance", "Compliance Team", "IP-MD-2025-0005", "compliance@unionbankofindia.bank.in");

        yield return P("md-policies",
            "Policies", "Enquiry",
            "Central library of bank policies and guidelines, categorised by department and reviewed periodically.",
            "Compliance", "Compliance Team", "IP-MD-2025-0006", "compliance@unionbankofindia.bank.in");

        yield return P("md-downloads",
            "Downloads", "Enquiry",
            "Downloadable forms, templates, manuals and reference material used across branches and offices.",
            "IT / Digital Banking", "MyDiary Team", "IP-MD-2025-0007", "mydiary@unionbankofindia.bank.in");

        yield return P("md-kpi",
            "Key Performance Indicator", "Dashboard",
            "Branch/office KPI dashboard within Focus 360° — target-vs-achievement radial and performance summaries.",
            "Strategy / Analytics", "Branch Analytics", "IP-MD-2025-0008", "analytics@unionbankofindia.bank.in");

        yield return P("md-focus360",
            "Focus 360°", "Dashboard",
            "360° branch performance snapshot with parameter-driven gap analysis across Financial, Operational and Compliance verticals; exports to Excel and PDF.",
            "Strategy / Analytics", "Branch Analytics", "IP-MD-2025-0009", "analytics@unionbankofindia.bank.in", "v2.0");

        yield return P("md-business360",
            "Business 360°", "Dashboard",
            "Business hub — zone/region/branch drill-down across Advances, Deposits, Staff Details, ATM/CRM, Amenities, Branch Profile, Assurance Function and more.",
            "Strategy / Analytics", "Branch Analytics", "IP-MD-2025-0010", "analytics@unionbankofindia.bank.in", "v2.0");

        yield return P("md-business360-legacy",
            "Business 360° (Legacy)", "Dashboard",
            "Previous Business 360° layout retained during the transition to the redesigned hub.",
            "Strategy / Analytics", "Branch Analytics", "IP-MD-2025-0011", "analytics@unionbankofindia.bank.in");

        yield return P("md-assurance-snapshot",
            "Assurance Snapshot", "Report",
            "Branch-wise Financial, Operational and Compliance assurance parameters with Excel/PDF/CSV export and print.",
            "Assurance / Audit", "Assurance Team", "IP-MD-2025-0012", "assurance@unionbankofindia.bank.in");

        yield return P("md-assurance-corner",
            "Assurance Corner", "Dashboard",
            "Assurance dashboards and thematic snapshots supporting the bank's assurance and audit functions.",
            "Assurance / Audit", "Assurance Team", "IP-MD-2025-0013", "assurance@unionbankofindia.bank.in");

        yield return P("md-mis",
            "MIS", "Report",
            "Management Information System reports and analytics consolidating operational and business metrics.",
            "Strategy / Analytics", "MIS Team", "IP-MD-2025-0014", "mis@unionbankofindia.bank.in");

        yield return P("md-qlik",
            "Qlik Dashboard", "Dashboard",
            "Embedded Qlik Sense Enterprise dashboards rendered via qlik-embed with QPS ticket authentication; supports Save-As-PDF.",
            "Strategy / Analytics", "MIS Team", "IP-MD-2025-0015", "mis@unionbankofindia.bank.in");

        yield return P("md-applications",
            "Applications", "Enquiry",
            "Launchpad of internal banking applications and portals available to staff, grouped by function.",
            "IT / Digital Banking", "MyDiary Team", "IP-MD-2025-0016", "mydiary@unionbankofindia.bank.in");

        yield return P("md-atm-indent",
            "ATM Indent", "Upload",
            "Raise and track ATM cash/consumables indents for branches, routed to the concerned procurement/operations team.",
            "Operations", "Procurement Team", "IP-MD-2025-0017", "procurement@unionbankofindia.bank.in");

        yield return P("report-developer",
            "Report Developer", "Maintenance",
            "Super Admin tool to define report sources (Server File Path, SFTP, Oracle DB, MS SQL Server DB), capture connection details and queries, and publish them to the Reports page under a Vertical/Department.",
            "IT / Analytics", "IT Admin", "IP-MD-2025-0019", "it.admin@unionbankofindia.bank.in");

        yield return P("report-module-reports",
            "Reports", "Report",
            "Filter published reports by Vertical, Department and Report Name, load up to 200 rows with paging (25/50/100 per page), and export to Excel, PDF or CSV.",
            "IT / Analytics", "IT Admin", "IP-MD-2025-0020", "it.admin@unionbankofindia.bank.in");

        yield return P("md-union-hub",
            "Union Hub", "Enquiry",
            "Corporate events and updates feed — verticals post announcements, photos, upcoming events and tags for all staff.",
            "Corporate Communications", "CorpComm Team", "IP-MD-2025-0018", "corpcomm@unionbankofindia.bank.in");
    }
}
