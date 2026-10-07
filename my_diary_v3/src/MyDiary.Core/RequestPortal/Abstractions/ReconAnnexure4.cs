using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

public interface IReconAnnexure4Repo
{
    /// <summary>Branch's existing row for a given month/year (REPORT_TYPE='BRANCH'), including all 6
    /// child breakup collections — mirrors Annexure4.razor's OnAfterRenderAsync load exactly.</summary>
    Task<ReconAnnexure4?> GetBranchEntryAsync(string branchCode, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Inserts a new row (new ReportId) or replaces an existing one's children in-place —
    /// exact same "delete all children, re-insert" strategy as the source app's ProcessSave, all inside
    /// one transaction so a partial write can never leave orphaned child rows.</summary>
    Task SaveAsync(ReconAnnexure4 entry, bool isEditMode, CancellationToken ct = default);

    Task AppendActionHistoryAsync(ReconA4ActionHistory action, CancellationToken ct = default);
    Task<IReadOnlyList<ReconA4ActionHistory>> GetActionHistoryAsync(string reportId, CancellationToken ct = default);

    // ── RO/ZO consolidation (Annexure4C.razor's ForwardtoChecker/ForwardtoZo) ──────────────────────

    /// <summary>Branch-level rows for the given period/branch set with STATUS in the given set — exact
    /// port of Annexure4C.razor's submissions query used by SetData/ForwardtoChecker.</summary>
    Task<IReadOnlyList<ReconAnnexure4>> ListSubmissionsAsync(string reportMonth, string reportYear, IReadOnlyCollection<string> branchCodes, IReadOnlyCollection<string> statuses, CancellationToken ct = default);

    /// <summary>The consolidated row for a given REPORT_TYPE ('RO'/'ZO') and owning region/zone code
    /// (stored in BRANCH_CODE, exactly as source overloads that column) — exact port of the
    /// REPORT_TYPE=='RO'/'ZO' && BRANCH_CODE==ownerCode queries used throughout Annexure4A/4C/ReconRO/ReconZO.</summary>
    Task<ReconAnnexure4?> GetConsolidatedAsync(string reportType, string ownerCode, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Insert-or-update the consolidated row's header/aggregate columns only (it has no child
    /// rows — exact port of CopyEntityToRecord + the AddAsync/Attach-Modified branch in ForwardtoChecker).</summary>
    Task UpsertConsolidatedAsync(ReconAnnexure4 consolidated, bool exists, CancellationToken ct = default);

    /// <summary>Sets STATUS + the RO_OPERATIONS_USERID/ACTIONDATE pair on every given branch REPORT_ID —
    /// exact port of ForwardtoChecker's "update all underlying branch records" loop.</summary>
    Task MarkBranchesForwardedToRoAsync(IReadOnlyCollection<string> reportIds, string actorEmpCode, CancellationToken ct = default);

    /// <summary>Sets STATUS='103' + RO_CHECKER_USERID/ACTIONDATE on the consolidated RO row — exact port
    /// of ForwardtoZo.</summary>
    Task MarkConsolidatedForwardedToZoAsync(string reportId, string actorEmpCode, CancellationToken ct = default);

    /// <summary>Consolidated rows (REPORT_TYPE='RO' or 'ZO') owned by any of the given codes with STATUS
    /// in the given set — exact port of Annexure5B.razor's roRecords query (regionCodes.Contains(BRANCH_CODE)).</summary>
    Task<IReadOnlyList<ReconAnnexure4>> ListConsolidatedAsync(string reportType, IReadOnlyCollection<string> ownerCodes, string reportMonth, string reportYear, IReadOnlyCollection<string> statuses, CancellationToken ct = default);

    /// <summary>Sets STATUS + ZO_OPERATIONS_USERID/ACTIONDATE on a batch of RO consolidated rows — exact
    /// port of Annexure5B.razor's ForwardtoChecker "update RO records to status 104" loop.</summary>
    Task MarkRoRecordsForwardedToZoAsync(IReadOnlyCollection<string> reportIds, string actorEmpCode, CancellationToken ct = default);

    /// <summary>Sets STATUS='100' + ZO_CHECKER_USERID/ACTIONDATE on the consolidated ZO row — exact port
    /// of Annexure5B.razor's ForwardtoRecon.</summary>
    Task MarkConsolidatedForwardedToReconAsync(string reportId, string actorEmpCode, CancellationToken ct = default);

    /// <summary>Exact port of the source's `roRecord.BooksNotTallied.Select(b => b.REPORT_ID).Distinct().Count()` —
    /// the number of distinct branch submissions (within the given branch set/period) that have at least
    /// one Books Not Tallied row.</summary>
    Task<int> CountUnbalancedBranchesAsync(string reportMonth, string reportYear, IReadOnlyCollection<string> branchCodes, CancellationToken ct = default);

    /// <summary>Exact port of the "Outstanding entry wise break-up more than three months" listing (source's
    /// Annexure4C/5B Section B) — every ANNEXURE4_INTERBANK_BREAKUP row belonging to any branch submission
    /// in the given set/period, exactly as GenerateAnnexure4ABCPdf's caller populates roRecord.BreakupEntries
    /// before rendering (the consolidated row itself never stores this data).</summary>
    Task<IReadOnlyList<ReconA4InterbankBreakup>> ListInterbankBreakupForBranchesAsync(string reportMonth, string reportYear, IReadOnlyCollection<string> branchCodes, CancellationToken ct = default);
}

/// <summary>Branch-level Annexure 4 submit/draft logic — exact port of Annexure4.razor's SaveDraft/
/// SubmitFinal/ProcessSave. Server-side role enforcement (RequireRoleForLevelAsync) is NEW relative to the
/// source app (which had no such check at all) — added per the same convention as
/// BackDatedDepositWorkflowService, since this is now gated by my_diary_v3_1's own RBAC rather than
/// Recon's original USER_MASTER/AD login.</summary>
public interface IReconAnnexure4Service
{
    Task<ReconAnnexure4?> GetBranchEntryAsync(string actorEmpCode, string branchCode, string reportMonth, string reportYear, CancellationToken ct = default);
    Task SaveDraftAsync(string actorEmpCode, ReconAnnexure4 entry, CancellationToken ct = default);
    Task SubmitAsync(string actorEmpCode, ReconAnnexure4 entry, CancellationToken ct = default);

    /// <summary>RO Operations dashboard data — exact port of Annexure4C.razor's pendingBranches/SetData
    /// for user_type "102".</summary>
    Task<ReconRoOperationsView> GetRoOperationsViewAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Exact port of Annexure4C.razor's ForwardtoChecker — blocked if any branch is still
    /// pending; aggregates all submitted branches into the RO consolidated row and advances everything
    /// (branch rows + consolidated row) to STATUS='102'.</summary>
    Task ForwardToRoCheckerAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Exact port of Annexure4C.razor's ForwardtoZo — advances the RO consolidated row
    /// (STATUS 102/106) to STATUS='103'.</summary>
    Task ForwardToZoAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>RO Checker's view — exact port of Annexure4C.razor's user_type "103" branch of
    /// OnInitializedAsync: the consolidated RO row awaiting checker action (STATUS 102/106), or null if
    /// none exists yet (RO Operations hasn't forwarded).</summary>
    Task<ReconAnnexure4?> GetRoCheckerViewAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>ZO Operations dashboard — exact port of Annexure5B.razor's pendingROSummary/SetData for
    /// user_type "104" (aggregates RO consolidated rows with STATUS='103' across every region in the zone).</summary>
    Task<ReconZoOperationsView> GetZoOperationsViewAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Exact port of Annexure5B.razor's ForwardtoChecker (ZO level) — blocked if any region's RO
    /// hasn't been forwarded (STATUS=103); aggregates all RO rows into the ZO consolidated row and
    /// advances everything (RO rows + ZO consolidated row) to STATUS='104'.</summary>
    Task ForwardToZoCheckerAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>ZO Checker's view — exact port of Annexure5B.razor's user_type "105" branch: the
    /// consolidated ZO row awaiting checker action (STATUS 104/106), or null if none exists yet.</summary>
    Task<ReconAnnexure4?> GetZoCheckerViewAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Exact port of Annexure5B.razor's ForwardtoRecon — the terminal action: advances the ZO
    /// consolidated row (STATUS 104/106) to STATUS='100'.</summary>
    Task ForwardToReconAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Branch roster (code/name/head/mobile/ip) for the consolidated RO-level PDF's page 2 —
    /// every branch in the region with a submission this period, exact same fields as source's
    /// BranchDetailDto.</summary>
    Task<IReadOnlyList<(string Code, string Name, string? Head, string? Mobile, string? Ip)>> GetBranchRosterAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Region roster for the consolidated ZO-level PDF's page 2 — the regions under the zone
    /// (no source equivalent for Head/Mobile/IP at this level, always blank).</summary>
    Task<IReadOnlyList<(string Code, string Name, string? Head, string? Mobile, string? Ip)>> GetRegionRosterAsync(string actorEmpCode, string zoneSolId, CancellationToken ct = default);

    /// <summary>Unbalanced-branch count for the RO-level consolidated certificate — exact port of
    /// the source's roRecord.BooksNotTallied.Select(REPORT_ID).Distinct().Count(), over every branch in
    /// the region for the given period.</summary>
    Task<int> GetUnbalancedBranchCountAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Same, at ZO level — over every branch in every region of the zone.</summary>
    Task<int> GetUnbalancedBranchCountForZoneAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Interbank breakup detail listing (source's Annexure4C Section B) for every branch in the
    /// region, this period.</summary>
    Task<IReadOnlyList<ReconA4InterbankBreakup>> GetInterbankBreakupForRegionAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default);

    /// <summary>Same, at ZO level (source's Annexure5B Section B) — every branch in every region of the zone.</summary>
    Task<IReadOnlyList<ReconA4InterbankBreakup>> GetInterbankBreakupForZoneAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default);
}

public sealed class ReconZoOperationsView
{
    public List<(string RegionCode, string RegionName)> PendingRegions { get; set; } = new();
    public bool CanForward => PendingRegions.Count == 0;
    public ReconAnnexure4 Aggregated { get; set; } = new();
}

/// <summary>Everything the RO Operations page needs — exact shape of Annexure4C.razor's own state for
/// user_type "102" (pendingBranches list + the live-aggregated `entity` totals).</summary>
public sealed class ReconRoOperationsView
{
    public List<(string BranchCode, string BranchName, string Status)> PendingBranches { get; set; } = new();
    public bool CanForward => PendingBranches.Count == 0;
    public ReconAnnexure4 Aggregated { get; set; } = new();
}
