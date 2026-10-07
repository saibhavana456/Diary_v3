namespace RequestPortal.Core.Models;

/// <summary>One raw page-visit event — a hit on an RBAC-registered menu route.
/// Org-unit ids are denormalized from the viewer's session claims at visit
/// time so drill-down/aggregation never needs a runtime join.</summary>
public sealed class PageVisit
{
    public long Id { get; set; }
    public string MenuCode { get; set; } = "";
    public string Route { get; set; } = "";
    public string EmpCode { get; set; } = "";
    public DateTime VisitedAtUtc { get; set; } = DateTime.UtcNow;
    public string? BranchSolId { get; set; }
    public string? RegionSolId { get; set; }
    public string? ZoneSolId { get; set; }
    public string? BranchName { get; set; }
    public string? RegionName { get; set; }
    public string? ZoneName { get; set; }
}

/// <summary>One bucket (day/month/quarter) of a usage trend line for one menu item.</summary>
public sealed record PageVisitTrendPoint(DateTime Bucket, string MenuCode, string MenuLabel, long VisitCount, long UniqueUsers);

/// <summary>One row of the org drill-down table — one Zone, Region, or Branch.</summary>
public sealed record PageUsageDrilldownRow(string ScopeId, long VisitCount, long UniqueUsers, string? ScopeName = null);

/// <summary>One row of the nightly underused-page ranking for a given scope
/// (BANK / a specific Zone / Region / Branch).</summary>
public sealed record UnderusedPageInsight(string MenuCode, string? MenuLabel, long VisitCount30D, int RankAsc, DateTime ComputedAtUtc);

public enum UsageTrendGranularity { Daily, Monthly, Quarterly }

public enum UsageScopeLevel { Bank, Zone, Region, Branch }

// ── Adoption Dashboard — Logins tab ─────────────────────────────────────────

public sealed class LoginEvent
{
    public string EmpCode { get; set; } = "";
    public DateTime LoginAtUtc { get; set; } = DateTime.UtcNow;
    public string LoginMethod { get; set; } = "SESSION";
    public string? BranchSolId { get; set; }
    public string? RegionSolId { get; set; }
    public string? ZoneSolId { get; set; }
    public string? BranchName { get; set; }
    public string? RegionName { get; set; }
    public string? ZoneName { get; set; }
}

public sealed record LoginTrendPoint(DateTime Bucket, long LoginCount, long UniqueUsers);

/// <summary>One drill-down row's login activity plus headcount (from
/// VW_STAFF_USER_SUMMARY) and the resulting adoption %. Headcount is null
/// when no matching org-unit headcount could be resolved (e.g. the org-unit
/// code scheme doesn't line up with the SOL id used for login/visit
/// tracking) — shown as "—" rather than a misleading 0%.</summary>
public sealed record AdoptionRow(string ScopeId, long UniqueUsers, long LoginCount, int? Headcount, string? ScopeName = null)
{
    public double? AdoptionPercent => Headcount is > 0 ? Math.Round(100.0 * UniqueUsers / Headcount.Value, 1) : null;
}

// ── Adoption Dashboard — Modules Usage tab ──────────────────────────────────

public sealed record ModuleUsageRow(string ModuleCode, string ModuleLabel, long VisitCount, long UniqueUsers);
public sealed record ModuleTrendPoint(DateTime Bucket, string ModuleCode, string ModuleLabel, long VisitCount);

// ── Adoption Dashboard — Reports Generation tab ─────────────────────────────

public sealed class ReportGenerationEvent
{
    public string ReportKey { get; set; } = "";
    public string? ReportLabel { get; set; }
    public string EmpCode { get; set; } = "";
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string? BranchSolId { get; set; }
    public string? RegionSolId { get; set; }
    public string? ZoneSolId { get; set; }
}

public sealed record ReportGenerationRow(string ReportKey, string? ReportLabel, long GenCount, long UniqueUsers);
public sealed record ReportGenerationTrendPoint(DateTime Bucket, long GenCount);

// ── Adoption Dashboard — Insights tab (plain aggregation, no ML) ────────────

public sealed record AdoptionInsight(string Title, string Detail, string Severity); // Severity: Info|Warning|Success
