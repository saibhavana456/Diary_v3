using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

/// <summary>Exact port of Annexure4.razor's SaveDraft/SubmitFinal/ProcessSave (Branch level only — RO/ZO
/// consolidation views come in a later phase). Status literals ('98' Draft, '101' Submitted, '105'
/// Returned) are unchanged from the source app. The only NEW behaviour relative to source is
/// RequireBranchRoleAsync — the source app had no server-side role check at all (any authenticated user
/// with a USER_MASTER/job-code-derived Branch type could submit); this now uses my_diary_v3_1's own RBAC
/// (RoleCode.BranchUser/BranchHead, or App/Super Admin), matching the Deposits workflow's convention.</summary>
public sealed class ReconAnnexure4Service : IReconAnnexure4Service
{
    private readonly IReconAnnexure4Repo _repo;
    private readonly IUserRepo _users;
    private readonly IRbacRepo _rbac;
    private readonly IReconOrgMasterRepo _orgMaster;

    public ReconAnnexure4Service(IReconAnnexure4Repo repo, IUserRepo users, IRbacRepo rbac, IReconOrgMasterRepo orgMaster)
    {
        _repo = repo;
        _users = users;
        _rbac = rbac;
        _orgMaster = orgMaster;
    }

    public Task<ReconAnnexure4?> GetBranchEntryAsync(string actorEmpCode, string branchCode, string reportMonth, string reportYear, CancellationToken ct = default) =>
        _repo.GetBranchEntryAsync(branchCode, reportMonth, reportYear, ct);

    /// <summary>ProcessSave("98") — draft. Same "Branch Name required" guard as source; everything else
    /// (report id generation, resubmit detection, transactional child-row replace) lives in the repo.</summary>
    public async Task SaveDraftAsync(string actorEmpCode, ReconAnnexure4 entry, CancellationToken ct = default)
    {
        await RequireBranchRoleAsync(actorEmpCode, ct);

        if (string.IsNullOrWhiteSpace(entry.BranchName))
            throw new ReconValidationException("Branch Name is required to save a draft.");

        await ProcessSaveAsync(actorEmpCode, entry, "98", ct);
    }

    /// <summary>ProcessSave("101") — submit (locks the form). Mirrors SubmitFinal's exact "Branch Name
    /// required" guard; the source app's client-side EditContext.Validate() and the "form mostly empty,
    /// confirm?" dialog are page-level concerns, not ported into this service.</summary>
    public async Task SubmitAsync(string actorEmpCode, ReconAnnexure4 entry, CancellationToken ct = default)
    {
        await RequireBranchRoleAsync(actorEmpCode, ct);

        if (string.IsNullOrWhiteSpace(entry.BranchName))
            throw new ReconValidationException("Branch Name is required to submit.");

        entry.Status = "101";
        await ProcessSaveAsync(actorEmpCode, entry, "101", ct);
    }

    private async Task ProcessSaveAsync(string actorEmpCode, ReconAnnexure4 entry, string status, CancellationToken ct)
    {
        var originalStatus = entry.Status;
        var isEditMode = !string.IsNullOrEmpty(entry.ReportId);

        entry.Status = status;
        entry.EntryBy ??= actorEmpCode;
        entry.ReportMonth ??= DateTime.Now.AddMonths(-1).ToString("MMMM");
        entry.ReportYear ??= DateTime.Now.AddMonths(-1).ToString("yyyy");

        if (!isEditMode)
        {
            entry.ReportId = DateTime.Now.ToString("yyMMddhhmmssfff");
            entry.EntryDate = DateTime.Now;
            entry.DelFlag = "N";
        }
        else if (entry.Status == "105" || !string.IsNullOrWhiteSpace(entry.ModifyBy))
        {
            entry.ModifyBy = actorEmpCode;
            entry.ModifyDate = DateTime.Now;
        }

        AssignDefaultsForChildren(entry);

        await _repo.SaveAsync(entry, isEditMode, ct);

        if (status == "98")
        {
            await _repo.AppendActionHistoryAsync(new ReconA4ActionHistory
            {
                ReportId = entry.ReportId, ActionLevel = "BRANCH", ActionType = "SAVE_DRAFT",
                UserId = actorEmpCode, StatusAfter = entry.Status
            }, ct);
        }
        else if (status == "101")
        {
            var isResubmit = originalStatus == "105"; // 105 = Returned to Branch
            await _repo.AppendActionHistoryAsync(new ReconA4ActionHistory
            {
                ReportId = entry.ReportId, ActionLevel = "BRANCH", ActionType = isResubmit ? "RESUBMIT" : "SUBMIT",
                UserId = actorEmpCode, StatusAfter = entry.Status
            }, ct);
        }
    }

    /// <summary>Exact port of Annexure4.razor's AssignDefaultsForChildren — every child row gets the
    /// parent's REPORT_ID plus the same "N/A"/0/now defaults for anything left blank.</summary>
    private static void AssignDefaultsForChildren(ReconAnnexure4 e)
    {
        foreach (var item in e.BreakupEntries)
        {
            item.ReportId = e.ReportId;
            item.BranchName ??= "N/A"; item.InterBankName ??= "N/A"; item.TransactionNatureDetails ??= "N/A";
            item.OurDebitAmount ??= 0; item.OurCreditAmount ??= 0; item.TheirDebitAmount ??= 0; item.TheirCreditAmount ??= 0;
            item.DateOfEntry ??= DateTime.Now;
        }
        foreach (var item in e.SundryEntries.Concat(e.SuspenseEntries).Concat(e.PobEntries))
        {
            item.ReportId = e.ReportId;
            item.NatureOfEntry ??= "N/A"; item.StatusReasonPendency ??= "N/A"; item.OutstandingAmountRupees ??= 0;
            item.OriginatedDate ??= DateTime.Now; item.ExpectedReversalDate ??= DateTime.Now.AddMonths(1);
        }
        foreach (var item in e.BooksNotTallied)
        {
            item.ReportId = e.ReportId;
            item.BranchName ??= "N/A"; item.HeadsNotTallied ??= "N/A"; item.ActionInitiatedReconExpDate ??= "N/A"; item.Reason ??= "N/A";
            item.DifferenceAmountRupees ??= 0; item.NotTalliedSinceDate ??= DateTime.Now;
        }
        foreach (var item in e.BrPositionReconciled)
        {
            item.ReportId = e.ReportId; item.IbrAccountBankBranch ??= "N/A"; item.IbrAccountBank ??= "N/A"; item.ReconciliationStatus = "Reconciled";
        }
        foreach (var item in e.BrPositionNotReconciled)
        {
            item.ReportId = e.ReportId; item.IbrAccountBankBranch ??= "N/A"; item.IbrAccountBank ??= "N/A"; item.ReconciliationStatus = "Not Reconciled";
        }
    }

    /// <summary>Exact port of Annexure4C.razor's OnInitializedAsync (user_type "102" branch) + SetData —
    /// pending branches (haven't submitted 101/106) and the live-aggregated totals over those who have.</summary>
    public async Task<ReconRoOperationsView> GetRoOperationsViewAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireRoRoleAsync(actorEmpCode, ct);

        var branches = await _orgMaster.ListBranchesInRegionAsync(regionSolId, ct);
        var branchCodes = branches.Select(b => b.SolId).ToHashSet();
        var submittedStatus = new[] { "101", "106" };
        var submissions = await _repo.ListSubmissionsAsync(reportMonth, reportYear, branchCodes, submittedStatus, ct);
        var submittedCodes = submissions.Select(s => s.BranchCode).ToHashSet();

        var view = new ReconRoOperationsView();
        foreach (var branch in branches)
        {
            if (submittedCodes.Contains(branch.SolId)) continue;
            var existing = submissions.FirstOrDefault(s => s.BranchCode == branch.SolId);
            view.PendingBranches.Add((branch.SolId, branch.BranchName ?? "", existing?.Status ?? "Not Submitted"));
        }

        view.Aggregated = AggregateSubmissions(submissions);
        return view;
    }

    /// <summary>Exact port of Annexure4C.razor's ForwardtoChecker.</summary>
    public async Task ForwardToRoCheckerAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireRoRoleAsync(actorEmpCode, ct);

        var branches = await _orgMaster.ListBranchesInRegionAsync(regionSolId, ct);
        var branchCodes = branches.Select(b => b.SolId).ToHashSet();
        var submittedStatus = new[] { "101", "106" };
        var submissions = await _repo.ListSubmissionsAsync(reportMonth, reportYear, branchCodes, submittedStatus, ct);
        var submittedCodes = submissions.Select(s => s.BranchCode).ToHashSet();

        if (branches.Any(b => !submittedCodes.Contains(b.SolId)))
            throw new ReconValidationException("Cannot forward: Pending branches still exist.");
        if (submissions.Count == 0)
            throw new ReconValidationException("No submissions to forward.");

        var aggregated = AggregateSubmissions(submissions);
        var existing = await _repo.GetConsolidatedAsync("RO", regionSolId, reportMonth, reportYear, ct);

        aggregated.Status = "102";
        aggregated.RoOperationsUserId = actorEmpCode;
        aggregated.RoOperationsActionDate = DateTime.Now;
        aggregated.ReportType = "RO";
        aggregated.ReportMonth = reportMonth;
        aggregated.ReportYear = reportYear;
        aggregated.BranchCode = regionSolId;

        if (existing is not null)
        {
            aggregated.ReportId = existing.ReportId;
            await _repo.UpsertConsolidatedAsync(aggregated, exists: true, ct);
        }
        else
        {
            aggregated.ReportId = $"RO_{regionSolId}_{reportMonth}{reportYear}_{DateTime.Now:yyyyMMddHHmmss}";
            aggregated.BranchName = "(Consolidated)";
            aggregated.EntryBy = actorEmpCode;
            aggregated.EntryDate = DateTime.Now;
            await _repo.UpsertConsolidatedAsync(aggregated, exists: false, ct);
        }

        await _repo.MarkBranchesForwardedToRoAsync(submissions.Select(s => s.ReportId).ToList(), actorEmpCode, ct);

        foreach (var s in submissions)
            await _repo.AppendActionHistoryAsync(new ReconA4ActionHistory
            { ReportId = s.ReportId, ActionLevel = "RO_OPERATIONS", ActionType = "FORWARD", UserId = actorEmpCode, StatusAfter = "102" }, ct);
        await _repo.AppendActionHistoryAsync(new ReconA4ActionHistory
        { ReportId = aggregated.ReportId, ActionLevel = "RO_OPERATIONS", ActionType = "FORWARD", UserId = actorEmpCode, StatusAfter = "102" }, ct);
    }

    /// <summary>Exact port of Annexure4C.razor's user_type "103" branch of OnInitializedAsync.</summary>
    public async Task<ReconAnnexure4?> GetRoCheckerViewAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireRoRoleAsync(actorEmpCode, ct);

        var roRecord = await _repo.GetConsolidatedAsync("RO", regionSolId, reportMonth, reportYear, ct);
        if (roRecord is null || (roRecord.Status != "102" && roRecord.Status != "106")) return null;
        return roRecord;
    }

    /// <summary>Exact port of Annexure4C.razor's ForwardtoZo.</summary>
    public async Task ForwardToZoAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireRoRoleAsync(actorEmpCode, ct);

        var roRecord = await _repo.GetConsolidatedAsync("RO", regionSolId, reportMonth, reportYear, ct);
        if (roRecord is null || (roRecord.Status != "102" && roRecord.Status != "106"))
            throw new ReconValidationException("No consolidated RO record found to forward.");

        await _repo.MarkConsolidatedForwardedToZoAsync(roRecord.ReportId, actorEmpCode, ct);
        await _repo.AppendActionHistoryAsync(new ReconA4ActionHistory
        { ReportId = roRecord.ReportId, ActionLevel = "RO_CHECKER", ActionType = "APPROVE", UserId = actorEmpCode, StatusAfter = "103" }, ct);
    }

    /// <summary>Exact port of Annexure5B.razor's user_type "104" branch of SetData/OnInitializedAsync.</summary>
    public async Task<ReconZoOperationsView> GetZoOperationsViewAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireZoRoleAsync(actorEmpCode, ct);

        var regionCodes = (await _orgMaster.ListRegionCodesInZoneAsync(zoneSolId, ct)).ToList();
        var submittedStatus = new[] { "103" };
        var roRecords = await _repo.ListConsolidatedAsync("RO", regionCodes, reportMonth, reportYear, submittedStatus, ct);
        var submittedRegionCodes = roRecords.Select(r => r.BranchCode).ToHashSet();

        var view = new ReconZoOperationsView();
        var pendingCodes = regionCodes.Where(rc => !submittedRegionCodes.Contains(rc)).ToList();
        var pendingNames = await _orgMaster.GetRegionNamesAsync(pendingCodes, ct);
        view.PendingRegions = pendingNames.Select(p => (p.Code, p.Name)).ToList();

        view.Aggregated = AggregateSubmissions(roRecords);
        return view;
    }

    /// <summary>Exact port of Annexure5B.razor's ForwardtoChecker (ZO level).</summary>
    public async Task ForwardToZoCheckerAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireZoRoleAsync(actorEmpCode, ct);

        var regionCodes = (await _orgMaster.ListRegionCodesInZoneAsync(zoneSolId, ct)).ToList();
        var roRecords = await _repo.ListConsolidatedAsync("RO", regionCodes, reportMonth, reportYear, new[] { "103" }, ct);
        var submittedRegionCodes = roRecords.Select(r => r.BranchCode).ToHashSet();

        if (regionCodes.Any(rc => !submittedRegionCodes.Contains(rc)))
            throw new ReconValidationException("Cannot forward: Pending ROs still exist.");
        if (roRecords.Count == 0)
            throw new ReconValidationException("No RO submissions to forward.");

        var aggregated = AggregateSubmissions(roRecords);
        var existing = await _repo.GetConsolidatedAsync("ZO", zoneSolId, reportMonth, reportYear, ct);

        aggregated.Status = "104";
        aggregated.ZoOperationsUserId = actorEmpCode;
        aggregated.ZoOperationsActionDate = DateTime.Now;
        aggregated.ReportType = "ZO";
        aggregated.ReportMonth = reportMonth;
        aggregated.ReportYear = reportYear;
        aggregated.BranchCode = zoneSolId;

        if (existing is not null)
        {
            aggregated.ReportId = existing.ReportId;
            await _repo.UpsertConsolidatedAsync(aggregated, exists: true, ct);
        }
        else
        {
            aggregated.ReportId = $"ZO_{zoneSolId}_{reportMonth}{reportYear}_{DateTime.Now:yyyyMMddHHmmss}";
            aggregated.BranchName = "(Zone Consolidated)";
            aggregated.EntryBy = actorEmpCode;
            aggregated.EntryDate = DateTime.Now;
            await _repo.UpsertConsolidatedAsync(aggregated, exists: false, ct);
        }

        await _repo.MarkRoRecordsForwardedToZoAsync(roRecords.Select(r => r.ReportId).ToList(), actorEmpCode, ct);

        foreach (var r in roRecords)
            await _repo.AppendActionHistoryAsync(new ReconA4ActionHistory
            { ReportId = r.ReportId, ActionLevel = "ZO_OPERATIONS", ActionType = "FORWARD", UserId = actorEmpCode, StatusAfter = "104" }, ct);
        await _repo.AppendActionHistoryAsync(new ReconA4ActionHistory
        { ReportId = aggregated.ReportId, ActionLevel = "ZO_OPERATIONS", ActionType = "FORWARD", UserId = actorEmpCode, StatusAfter = "104" }, ct);
    }

    /// <summary>Exact port of Annexure5B.razor's user_type "105" branch.</summary>
    public async Task<ReconAnnexure4?> GetZoCheckerViewAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireZoRoleAsync(actorEmpCode, ct);

        var zoRecord = await _repo.GetConsolidatedAsync("ZO", zoneSolId, reportMonth, reportYear, ct);
        if (zoRecord is null || (zoRecord.Status != "104" && zoRecord.Status != "106")) return null;
        return zoRecord;
    }

    /// <summary>Exact port of Annexure5B.razor's ForwardtoRecon.</summary>
    public async Task ForwardToReconAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireZoRoleAsync(actorEmpCode, ct);

        var zoRecord = await _repo.GetConsolidatedAsync("ZO", zoneSolId, reportMonth, reportYear, ct);
        if (zoRecord is null || (zoRecord.Status != "104" && zoRecord.Status != "106"))
            throw new ReconValidationException("No consolidated ZO record found to forward.");

        await _repo.MarkConsolidatedForwardedToReconAsync(zoRecord.ReportId, actorEmpCode, ct);
        await _repo.AppendActionHistoryAsync(new ReconA4ActionHistory
        { ReportId = zoRecord.ReportId, ActionLevel = "ZO_CHECKER", ActionType = "APPROVE", UserId = actorEmpCode, StatusAfter = "100" }, ct);
    }

    public async Task<IReadOnlyList<(string Code, string Name, string? Head, string? Mobile, string? Ip)>> GetBranchRosterAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireRoRoleAsync(actorEmpCode, ct);
        var branches = await _orgMaster.ListBranchesInRegionAsync(regionSolId, ct);
        var submissions = await _repo.ListSubmissionsAsync(reportMonth, reportYear, branches.Select(b => b.SolId).ToList(),
            new[] { "101", "102", "103", "104", "106" }, ct);
        return branches.Select(b =>
        {
            var s = submissions.FirstOrDefault(x => x.BranchCode == b.SolId);
            return (b.SolId, b.BranchName ?? "", s?.BranchHead, s?.MobileNo, s?.IpNo);
        }).ToList();
    }

    public async Task<IReadOnlyList<(string Code, string Name, string? Head, string? Mobile, string? Ip)>> GetRegionRosterAsync(string actorEmpCode, string zoneSolId, CancellationToken ct = default)
    {
        await RequireZoRoleAsync(actorEmpCode, ct);
        var regionCodes = await _orgMaster.ListRegionCodesInZoneAsync(zoneSolId, ct);
        var names = await _orgMaster.GetRegionNamesAsync(regionCodes.ToList(), ct);
        return names.Select(n => (n.Code, n.Name, (string?)null, (string?)null, (string?)null)).ToList();
    }

    public async Task<int> GetUnbalancedBranchCountAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireRoRoleAsync(actorEmpCode, ct);
        var branches = await _orgMaster.ListBranchesInRegionAsync(regionSolId, ct);
        return await _repo.CountUnbalancedBranchesAsync(reportMonth, reportYear, branches.Select(b => b.SolId).ToList(), ct);
    }

    public async Task<int> GetUnbalancedBranchCountForZoneAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireZoRoleAsync(actorEmpCode, ct);
        var branches = await _orgMaster.ListBranchesInZoneAsync(zoneSolId, ct);
        return await _repo.CountUnbalancedBranchesAsync(reportMonth, reportYear, branches.Select(b => b.SolId).ToList(), ct);
    }

    public async Task<IReadOnlyList<ReconA4InterbankBreakup>> GetInterbankBreakupForRegionAsync(string actorEmpCode, string regionSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireRoRoleAsync(actorEmpCode, ct);
        var branches = await _orgMaster.ListBranchesInRegionAsync(regionSolId, ct);
        return await _repo.ListInterbankBreakupForBranchesAsync(reportMonth, reportYear, branches.Select(b => b.SolId).ToList(), ct);
    }

    public async Task<IReadOnlyList<ReconA4InterbankBreakup>> GetInterbankBreakupForZoneAsync(string actorEmpCode, string zoneSolId, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await RequireZoRoleAsync(actorEmpCode, ct);
        var branches = await _orgMaster.ListBranchesInZoneAsync(zoneSolId, ct);
        return await _repo.ListInterbankBreakupForBranchesAsync(reportMonth, reportYear, branches.Select(b => b.SolId).ToList(), ct);
    }

    /// <summary>ZO Operations/Checker are both ZoUser/ZoHead in my_diary_v3_1's RBAC (same convention as
    /// RO's user_type 102/103 collapse onto RoUser/RoHead) — App/Super Admin always bypass.</summary>
    private async Task RequireZoRoleAsync(string actorEmpCode, CancellationToken ct)
    {
        if (await _rbac.IsSuperAdminAsync(actorEmpCode, ct)) return;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(actorEmpCode, ct);
        if (appAdmin is { IsActive: true }) return;

        var roles = await _users.GetEffectiveRolesAsync(actorEmpCode, ct);
        if (roles.Contains(RoleCode.ZoUser) || roles.Contains(RoleCode.ZoHead)) return;

        throw new ReconAuthorizationException("This action is restricted to ZO desk users (or App/Super Admin).");
    }

    /// <summary>Exact port of Annexure4C.razor's SetData — sums every numeric field across the given
    /// branch submissions, and semicolon-joins the non-empty reason/effort text fields.</summary>
    private static ReconAnnexure4 AggregateSubmissions(IReadOnlyList<ReconAnnexure4> s)
    {
        string? Join(Func<ReconAnnexure4, string?> f) => string.Join("; ", s.Select(f).Where(v => !string.IsNullOrEmpty(v)));

        return new ReconAnnexure4
        {
            OurDebitEntries1M = s.Sum(x => x.OurDebitEntries1M), OurDebitAmount1M = s.Sum(x => x.OurDebitAmount1M),
            OurCreditEntries1M = s.Sum(x => x.OurCreditEntries1M), OurCreditAmount1M = s.Sum(x => x.OurCreditAmount1M),
            TheirDebitEntries1M = s.Sum(x => x.TheirDebitEntries1M), TheirDebitAmount1M = s.Sum(x => x.TheirDebitAmount1M),
            TheirCreditEntries1M = s.Sum(x => x.TheirCreditEntries1M), TheirCreditAmount1M = s.Sum(x => x.TheirCreditAmount1M),
            OurDebitEntries3M = s.Sum(x => x.OurDebitEntries3M), OurDebitAmount3M = s.Sum(x => x.OurDebitAmount3M),
            OurCreditEntries3M = s.Sum(x => x.OurCreditEntries3M), OurCreditAmount3M = s.Sum(x => x.OurCreditAmount3M),
            TheirDebitEntries3M = s.Sum(x => x.TheirDebitEntries3M), TheirDebitAmount3M = s.Sum(x => x.TheirDebitAmount3M),
            TheirCreditEntries3M = s.Sum(x => x.TheirCreditEntries3M), TheirCreditAmount3M = s.Sum(x => x.TheirCreditAmount3M),
            OurDebitEntries6M = s.Sum(x => x.OurDebitEntries6M), OurDebitAmount6M = s.Sum(x => x.OurDebitAmount6M),
            OurCreditEntries6M = s.Sum(x => x.OurCreditEntries6M), OurCreditAmount6M = s.Sum(x => x.OurCreditAmount6M),
            TheirDebitEntries6M = s.Sum(x => x.TheirDebitEntries6M), TheirDebitAmount6M = s.Sum(x => x.TheirDebitAmount6M),
            TheirCreditEntries6M = s.Sum(x => x.TheirCreditEntries6M), TheirCreditAmount6M = s.Sum(x => x.TheirCreditAmount6M),
            OurDebitEntries1Y = s.Sum(x => x.OurDebitEntries1Y), OurDebitAmount1Y = s.Sum(x => x.OurDebitAmount1Y),
            OurCreditEntries1Y = s.Sum(x => x.OurCreditEntries1Y), OurCreditAmount1Y = s.Sum(x => x.OurCreditAmount1Y),
            TheirDebitEntries1Y = s.Sum(x => x.TheirDebitEntries1Y), TheirDebitAmount1Y = s.Sum(x => x.TheirDebitAmount1Y),
            TheirCreditEntries1Y = s.Sum(x => x.TheirCreditEntries1Y), TheirCreditAmount1Y = s.Sum(x => x.TheirCreditAmount1Y),
            OurDebitEntriesAbove1Y = s.Sum(x => x.OurDebitEntriesAbove1Y), OurDebitAmountAbove1Y = s.Sum(x => x.OurDebitAmountAbove1Y),
            OurCreditEntriesAbove1Y = s.Sum(x => x.OurCreditEntriesAbove1Y), OurCreditAmountAbove1Y = s.Sum(x => x.OurCreditAmountAbove1Y),
            TheirDebitEntriesAbove1Y = s.Sum(x => x.TheirDebitEntriesAbove1Y), TheirDebitAmountAbove1Y = s.Sum(x => x.TheirDebitAmountAbove1Y),
            TheirCreditEntriesAbove1Y = s.Sum(x => x.TheirCreditEntriesAbove1Y), TheirCreditAmountAbove1Y = s.Sum(x => x.TheirCreditAmountAbove1Y),

            SunUpto3MEntries = s.Sum(x => x.SunUpto3MEntries), SunUpto3MAmount = s.Sum(x => x.SunUpto3MAmount),
            SunThree6MEntries = s.Sum(x => x.SunThree6MEntries), SunThree6MAmount = s.Sum(x => x.SunThree6MAmount),
            SunOver6MEntries = s.Sum(x => x.SunOver6MEntries), SunOver6MAmount = s.Sum(x => x.SunOver6MAmount),
            SunNewOver6MEntries = s.Sum(x => x.SunNewOver6MEntries), SunNewOver6MAmount = s.Sum(x => x.SunNewOver6MAmount),
            SunReason = Join(x => x.SunReason), SunEfforts = Join(x => x.SunEfforts),

            SusUpto3MEntries = s.Sum(x => x.SusUpto3MEntries), SusUpto3MAmount = s.Sum(x => x.SusUpto3MAmount),
            SusThree6MEntries = s.Sum(x => x.SusThree6MEntries), SusThree6MAmount = s.Sum(x => x.SusThree6MAmount),
            SusOver6MEntries = s.Sum(x => x.SusOver6MEntries), SusOver6MAmount = s.Sum(x => x.SusOver6MAmount),
            SusNewOver6MEntries = s.Sum(x => x.SusNewOver6MEntries), SusNewOver6MAmount = s.Sum(x => x.SusNewOver6MAmount),
            SusReason = Join(x => x.SusReason), SusEfforts = Join(x => x.SusEfforts),

            PobUpto3MEntries = s.Sum(x => x.PobUpto3MEntries), PobUpto3MAmount = s.Sum(x => x.PobUpto3MAmount),
            PobThree6MEntries = s.Sum(x => x.PobThree6MEntries), PobThree6MAmount = s.Sum(x => x.PobThree6MAmount),
            PobOver6MEntries = s.Sum(x => x.PobOver6MEntries), PobOver6MAmount = s.Sum(x => x.PobOver6MAmount),
            PobNewOver6MEntries = s.Sum(x => x.PobNewOver6MEntries), PobNewOver6MAmount = s.Sum(x => x.PobNewOver6MAmount),
            PobReason = Join(x => x.PobReason), PobEfforts = Join(x => x.PobEfforts),

            IbkUpto3MEntries = s.Sum(x => x.IbkUpto3MEntries), IbkUpto3MAmount = s.Sum(x => x.IbkUpto3MAmount),
            IbkThree6MEntries = s.Sum(x => x.IbkThree6MEntries), IbkThree6MAmount = s.Sum(x => x.IbkThree6MAmount),
            IbkOver6MEntries = s.Sum(x => x.IbkOver6MEntries), IbkOver6MAmount = s.Sum(x => x.IbkOver6MAmount),
            IbkNewOver6MEntries = s.Sum(x => x.IbkNewOver6MEntries), IbkNewOver6MAmount = s.Sum(x => x.IbkNewOver6MAmount),
            IbkReason = Join(x => x.IbkReason), IbkEfforts = Join(x => x.IbkEfforts),

            BrMaintainIbAcc = s.Count(x => x.BrMaintainIbAcc.HasValue && x.BrMaintainIbAcc.Value > 0),
            BrMaintainIbAccNotRecon = s.Count(x => x.BrMaintainIbAccNotRecon.HasValue && x.BrMaintainIbAccNotRecon.Value > 0),
        };
    }

    /// <summary>RO Operations/Checker are both RoUser/RoHead in my_diary_v3_1's RBAC (the source app's
    /// user_type 102/103 split doesn't have an equivalent tier here) — App/Super Admin always bypass.</summary>
    private async Task RequireRoRoleAsync(string actorEmpCode, CancellationToken ct)
    {
        if (await _rbac.IsSuperAdminAsync(actorEmpCode, ct)) return;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(actorEmpCode, ct);
        if (appAdmin is { IsActive: true }) return;

        var roles = await _users.GetEffectiveRolesAsync(actorEmpCode, ct);
        if (roles.Contains(RoleCode.RoUser) || roles.Contains(RoleCode.RoHead)) return;

        throw new ReconAuthorizationException("This action is restricted to RO desk users (or App/Super Admin).");
    }

    private async Task RequireBranchRoleAsync(string actorEmpCode, CancellationToken ct)
    {
        if (await _rbac.IsSuperAdminAsync(actorEmpCode, ct)) return;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(actorEmpCode, ct);
        if (appAdmin is { IsActive: true }) return;

        var roles = await _users.GetEffectiveRolesAsync(actorEmpCode, ct);
        if (roles.Contains(RoleCode.BranchUser) || roles.Contains(RoleCode.BranchHead)) return;

        throw new ReconAuthorizationException("This action is restricted to Branch desk users (or App/Super Admin).");
    }
}

public sealed class ReconValidationException : Exception
{
    public ReconValidationException(string message) : base(message) { }
}

public sealed class ReconAuthorizationException : Exception
{
    public ReconAuthorizationException(string message) : base(message) { }
}
