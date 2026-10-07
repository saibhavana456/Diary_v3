using RequestPortal.Core.Dtos;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

/// <summary>Page/menu usage tracking — backs the "Menu Access Dashboard" under
/// Access Management. Visits are logged fire-and-forget from MainLayout on
/// every RBAC-registered-route navigation; trend/drill-down reads hit the
/// daily aggregate (populated by the 2 AM job), never the raw log, so they
/// stay fast regardless of raw-log volume.</summary>
public interface IPageUsageRepo
{
    /// <summary>Fire-and-forget insert of one raw visit — callers should not await
    /// this on the render path; see MainLayout's LogPageVisit helper.</summary>
    Task InsertVisitAsync(PageVisit visit, CancellationToken ct = default);

    /// <summary>Usage trend, one point per menu item per bucket (day/month/quarter),
    /// optionally scoped to one org unit. Reads RP_PAGE_VISIT_DAILY_AGG.</summary>
    Task<IReadOnlyList<PageVisitTrendPoint>> GetTrendAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageTrendGranularity granularity,
        string? menuCode, UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default);

    /// <summary>Drill-down rows one level down from the given parent (e.g.
    /// drillLevel=Region, parentScopeId=&lt;a Zone's sol id&gt; → one row per Region
    /// in that Zone). Pass parentScopeId=null with drillLevel=Zone for the
    /// bank-wide, all-Zones view.</summary>
    Task<IReadOnlyList<PageUsageDrilldownRow>> GetDrilldownAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageScopeLevel drillLevel, string? parentScopeId, CancellationToken ct = default);

    /// <summary>Latest nightly underused-page ranking for one scope (Bank overall,
    /// or a specific Zone/Region/Branch sol id).</summary>
    Task<IReadOnlyList<UnderusedPageInsight>> GetUnderusedPagesAsync(
        UsageScopeLevel scopeLevel, string? scopeId, int take = 10, CancellationToken ct = default);

    /// <summary>The 2 AM job: rolls the given UTC date's raw visits into the daily
    /// aggregate, then recomputes the underused-page ranking snapshot for
    /// Bank + every Zone/Region/Branch with any activity in the last 30 days.
    /// Idempotent — safe to re-run for the same date.</summary>
    Task RunNightlyAggregationAsync(DateTime forDateUtc, CancellationToken ct = default);

    // ── Adoption Dashboard — Logins tab ──────────────────────────────────────

    /// <summary>Fire-and-forget insert of one login event — logged once per Blazor
    /// circuit from MainLayout, right where page-visit logging is initialised.</summary>
    Task InsertLoginAsync(LoginEvent login, CancellationToken ct = default);

    Task<IReadOnlyList<LoginTrendPoint>> GetLoginTrendAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageTrendGranularity granularity,
        UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default);

    /// <summary>Unique-login counts (from RP_LOGIN_DAILY_AGG) joined with headcount
    /// (from VW_STAFF_USER_SUMMARY) for adoption %, one row per org unit one level
    /// down from parentScopeId — same drill-down shape as GetDrilldownAsync.</summary>
    Task<IReadOnlyList<AdoptionRow>> GetAdoptionDrilldownAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageScopeLevel drillLevel, string? parentScopeId, CancellationToken ct = default);

    /// <summary>DAU/WAU/MAU + adoption % for the given scope, as of "now".</summary>
    Task<(long Dau, long Wau, long Mau, int? Headcount)> GetLoginSummaryAsync(
        UsageScopeLevel scopeLevel, string? scopeId, CancellationToken ct = default);

    // ── Adoption Dashboard — Modules Usage tab ───────────────────────────────

    Task<IReadOnlyList<ModuleUsageRow>> GetModuleUsageAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default);

    Task<IReadOnlyList<ModuleTrendPoint>> GetModuleTrendAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageTrendGranularity granularity,
        UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default);

    // ── Adoption Dashboard — Reports Generation tab ──────────────────────────

    /// <summary>Fire-and-forget insert of one report/export-generation event. Call
    /// this from any export/report button's click handler — reportKey is a short
    /// stable id (e.g. "adoption-dashboard", "dealers-master"), reportLabel is the
    /// human-readable name shown on the tab.</summary>
    Task InsertReportGenerationAsync(ReportGenerationEvent evt, CancellationToken ct = default);

    Task<IReadOnlyList<ReportGenerationRow>> GetReportGenerationSummaryAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default);

    Task<IReadOnlyList<ReportGenerationTrendPoint>> GetReportGenerationTrendAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageTrendGranularity granularity,
        string? reportKey, UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default);

    // ── Adoption Dashboard — Insights tab (plain aggregation, no ML) ─────────

    Task<IReadOnlyList<AdoptionInsight>> GetInsightsAsync(UsageScopeLevel scopeLevel, string? scopeId, CancellationToken ct = default);

    // ── Nightly aggregation (2 AM) — logins + report generations ────────────

    Task RunLoginAggregationAsync(DateTime forDateUtc, CancellationToken ct = default);
    Task RunReportGenerationAggregationAsync(DateTime forDateUtc, CancellationToken ct = default);

    // ── 9 AM digest — yesterday's headline adoption numbers ─────────────────

    Task<(long UniqueLogins, int? Headcount, string? TopModuleLabel, long TopModuleVisits)> GetDailyDigestSummaryAsync(
        DateTime forDateUtc, CancellationToken ct = default);
}

/// <summary>User Feedback — Home page popup → MIS "Feedback" (admin) /
/// "My Feedbacks" (submitter). See FeedbackModels.cs.</summary>
public interface IFeedbackRepo
{
    /// <summary>Inserts a new ticket with an auto-generated TicketNo (FDB-NNNNNN) and returns its id.</summary>
    Task<long> InsertTicketAsync(FeedbackTicket ticket, CancellationToken ct = default);

    /// <summary>All tickets submitted by the given employee (My Feedbacks).</summary>
    Task<IReadOnlyList<FeedbackTicket>> ListMyTicketsAsync(string empCode, FeedbackStatus? status = null, FeedbackCategory? category = null, CancellationToken ct = default);

    /// <summary>All tickets, for admin triage — optionally filtered.</summary>
    Task<IReadOnlyList<FeedbackTicket>> ListAllTicketsAsync(FeedbackStatus? status = null, FeedbackCategory? category = null, string? zone = null, string? region = null, CancellationToken ct = default);

    Task<FeedbackTicket?> GetTicketAsync(long id, CancellationToken ct = default);

    /// <summary>Admin/dev posts an update note and optionally changes severity/status/level (the
    /// escalation-matrix level, 1-5) — appended to the ticket's update log. Passing null for any
    /// of newStatus/newSeverity/newLevel leaves that field unchanged.</summary>
    Task AddUpdateAsync(long ticketId, string updateText, FeedbackStatus? newStatus, FeedbackSeverity? newSeverity, int? newLevel, string updatedByEmp, CancellationToken ct = default);

    /// <summary>Submitter rates a Closed ticket 1-5 stars, once.</summary>
    Task SubmitRatingAsync(long ticketId, int rating, string? comments, CancellationToken ct = default);

    Task<long> InsertAttachmentAsync(FeedbackAttachment attachment, CancellationToken ct = default);
    Task<IReadOnlyList<FeedbackAttachment>> ListAttachmentsAsync(long ticketId, CancellationToken ct = default);

    /// <summary>Aggregate counts for the Feedback Dashboard.</summary>
    Task<FeedbackDashboardStats> GetDashboardStatsAsync(CancellationToken ct = default);
}

/// <summary>Auto-fills the Feedback submission page's identity/org fields for the
/// logged-in user. Implemented by AuthStateEmployeeProfileService, which reads
/// Name/Zone/Region/Branch/Mobile from the authenticated user's claims
/// (AuthState/CustomAuthState).</summary>
public interface IEmployeeProfileService
{
    Task<EmployeeProfile> GetProfileAsync(string empCode, CancellationToken ct = default);
}

/// <summary>Physical storage for Feedback attachments — Feedback's own file store,
/// decoupled from the Request Portal's IAttachmentService (Feedback tickets live in
/// their own table, not RP_REQUEST).</summary>
public interface IFeedbackFileStore
{
    Task<(string RelativePath, string FileName)> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);
}

/// <summary>"ZAH: RO Visit Report" — see RoVisitModels.cs for the shape.</summary>
public interface IRoVisitRepo
{
    /// <summary>Table-1 lookup: derives Region/Regional-Head/Assurance-Head info from
    /// RP_CSBE_ORG_EMPLOYEE for the given Region SOL ID.</summary>
    Task<RoVisitRegionInfo?> GetRegionInfoAsync(string solId, CancellationToken ct = default);

    /// <summary>Table-1 lookup for "RAH: Branch Visit Report": Branch Manager / Assurance
    /// Head info derived from RP_CSBE_ORG_EMPLOYEE for the given Branch SOL ID.</summary>
    Task<BranchVisitInfo?> GetBranchInfoAsync(string solId, CancellationToken ct = default);

    Task<IReadOnlyList<RoVisitReport>> ListReportsAsync(string? regionSolId, CancellationToken ct = default);
    Task<RoVisitReport?> GetReportAsync(long id, CancellationToken ct = default);

    /// <summary>Inserts a new report and all its child rows (exec summary/metrics/
    /// findings/pending issues/assessment) in one call. Returns the new ID.</summary>
    Task<long> InsertReportAsync(RoVisitReport report, CancellationToken ct = default);

    /// <summary>Replaces the header fields and every child collection (delete +
    /// reinsert) — simplest correct semantics for a form saved as a whole. Throws
    /// if the draft has already been submitted (submitted data is immutable).</summary>
    Task UpdateReportAsync(RoVisitReport report, CancellationToken ct = default);

    /// <summary>Freezes the given draft into an immutable row in the "main"
    /// submitted table (header + every child collection as JSON, plus the exact
    /// PDF bytes that were generated), and marks the draft SUBMITTED so it can no
    /// longer be edited. Returns the new submitted-row ID.</summary>
    Task<long> SubmitReportAsync(long draftId, byte[] pdfBytes, string submittedByEmp, CancellationToken ct = default);

    /// <summary>Bytes of the PDF captured at submit time — lets a submitted report
    /// be re-downloaded at any point without regenerating it.</summary>
    Task<byte[]?> GetSubmittedPdfAsync(long submittedId, CancellationToken ct = default);

    Task<RoVisitDashboardCounts> GetDashboardCountsAsync(RoVisitDashboardFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<RoVisitSummary>> ListDashboardAsync(RoVisitDashboardFilter filter, bool? submittedOnly, CancellationToken ct = default);

    /// <summary>SOL/Zone-code -> email lookup for the auto-mail-on-submit feature.</summary>
    Task<string?> GetSolEmailAsync(string code, string levelType, CancellationToken ct = default);
}

/// <summary>"Executive Branch Visit" (Assurance 360) — see ExecBranchVisitModels.cs.
/// Simpler lifecycle than IRoVisitRepo: no email-on-submit, no PDF blob yet (this
/// form only has window.print(), no server-generated PDF) — just Draft/Submitted
/// persistence with immutability enforced once submitted.</summary>
public interface IExecBranchVisitRepo
{
    Task<ExecBranchVisit?> GetAsync(long id, CancellationToken ct = default);

    /// <summary>Most recent DRAFT for a branch code, if any — lets re-entering the
    /// same branch code resume an in-progress visit instead of starting blank.</summary>
    Task<ExecBranchVisit?> GetLatestDraftForBranchAsync(string branchCode, CancellationToken ct = default);

    /// <summary>Inserts (Id==0) or replaces (delete+reinsert Fields/OpenRows, same
    /// convention as IRoVisitRepo) a draft. Throws if the row is already SUBMITTED.
    /// Returns the row's ID (unchanged on update, new on insert).</summary>
    Task<long> SaveDraftAsync(ExecBranchVisit visit, CancellationToken ct = default);

    /// <summary>Freezes the draft into RP_EXEC_BRANCH_VISIT_SUBMITTED (header +
    /// fields + open-rows as JSON) and marks the draft SUBMITTED so it can no
    /// longer be edited. Throws if already submitted.</summary>
    Task SubmitAsync(long draftId, string submittedByEmp, CancellationToken ct = default);

    /// <summary>Draft/Submitted counts (+ submitted-by-region breakdown for the
    /// dashboard's chart) matching the given filter.</summary>
    Task<ExecBranchVisitCounts> GetCountsAsync(ExecBranchVisitFilter filter, CancellationToken ct = default);

    /// <summary>Filtered visit list for the dashboard table. statusFilter: null = both,
    /// "DRAFT"/"SUBMITTED" = one or the other (same convention as IRoVisitRepo's
    /// ListDashboardAsync submittedOnly parameter, just spelled out as a string here
    /// since Status is already a free string on the header table).</summary>
    Task<IReadOnlyList<ExecBranchVisitSummary>> ListAsync(ExecBranchVisitFilter filter, string? statusFilter, CancellationToken ct = default);
}


public interface IMasterRepo
{
    // Active-only lookup (used by app flows).
    Task<IReadOnlyList<RequestType>> ListRequestTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UnitType>> ListUnitTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Unit>> ListUnitsAsync(long? unitTypeId, CancellationToken ct = default);
    Task<IReadOnlyList<Vertical>> ListVerticalsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Department>> ListDepartmentsAsync(long? verticalId, CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> ListActivitiesAsync(long? departmentId, CancellationToken ct = default);

    // Admin lookup including inactive rows.
    Task<IReadOnlyList<RequestType>> ListRequestTypesAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UnitType>> ListUnitTypesAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Unit>> ListUnitsAllAsync(long? unitTypeId, CancellationToken ct = default);
    Task<IReadOnlyList<Vertical>> ListVerticalsAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Vertical>> ListVerticalsAllAsyncRequest(string unit, CancellationToken ct = default);
    Task<IReadOnlyList<Department>> ListDepartmentsAllAsync(long? verticalId, CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> ListActivitiesAllAsync(long? departmentId, CancellationToken ct = default);

    // CRUD — RequestType / UnitType / Vertical (flat masters).
    Task<long> InsertRequestTypeAsync(RequestType m, CancellationToken ct = default);
    Task UpdateRequestTypeAsync(RequestType m, CancellationToken ct = default);
    Task SetRequestTypeActiveAsync(long id, bool isActive, CancellationToken ct = default);

    Task<long> InsertUnitTypeAsync(UnitType m, CancellationToken ct = default);
    Task UpdateUnitTypeAsync(UnitType m, CancellationToken ct = default);
    Task SetUnitTypeActiveAsync(long id, bool isActive, CancellationToken ct = default);

    Task<long> InsertVerticalAsync(Vertical m, CancellationToken ct = default);
    Task UpdateVerticalAsync(Vertical m, CancellationToken ct = default);
    Task SetVerticalActiveAsync(long id, bool isActive, CancellationToken ct = default);

    // CRUD — Unit / Department / Activity (hierarchical).
    Task<long> InsertUnitAsync(Unit m, CancellationToken ct = default);
    Task UpdateUnitAsync(Unit m, CancellationToken ct = default);
    Task SetUnitActiveAsync(long id, bool isActive, CancellationToken ct = default);

    Task<long> InsertDepartmentAsync(Department m, CancellationToken ct = default);
    Task UpdateDepartmentAsync(Department m, CancellationToken ct = default);
    Task SetDepartmentActiveAsync(long id, bool isActive, CancellationToken ct = default);

    Task<long> InsertActivityAsync(Activity m, CancellationToken ct = default);
    Task UpdateActivityAsync(Activity m, CancellationToken ct = default);
    Task SetActivityActiveAsync(long id, bool isActive, CancellationToken ct = default);

    Task<int> GetSlaWorkingDaysAsync(long requestTypeId, int levelNo, CancellationToken ct = default);
    Task<IReadOnlyList<DateTime>> GetHolidaysAsync(long calendarId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    // SLA config CRUD.
    Task<IReadOnlyList<SlaConfig>> ListSlaConfigAsync(CancellationToken ct = default);
    Task<long> InsertSlaConfigAsync(SlaConfig m, CancellationToken ct = default);
    Task UpdateSlaConfigAsync(SlaConfig m, CancellationToken ct = default);
    Task DeleteSlaConfigAsync(long id, CancellationToken ct = default);

    // Holiday calendar + days CRUD.
    Task<IReadOnlyList<HolidayCalendar>> ListHolidayCalendarsAsync(CancellationToken ct = default);
    Task<long> InsertHolidayCalendarAsync(HolidayCalendar m, CancellationToken ct = default);
    Task UpdateHolidayCalendarAsync(HolidayCalendar m, CancellationToken ct = default);
    Task DeleteHolidayCalendarAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<Holiday>> ListHolidaysForAdminAsync(long calId, CancellationToken ct = default);
    Task<long> InsertHolidayAsync(Holiday h, CancellationToken ct = default);
    Task UpdateHolidayAsync(Holiday h, CancellationToken ct = default);
    Task DeleteHolidayAsync(long id, CancellationToken ct = default);
}

public interface IReportRepo
{
    Task<IReadOnlyList<ReportDef>> ListDefsAsync(long? verticalId, long? departmentId, string? nameSearch, bool activeOnly, CancellationToken ct = default);
    Task<ReportDef?> GetDefAsync(long id, CancellationToken ct = default);
    Task<long> InsertDefAsync(ReportDefForm f, string createdByEmp, CancellationToken ct = default);
    Task UpdateDefAsync(ReportDefForm f, string actorEmp, CancellationToken ct = default);
    // Soft delete only — reports are never hard-deleted, only enabled/disabled.
    Task SetActiveAsync(long id, bool isActive, string actorEmp, CancellationToken ct = default);

    /// <summary>Generates the next unique Report Code for a vertical — e.g. "COMP-0007" —
    /// from the vertical's name prefix and its own running sequence. Called by Report
    /// Developer whenever the admin picks/changes the Vertical on a new report.</summary>
    Task<string> GenerateReportCodeAsync(long verticalId, CancellationToken ct = default);

    // Filters (Zone/Region/Branch/Custom) available on a report's viewing page.
    Task<IReadOnlyList<ReportFilterDef>> ListFiltersAsync(long reportId, CancellationToken ct = default);
    /// <summary>Replaces the full filter set for a report in one call (delete + reinsert) —
    /// simplest correct semantics for a small admin-managed list edited as a whole form.</summary>
    Task ReplaceFiltersAsync(long reportId, IReadOnlyList<ReportFilterDef> filters, CancellationToken ct = default);

    // Audit trail — every create/update/delete/enable/disable on a report definition.
    Task<IReadOnlyList<ReportAuditEntry>> ListAuditAsync(long? reportId, int take, CancellationToken ct = default);

    // Access log — every load attempt against a report (success, failure or disabled).
    Task LogAccessAsync(long reportId, string reportName, string accessedBy, ReportSite? runningFrom, int? rowsReturned, bool success, string? errorMsg, CancellationToken ct = default);
    Task<IReadOnlyList<ReportAccessEntry>> ListAccessAsync(long? reportId, int take, CancellationToken ct = default);
}

/// <summary>Repository for the MoM Developer module (Phase 1: type/meeting-type/union masters + entries).</summary>
public interface IMomRepo
{
    // MoM Types (admin-authored config cards)
    Task<IReadOnlyList<MomType>> ListTypesAsync(long? verticalId, CancellationToken ct = default);
    Task<MomType?> GetTypeAsync(long id, CancellationToken ct = default);
    Task<long> InsertTypeAsync(MomTypeForm f, string createdByEmp, CancellationToken ct = default);
    Task UpdateTypeAsync(long id, MomTypeForm f, CancellationToken ct = default);
    Task SetTypeActiveAsync(long id, bool isActive, CancellationToken ct = default);

    // Meeting types scoped to a MoM Type
    Task<IReadOnlyList<MomMeetingType>> ListMeetingTypesAsync(long momTypeId, CancellationToken ct = default);
    Task<long> UpsertMeetingTypeAsync(long momTypeId, MomMeetingType m, CancellationToken ct = default);

    // Unions scoped to a MoM Type
    Task<IReadOnlyList<MomUnion>> ListUnionsAsync(long momTypeId, CancellationToken ct = default);
    Task<long> UpsertUnionAsync(long momTypeId, MomUnion u, CancellationToken ct = default);

    // Entries
    Task<IReadOnlyList<MomEntry>> ListEntriesAsync(long momTypeId, string? zone, string? region, DateTime? dateFrom, DateTime? dateTo,
        long? meetingTypeId, int? quarter, int? year, CancellationToken ct = default);
    Task<MomEntry?> GetEntryAsync(long id, CancellationToken ct = default);
    Task<long> InsertEntryAsync(MomEntryForm f, string createdByEmp, CancellationToken ct = default);
    Task UpdateEntryAsync(long id, MomEntryForm f, CancellationToken ct = default);
    Task DeleteEntryAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Item 6 (RBAC), generalized (migration 007) beyond the original IR/ER-only ERD/WELFARE pair: for any
    /// MoM type whose meeting types have been grouped into CO scope codes via the builder (MomMeetingType.
    /// CoScopeCode), a CO-level user may be narrowed to one of those groups via RP_M_MOM_CO_SCOPE. Returns
    /// null when the user has no explicit row for this MoM type (fail-open: sees everything, matching the
    /// existing generic-CO behavior).
    /// </summary>
    Task<string?> GetCoScopeAsync(long momTypeId, string empCode, CancellationToken ct = default);

    // Item 6 admin UI: manage RP_M_MOM_CO_SCOPE assignments.
    Task<IReadOnlyList<MomCoScope>> ListCoScopeAsync(long momTypeId, CancellationToken ct = default);
    Task<long> AddCoScopeAsync(long momTypeId, string empCode, string scopeCode, string assignedByEmp, CancellationToken ct = default);
    Task RemoveCoScopeAsync(long id, CancellationToken ct = default);

    /// <summary>Distinct CO scope codes actually configured (via meeting-type CoScopeCode) for this MoM
    /// type, each with a friendly label built from the meeting-type names sharing that code — drives the
    /// Scope dropdown on MomCoScopeAdmin generically instead of a hardcoded ERD/WELFARE pair. Empty for a
    /// MoM type where no meeting type has been assigned a CoScopeCode yet.</summary>
    Task<IReadOnlyList<(string ScopeCode, string Label)>> ListCoScopeGroupsAsync(long momTypeId, CancellationToken ct = default);

    // Attachments
    Task<IReadOnlyList<MomAttachment>> ListAttachmentsAsync(long entryId, CancellationToken ct = default);
    Task<long> InsertAttachmentAsync(long entryId, string fileName, string filePath, string uploadedByEmp, CancellationToken ct = default);
}

/// <summary>Physical storage for MoM Developer uploads (filesystem on the app server).</summary>
public interface IMomAttachmentStore
{
    /// <summary>Persist a stream and return its storage-relative path and sanitized file name.</summary>
    Task<(string RelativePath, string FileName)> SaveAsync(long momTypeId, string storageRootPath, Stream content, string originalFileName, CancellationToken ct = default);
    /// <summary>Open a stored file for reading.</summary>
    Task<Stream> OpenAsync(string relativePath, CancellationToken ct = default);
}

///// <summary>
///// Repository for the CS&amp;BE "Monthly Information Notes by CO Verticals to
///// ED / MD&amp;CEO" module: one note per (vertical, month, year), with full
///// document-version history and status-change audit trail.
///// </summary>
//public interface ICsbeMonthlyNoteRepo
//{
//    Task<IReadOnlyList<MonthlyNote>> ListNotesAsync(int year, int? month, long? verticalId, CancellationToken ct = default);
//    Task<MonthlyNote?> GetNoteAsync(long id, CancellationToken ct = default);
//    Task<MonthlyNote?> GetNoteAsync(long verticalId, int month, int year, CancellationToken ct = default);
//    Task<MonthlyNote> GetOrCreateNoteAsync(long verticalId, int month, int year, CancellationToken ct = default);

//    /// <summary>
//    /// Records a new document for the (vertical, month, year) note. If the note
//    /// doesn't exist yet it is created (status Submitted). If it already has a
//    /// current version, that version is archived (REPLACED_AT set) before the
//    /// new one is inserted as current.
//    /// The caller is responsible for persisting the file itself first (via
//    /// <see cref="IMomAttachmentStore"/>, reused for storage) and passing the
//    /// resulting storage-relative path here — this method only records the
//    /// DB-side pointer + version history, it does not touch the filesystem.
//    /// </summary>
//    Task<long> UploadOrReplaceAsync(long verticalId, int month, int year, string relativeFilePath, string fileName, string uploadedByEmp, string? actionDetails = null, CancellationToken ct = default);

//    /// <summary>Validates the transition is forward-only through the 4 states and records it in the status history.</summary>
//    Task AdvanceStatusAsync(long noteId, string newStatus, string changedByEmp, string? remarks, CancellationToken ct = default);

//    Task<IReadOnlyList<MonthlyNoteVersion>> ListVersionsAsync(long noteId, CancellationToken ct = default);
//    Task<MonthlyNoteVersion?> GetVersionAsync(long versionId, CancellationToken ct = default);
//    Task<IReadOnlyList<StatusHistoryEntry>> ListStatusHistoryAsync(long noteId, CancellationToken ct = default);

//    /// <summary>Active verticals that have NOT yet submitted a note for the given month/year — used by both reminder jobs.</summary>
//    Task<IReadOnlyList<Vertical>> ListPendingForReminderAsync(int month, int year, CancellationToken ct = default);

//    /// <summary>
//    /// Vertical IDs the given employee is a registered admin for, per the existing
//    /// RP_RBAC_VERTICAL_ADMIN table (built for Union Hub posting permissions, reused
//    /// here — see CSBE_MONTHLY_NOTES_MODULE.md for the honesty note on this fallback).
//    /// Empty list means "no explicit per-vertical mapping exists for this user".
//    /// </summary>
//    Task<IReadOnlyList<long>> ListMyVerticalIdsAsync(string empCode, CancellationToken ct = default);

//    /// <summary>Employee codes registered as RP_RBAC_VERTICAL_ADMIN for a given vertical — used by the reminder job.</summary>
//    Task<IReadOnlyList<string>> ListVerticalAdminEmpCodesAsync(long verticalId, CancellationToken ct = default);
//}

///// <summary>
///// Data source for the CS&amp;BE "Organogram Page" module. Per the BRD, organogram
///// data should be sourced from the "Role Clarity" tool ("Organogram data shall
///// be derived from Role Clarity tool"), but the BRD itself flags that
///// integration as dependent on "accuracy and approved access to source data"
///// — i.e. not available yet. This interface is the documented swap-in seam:
///// <see cref="RequestPortal.Data.Repositories.OrgDataRepo"/> is the only
///// implementation today (a local, admin/HR-maintained DB table shaped like
///// Role Clarity's expected output). A future real Role Clarity API client
///// could implement this same interface and be registered in its place in
///// <c>DataServiceCollectionExtensions</c> — no UI change required.
///// </summary>
//public interface IOrgDataSource
//{
//    /// <summary>All employees (optionally filtered), active + inactive, for admin listing.</summary>
//    Task<IReadOnlyList<OrgEmployee>> ListEmployeesAsync(long? verticalId, bool includeInactive, CancellationToken ct = default);
//    Task<OrgEmployee?> GetByEmpCodeAsync(string empCode, CancellationToken ct = default);

//    /// <summary>Direct reports of the given employee code — the "My Team" / supervisor view.</summary>
//    Task<IReadOnlyList<OrgEmployee>> ListDirectReportsAsync(string reportingOfficerEmpCode, CancellationToken ct = default);

//    /// <summary>Scale-wise headcount summary (active employees only), optionally scoped to a vertical.</summary>
//    Task<IReadOnlyList<OrgScaleSummary>> GetScaleSummaryAsync(long? verticalId, CancellationToken ct = default);

//    /// <summary>Inactive employees, optionally scoped to a vertical, for the separate inactive-employees panel.</summary>
//    Task<IReadOnlyList<OrgEmployee>> ListInactiveAsync(long? verticalId, CancellationToken ct = default);

//    /// <summary>Insert (Id null) or update (Id set) an employee row — admin data entry, since there is no live feed to auto-populate from.</summary>
//    Task<long> UpsertEmployeeAsync(OrgEmployeeForm f, string actorEmp, CancellationToken ct = default);

//    Task SetActiveAsync(long id, bool isActive, DateTime? inactiveSinceDate, string actorEmp, CancellationToken ct = default);
//}


public interface IRequestRepo
{
    Task<long> InsertAsync(Request r, CancellationToken ct = default);
    Task<Request?> GetAsync(long id, CancellationToken ct = default);
    Task<RequestDetail?> GetDetailAsync(long id, CancellationToken ct = default);
    Task<(IReadOnlyList<RequestListItem> Items, int Total)> ListAsync(FilterDto f, string? assignedToEmpCode, string? raisedByEmpCode, CancellationToken ct = default);
    Task UpdateStatusLevelAsync(long id, RequestStatus status, int currentLevel, DateTime? slaDueUtc, CancellationToken ct = default);
    Task SetClosedAsync(long id, DateTime closedAt, CancellationToken ct = default);
    Task DeactivateAssigneesAsync(long id, int level, CancellationToken ct = default);
    Task InsertAssigneeAsync(RequestAssignee a, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetActiveAssigneeEmpCodesAsync(long id, int level, CancellationToken ct = default);
    Task InsertActionAsync(RequestAction a, CancellationToken ct = default);
    Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<long>> ListIdsDueForEscalationAsync(int batchSize, CancellationToken ct = default);
    Task InsertClarificationAsync(Clarification c, CancellationToken ct = default);
    Task<Clarification?> GetClarificationAsync(long id, CancellationToken ct = default);
    Task<Clarification?> GetLatestOpenClarificationAsync(long requestId, CancellationToken ct = default);
    Task UpdateClarificationReplyAsync(long id, string answer, DateTime repliedAt, CancellationToken ct = default);
    Task InsertSlaPauseAsync(SlaPause p, CancellationToken ct = default);
    Task ResumeOpenSlaPauseAsync(long requestId, DateTime resumedAt, CancellationToken ct = default);
    Task<IReadOnlyList<SlaPause>> GetSlaPausesAsync(long requestId, CancellationToken ct = default);
    Task InsertFeedbackAsync(Feedback f, CancellationToken ct = default);
}

public interface IRoutingRepo
{
    Task<IReadOnlyList<string>> ResolveL1Async(long requestTypeId, long unitId, long verticalId, long departmentId, long activityId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ResolveByRoleAsync(RoleCode role, long unitId, long verticalId, long departmentId, CancellationToken ct = default);

    // Routing rule CRUD.
    Task<IReadOnlyList<RoutingRule>> ListRulesAsync(CancellationToken ct = default);
    Task<long> InsertRuleAsync(RoutingRule r, CancellationToken ct = default);
    Task UpdateRuleAsync(RoutingRule r, CancellationToken ct = default);
    Task SetRuleActiveAsync(long id, bool isActive, CancellationToken ct = default);
    Task DeleteRuleAsync(long id, CancellationToken ct = default);

    // Routing assignee CRUD (per rule).
    Task<IReadOnlyList<RoutingAssignee>> ListAssigneesAsync(long ruleId, CancellationToken ct = default);
    Task<long> InsertAssigneeAsync(RoutingAssignee a, CancellationToken ct = default);
    Task DeleteAssigneeAsync(long id, CancellationToken ct = default);
}

public interface IAuditRepo
{
    Task<string?> GetLastHashAsync(CancellationToken ct = default);
    Task InsertAsync(AuditLog log, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> ListByEntityAsync(string entity, long entityId, CancellationToken ct = default);
    Task<bool> VerifyChainAsync(CancellationToken ct = default);

    /// <summary>
    /// Browse the audit trail for the Setup Audit Log admin page.
    /// All parameters are optional filters.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> ListSetupAuditAsync(
        int take,
        string? entityFilter = null,
        string? actorEmpCode = null,
        string? actionFilter = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken ct = default);
}

public interface INotifRepo
{
    Task<NotifTemplate?> GetTemplateAsync(string eventCode, NotifChannel channel, CancellationToken ct = default);
    Task<long> EnqueueAsync(NotifOutboxItem item, CancellationToken ct = default);
    Task<IReadOnlyList<NotifOutboxItem>> ListPendingAsync(int batchSize, CancellationToken ct = default);
    Task MarkSentAsync(long id, DateTime sentAt, CancellationToken ct = default);
    Task MarkFailedAsync(long id, string error, CancellationToken ct = default);

    // Template CRUD.
    Task<IReadOnlyList<NotifTemplate>> ListTemplatesAsync(CancellationToken ct = default);
    Task<long> InsertTemplateAsync(NotifTemplate m, CancellationToken ct = default);
    Task UpdateTemplateAsync(NotifTemplate m, CancellationToken ct = default);
    Task SetTemplateActiveAsync(long id, bool isActive, CancellationToken ct = default);
}

public interface IUserRepo
{
    Task<AppUser?> GetByAdAsync(string sam, CancellationToken ct = default);
    Task<AppUser?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<long> InsertAsync(AppUser u, CancellationToken ct = default);
    Task UpdateProfileAsync(long id, string? name, string? email, CancellationToken ct = default);
    Task<IReadOnlyList<RoleCode>> GetEffectiveRolesAsync(string empCode, CancellationToken ct = default);
    Task<IReadOnlyList<AppUser>> SearchAsync(string q, int take, CancellationToken ct = default);

    /// <summary>
    /// Batch lookup of users by exact employee codes. Returns one AppUser per matched code.
    /// Eliminates N+1 when resolving display names for a list of emp codes.
    /// </summary>
    Task<IReadOnlyList<AppUser>> GetByEmpCodesAsync(IEnumerable<string> empCodes, CancellationToken ct = default);

    // Role lookup (for delegation + admin role assignment).
    Task<long?> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default);
    Task<IReadOnlyList<AppRole>> ListRolesAsync(CancellationToken ct = default);

    // Delegation CRUD.
    Task<IReadOnlyList<Delegation>> ListDelegationsAsync(bool currentOnly, CancellationToken ct = default);
    Task<long> InsertDelegationAsync(Delegation d, CancellationToken ct = default);
    Task UpdateDelegationAsync(Delegation d, CancellationToken ct = default);
    Task DeleteDelegationAsync(long id, CancellationToken ct = default);
}
