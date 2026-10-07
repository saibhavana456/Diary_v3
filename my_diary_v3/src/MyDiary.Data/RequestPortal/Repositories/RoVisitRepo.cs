using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

/// <summary>
/// ADO.NET/Oracle implementation of <see cref="IRoVisitRepo"/> — same conventions
/// as <see cref="OrgDataRepo"/>/<see cref="CsbeMonthlyNoteRepo"/>.
/// </summary>
public sealed class RoVisitRepo : IRoVisitRepo
{
    private readonly AdoUnitOfWork _uow;
    public RoVisitRepo(AdoUnitOfWork uow) => _uow = uow;

    // ── Table-1 region lookup (derived from RP_CSBE_ORG_EMPLOYEE by convention) ─
    public async Task<RoVisitRegionInfo?> GetRegionInfoAsync(string solId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT EMPLOYEE_NAME, EMP_CODE, ROLE_NAME, SOL_NAME, POSTING_SINCE_DATE
              FROM RP_CSBE_ORG_EMPLOYEE
              WHERE SOL_ID = :p_sol AND IS_ACTIVE = 1
              ORDER BY POSTING_SINCE_DATE DESC NULLS LAST", _uow.OracleTx);
        cmd.Parameters.AddIn("p_sol", solId);

        var rows = await cmd.QueryAsync(r => new
        {
            Name = Str(r, "EMPLOYEE_NAME"),
            EmpCode = Str(r, "EMP_CODE"),
            Role = StrN(r, "ROLE_NAME") ?? "",
            SolName = StrN(r, "SOL_NAME"),
            Posted = DtN(r, "POSTING_SINCE_DATE")
        }, ct);

        if (rows.Count == 0) return null;

        var rh = rows.FirstOrDefault(x => x.Role.Contains("Regional Head", StringComparison.OrdinalIgnoreCase));
        var ah = rows.FirstOrDefault(x => x.Role.Contains("Assurance Head", StringComparison.OrdinalIgnoreCase)
                                        || x.Role.Contains("RAH", StringComparison.OrdinalIgnoreCase));

        return new RoVisitRegionInfo
        {
            RegionName = rows[0].SolName,
            RegionalHeadEmpCode = rh?.EmpCode,
            RegionalHeadName = rh?.Name,
            RhWorkingSince = rh?.Posted,
            AssuranceHeadEmpCode = ah?.EmpCode,
            AssuranceHeadName = ah?.Name,
            AhWorkingSince = ah?.Posted
        };
    }

    // ── Table-1 branch lookup (Branch Visit Report — Branch Manager + Assurance Head) ─
    public async Task<BranchVisitInfo?> GetBranchInfoAsync(string solId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT EMPLOYEE_NAME, EMP_CODE, ROLE_NAME, SOL_NAME, POSTING_SINCE_DATE
              FROM RP_CSBE_ORG_EMPLOYEE
              WHERE SOL_ID = :p_sol AND IS_ACTIVE = 1
              ORDER BY POSTING_SINCE_DATE DESC NULLS LAST", _uow.OracleTx);
        cmd.Parameters.AddIn("p_sol", solId);

        var rows = await cmd.QueryAsync(r => new
        {
            Name = Str(r, "EMPLOYEE_NAME"),
            EmpCode = Str(r, "EMP_CODE"),
            Role = StrN(r, "ROLE_NAME") ?? "",
            SolName = StrN(r, "SOL_NAME"),
            Posted = DtN(r, "POSTING_SINCE_DATE")
        }, ct);

        if (rows.Count == 0) return null;

        var bm = rows.FirstOrDefault(x => x.Role.Contains("Branch Manager", StringComparison.OrdinalIgnoreCase));
        var ah = rows.FirstOrDefault(x => x.Role.Contains("Assurance Head", StringComparison.OrdinalIgnoreCase));

        return new BranchVisitInfo
        {
            BranchName = rows[0].SolName,
            BranchManagerEmpCode = bm?.EmpCode,
            BranchManagerName = bm?.Name,
            BmWorkingSince = bm?.Posted,
            AssuranceHeadEmpCode = ah?.EmpCode,
            AssuranceHeadName = ah?.Name,
            AhWorkingSince = ah?.Posted
        };
    }

    // ── Reports ──────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<RoVisitReport>> ListReportsAsync(string? regionSolId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var sql = @"SELECT ID, REGION_SOL_ID, REGION_NAME, VISITING_OFFICIAL_NAME, DATE_OF_VISIT, REPORT_DATE, STATUS, CREATED_AT
                    FROM RP_RO_VISIT_REPORT WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(regionSolId)) sql += " AND REGION_SOL_ID = :p_sol";
        sql += " ORDER BY CREATED_AT DESC";

        using var cmd = _uow.OracleConn.Cmd(sql, _uow.OracleTx);
        if (!string.IsNullOrWhiteSpace(regionSolId)) cmd.Parameters.AddIn("p_sol", regionSolId);

        return await cmd.QueryAsync(r => new RoVisitReport
        {
            Id = Long(r, "ID"), RegionSolId = Str(r, "REGION_SOL_ID"), RegionName = StrN(r, "REGION_NAME"),
            VisitingOfficialName = Str(r, "VISITING_OFFICIAL_NAME"), DateOfVisit = DtN(r, "DATE_OF_VISIT"),
            ReportDate = DtN(r, "REPORT_DATE"), Status = Str(r, "STATUS"), CreatedAt = Dt(r, "CREATED_AT")
        }, ct);
    }

    public async Task<RoVisitReport?> GetReportAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT ID, REPORT_TYPE, REGION_SOL_ID, REGION_NAME, ZONE_NAME, REGIONAL_HEAD_EMP_CODE, REGIONAL_HEAD_NAME, RH_WORKING_SINCE,
                     ASSURANCE_HEAD_EMP_CODE, ASSURANCE_HEAD_NAME, AH_WORKING_SINCE, VISITING_OFFICIAL_EMP_CODE,
                     VISITING_OFFICIAL_NAME, DESIGNATION, DATE_OF_VISIT, REPORT_DATE, SUSPENSE_COMMENTS, OVERALL_REMARKS,
                     RAH_NAME, RAH_EMP_CODE, STATUS, CREATED_BY_EMP, CREATED_AT,
                     BRANCH_NAME, BRANCH_CODE, DATE_OF_OPENING, BRANCH_CATEGORY, AUDIT_RATING, QUARTER_YEAR,
                     BRANCH_MANAGER_NAME, BM_WORKING_SINCE, BRANCH_MANAGER_SIGNOFF_NAME, REGISTERS_COMMENTS
              FROM RP_RO_VISIT_REPORT WHERE ID = :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        var report = await cmd.QueryOneAsync(MapReport, ct);
        if (report is null) return null;

        report.ExecSummary = (await ListExecSummaryAsync(id, ct)).ToList();
        report.Metrics = (await ListMetricsAsync(id, ct)).ToList();
        report.Findings = (await ListFindingsAsync(id, ct)).ToList();
        report.PendingIssues = (await ListPendingIssuesAsync(id, ct)).ToList();
        report.Assessment = (await ListAssessmentAsync(id, ct)).ToList();
        return report;
    }

    public async Task<long> InsertReportAsync(RoVisitReport report, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_RO_VISIT_REPORT
                (REPORT_TYPE, REGION_SOL_ID, REGION_NAME, ZONE_NAME, REGIONAL_HEAD_EMP_CODE, REGIONAL_HEAD_NAME, RH_WORKING_SINCE,
                 ASSURANCE_HEAD_EMP_CODE, ASSURANCE_HEAD_NAME, AH_WORKING_SINCE, VISITING_OFFICIAL_EMP_CODE,
                 VISITING_OFFICIAL_NAME, DESIGNATION, DATE_OF_VISIT, REPORT_DATE, SUSPENSE_COMMENTS, OVERALL_REMARKS,
                 RAH_NAME, RAH_EMP_CODE, STATUS, CREATED_BY_EMP, SAVED_AT,
                 BRANCH_NAME, BRANCH_CODE, DATE_OF_OPENING, BRANCH_CATEGORY, AUDIT_RATING, QUARTER_YEAR,
                 BRANCH_MANAGER_NAME, BM_WORKING_SINCE, BRANCH_MANAGER_SIGNOFF_NAME, REGISTERS_COMMENTS)
              VALUES
                (:p_rtype,:p_sol,:p_rname,:p_zname,:p_rhcode,:p_rhname,:p_rhsince,
                 :p_ahcode,:p_ahname,:p_ahsince,:p_vocode,
                 :p_voname,:p_desg,:p_dov,:p_rdate,:p_suspcom,:p_remarks,
                 :p_rahname,:p_rahcode,:p_status,:p_emp,SYSTIMESTAMP,
                 :p_bname,:p_bcode,:p_dopen,:p_bcat,:p_arating,:p_qyear,
                 :p_bmname,:p_bmsince,:p_bmsignoff,:p_regcom)
              RETURNING ID INTO :p_id", _uow.OracleTx);
        BindReport(cmd, report);
        cmd.Parameters.AddIn("p_emp", report.CreatedByEmp);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        var id = outId.OutId();

        await SaveChildrenAsync(id, report, ct);
        return id;
    }

    public async Task UpdateReportAsync(RoVisitReport report, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        using (var statusCmd = _uow.OracleConn.Cmd("SELECT STATUS FROM RP_RO_VISIT_REPORT WHERE ID=:p_id", _uow.OracleTx))
        {
            statusCmd.Parameters.AddIn("p_id", report.Id);
            var status = await statusCmd.ScalarAsync<string>(ct);
            if (status == "SUBMITTED")
                throw new InvalidOperationException("This report has already been submitted and can no longer be edited.");
        }

        using var cmd = _uow.OracleConn.Cmd(
            @"UPDATE RP_RO_VISIT_REPORT SET
                REPORT_TYPE=:p_rtype, REGION_SOL_ID=:p_sol, REGION_NAME=:p_rname, ZONE_NAME=:p_zname,
                REGIONAL_HEAD_EMP_CODE=:p_rhcode, REGIONAL_HEAD_NAME=:p_rhname, RH_WORKING_SINCE=:p_rhsince,
                ASSURANCE_HEAD_EMP_CODE=:p_ahcode, ASSURANCE_HEAD_NAME=:p_ahname, AH_WORKING_SINCE=:p_ahsince,
                VISITING_OFFICIAL_EMP_CODE=:p_vocode, VISITING_OFFICIAL_NAME=:p_voname, DESIGNATION=:p_desg,
                DATE_OF_VISIT=:p_dov, REPORT_DATE=:p_rdate, SUSPENSE_COMMENTS=:p_suspcom, OVERALL_REMARKS=:p_remarks,
                RAH_NAME=:p_rahname, RAH_EMP_CODE=:p_rahcode, STATUS=:p_status, SAVED_AT=SYSTIMESTAMP, UPDATED_AT=SYSTIMESTAMP,
                BRANCH_NAME=:p_bname, BRANCH_CODE=:p_bcode, DATE_OF_OPENING=:p_dopen, BRANCH_CATEGORY=:p_bcat,
                AUDIT_RATING=:p_arating, QUARTER_YEAR=:p_qyear, BRANCH_MANAGER_NAME=:p_bmname, BM_WORKING_SINCE=:p_bmsince,
                BRANCH_MANAGER_SIGNOFF_NAME=:p_bmsignoff, REGISTERS_COMMENTS=:p_regcom
              WHERE ID=:p_id", _uow.OracleTx);
        BindReport(cmd, report);
        cmd.Parameters.AddIn("p_id", report.Id);
        await cmd.ExecAsync(ct);

        await SaveChildrenAsync(report.Id, report, ct);
    }

    // ── Submit (draft -> immutable main table) ──────────────────────────────
    public async Task<long> SubmitReportAsync(long draftId, byte[] pdfBytes, string submittedByEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        var report = await GetReportAsync(draftId, ct)
            ?? throw new InvalidOperationException("Draft report not found.");
        if (report.Status == "SUBMITTED")
            throw new InvalidOperationException("This report has already been submitted.");

        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_RO_VISIT_REPORT_SUBMITTED
                (DRAFT_ID, REPORT_TYPE, REGION_SOL_ID, REGION_NAME, ZONE_NAME, REGIONAL_HEAD_NAME, ASSURANCE_HEAD_NAME,
                 VISITING_OFFICIAL_EMP_CODE, VISITING_OFFICIAL_NAME, DATE_OF_VISIT, REPORT_DATE, RAH_NAME,
                 EXEC_SUMMARY_JSON, METRICS_JSON, FINDINGS_JSON, PENDING_ISSUES_JSON, ASSESSMENT_JSON,
                 OVERALL_REMARKS, SUSPENSE_COMMENTS, PDF_BLOB, SUBMITTED_BY_EMP,
                 BRANCH_NAME, BRANCH_CODE, BRANCH_MANAGER_NAME)
              VALUES
                (:p_draft,:p_rtype,:p_sol,:p_rname,:p_zname,:p_rhname,:p_ahname,
                 :p_vocode,:p_voname,:p_dov,:p_rdate,:p_rahname,
                 :p_exec,:p_metrics,:p_findings,:p_issues,:p_assess,
                 :p_remarks,:p_suspcom,:p_pdf,:p_emp,
                 :p_bname,:p_bcode,:p_bmname)
              RETURNING ID INTO :p_id", _uow.OracleTx);

        cmd.Parameters.AddIn("p_draft", draftId);
        cmd.Parameters.AddIn("p_rtype", report.ReportType);
        cmd.Parameters.AddIn("p_bname", (object?)report.BranchName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_bcode", (object?)report.BranchCode ?? DBNull.Value);
        cmd.Parameters.AddIn("p_bmname", (object?)report.BranchManagerName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_sol", report.RegionSolId);
        cmd.Parameters.AddIn("p_rname", (object?)report.RegionName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_zname", (object?)report.ZoneName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rhname", (object?)report.RegionalHeadName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_ahname", (object?)report.AssuranceHeadName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_vocode", report.VisitingOfficialEmpCode);
        cmd.Parameters.AddIn("p_voname", report.VisitingOfficialName);
        cmd.Parameters.AddIn("p_dov", OracleDbType.Date, (object?)report.DateOfVisit ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rdate", OracleDbType.Date, (object?)report.ReportDate ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rahname", (object?)report.RahName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_exec", OracleDbType.Clob, JsonSerializer.Serialize(report.ExecSummary));
        cmd.Parameters.AddIn("p_metrics", OracleDbType.Clob, JsonSerializer.Serialize(report.Metrics));
        cmd.Parameters.AddIn("p_findings", OracleDbType.Clob, JsonSerializer.Serialize(report.Findings));
        cmd.Parameters.AddIn("p_issues", OracleDbType.Clob, JsonSerializer.Serialize(report.PendingIssues));
        cmd.Parameters.AddIn("p_assess", OracleDbType.Clob, JsonSerializer.Serialize(report.Assessment));
        cmd.Parameters.AddIn("p_remarks", OracleDbType.Clob, (object?)report.OverallRemarks ?? DBNull.Value);
        cmd.Parameters.AddIn("p_suspcom", OracleDbType.Clob, (object?)report.SuspenseComments ?? DBNull.Value);
        cmd.Parameters.AddIn("p_pdf", OracleDbType.Blob, pdfBytes);
        cmd.Parameters.AddIn("p_emp", submittedByEmp);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        var submittedId = outId.OutId();

        using (var lockCmd = _uow.OracleConn.Cmd("UPDATE RP_RO_VISIT_REPORT SET STATUS='SUBMITTED', UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id", _uow.OracleTx))
        {
            lockCmd.Parameters.AddIn("p_id", draftId);
            await lockCmd.ExecAsync(ct);
        }

        return submittedId;
    }

    public async Task<byte[]?> GetSubmittedPdfAsync(long submittedId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("SELECT PDF_BLOB FROM RP_RO_VISIT_REPORT_SUBMITTED WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", submittedId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        if (reader.IsDBNull(0)) return null;
        using var oracleReader = (OracleDataReader)reader;
        using var blob = oracleReader.GetOracleBlob(0);
        return blob.Value;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────
    public async Task<RoVisitDashboardCounts> GetDashboardCountsAsync(RoVisitDashboardFilter filter, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        var draftSql = "SELECT COUNT(*) FROM RP_RO_VISIT_REPORT WHERE STATUS='DRAFT'" + BuildDraftFilter(filter, out var draftParams);
        using (var dCmd = _uow.OracleConn.Cmd(draftSql, _uow.OracleTx))
        {
            foreach (var p in draftParams) dCmd.Parameters.AddIn(p.Key, p.Value);
            var draftCount = await dCmd.ScalarAsync<int>(ct);

            var subSql = "SELECT COUNT(*) FROM RP_RO_VISIT_REPORT_SUBMITTED WHERE 1=1" + BuildSubmittedFilter(filter, out var subParams);
            using var sCmd = _uow.OracleConn.Cmd(subSql, _uow.OracleTx);
            foreach (var p in subParams) sCmd.Parameters.AddIn(p.Key, p.Value);
            var subCount = await sCmd.ScalarAsync<int>(ct);

            return new RoVisitDashboardCounts { TotalDraft = draftCount, TotalSubmitted = subCount };
        }
    }

    public async Task<IReadOnlyList<RoVisitSummary>> ListDashboardAsync(RoVisitDashboardFilter filter, bool? submittedOnly, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var results = new List<RoVisitSummary>();

        if (submittedOnly != true)
        {
            var draftSql = @"SELECT ID, REGION_SOL_ID, REGION_NAME, ZONE_NAME, VISITING_OFFICIAL_NAME, DATE_OF_VISIT, SAVED_AT
                             FROM RP_RO_VISIT_REPORT WHERE STATUS='DRAFT'" + BuildDraftFilter(filter, out var draftParams);
            using var dCmd = _uow.OracleConn.Cmd(draftSql, _uow.OracleTx);
            foreach (var p in draftParams) dCmd.Parameters.AddIn(p.Key, p.Value);
            results.AddRange(await dCmd.QueryAsync(r => new RoVisitSummary
            {
                Id = Long(r, "ID"), RegionSolId = Str(r, "REGION_SOL_ID"), RegionName = StrN(r, "REGION_NAME"),
                ZoneName = StrN(r, "ZONE_NAME"), VisitingOfficialName = Str(r, "VISITING_OFFICIAL_NAME"),
                DateOfVisit = DtN(r, "DATE_OF_VISIT"), SavedAt = DtN(r, "SAVED_AT"), IsSubmitted = false
            }, ct));
        }

        if (submittedOnly != false)
        {
            var subSql = @"SELECT ID, DRAFT_ID, REGION_SOL_ID, REGION_NAME, ZONE_NAME, VISITING_OFFICIAL_NAME, DATE_OF_VISIT, SUBMITTED_AT
                           FROM RP_RO_VISIT_REPORT_SUBMITTED WHERE 1=1" + BuildSubmittedFilter(filter, out var subParams);
            using var sCmd = _uow.OracleConn.Cmd(subSql, _uow.OracleTx);
            foreach (var p in subParams) sCmd.Parameters.AddIn(p.Key, p.Value);
            results.AddRange(await sCmd.QueryAsync(r => new RoVisitSummary
            {
                Id = Long(r, "DRAFT_ID"), SubmittedId = Long(r, "ID"), RegionSolId = Str(r, "REGION_SOL_ID"),
                RegionName = StrN(r, "REGION_NAME"), ZoneName = StrN(r, "ZONE_NAME"),
                VisitingOfficialName = Str(r, "VISITING_OFFICIAL_NAME"), DateOfVisit = DtN(r, "DATE_OF_VISIT"),
                SubmittedAt = DtN(r, "SUBMITTED_AT"), IsSubmitted = true
            }, ct));
        }

        return results.OrderByDescending(r => r.SubmittedAt ?? r.SavedAt).ToList();
    }

    private static string BuildDraftFilter(RoVisitDashboardFilter f, out Dictionary<string, object> ps)
    {
        ps = new Dictionary<string, object>();
        var sql = "";
        if (!string.IsNullOrWhiteSpace(f.ReportType)) { sql += " AND REPORT_TYPE = :f_rtype"; ps["f_rtype"] = f.ReportType; }
        if (!string.IsNullOrWhiteSpace(f.ZoneName)) { sql += " AND UPPER(ZONE_NAME) LIKE UPPER(:f_zone)"; ps["f_zone"] = $"%{f.ZoneName}%"; }
        if (!string.IsNullOrWhiteSpace(f.RegionSolId)) { sql += " AND REGION_SOL_ID = :f_sol"; ps["f_sol"] = f.RegionSolId; }
        if (f.FromDate.HasValue) { sql += " AND DATE_OF_VISIT >= :f_from"; ps["f_from"] = f.FromDate.Value; }
        if (f.ToDate.HasValue) { sql += " AND DATE_OF_VISIT <= :f_to"; ps["f_to"] = f.ToDate.Value; }
        return sql;
    }

    private static string BuildSubmittedFilter(RoVisitDashboardFilter f, out Dictionary<string, object> ps)
    {
        ps = new Dictionary<string, object>();
        var sql = "";
        if (!string.IsNullOrWhiteSpace(f.ReportType)) { sql += " AND REPORT_TYPE = :f_rtype"; ps["f_rtype"] = f.ReportType; }
        if (!string.IsNullOrWhiteSpace(f.ZoneName)) { sql += " AND UPPER(ZONE_NAME) LIKE UPPER(:f_zone)"; ps["f_zone"] = $"%{f.ZoneName}%"; }
        if (!string.IsNullOrWhiteSpace(f.RegionSolId)) { sql += " AND REGION_SOL_ID = :f_sol"; ps["f_sol"] = f.RegionSolId; }
        if (f.FromDate.HasValue) { sql += " AND DATE_OF_VISIT >= :f_from"; ps["f_from"] = f.FromDate.Value; }
        if (f.ToDate.HasValue) { sql += " AND DATE_OF_VISIT <= :f_to"; ps["f_to"] = f.ToDate.Value; }
        return sql;
    }

    // ── SOL/Zone email lookup ────────────────────────────────────────────────
    public async Task<string?> GetSolEmailAsync(string code, string levelType, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT EMAIL_ID FROM RP_M_SOL_EMAIL WHERE UPPER(CODE)=UPPER(:p_code) AND LEVEL_TYPE=:p_level AND IS_ACTIVE=1", _uow.OracleTx);
        cmd.Parameters.AddIn("p_code", code);
        cmd.Parameters.AddIn("p_level", levelType);
        return await cmd.ScalarAsync<string>(ct);
    }

    private async Task SaveChildrenAsync(long reportId, RoVisitReport report, CancellationToken ct)
    {
        await ReplaceExecSummaryAsync(reportId, report.ExecSummary, ct);
        await ReplaceMetricsAsync(reportId, report.Metrics, ct);
        await ReplaceFindingsAsync(reportId, report.Findings, ct);
        await ReplacePendingIssuesAsync(reportId, report.PendingIssues, ct);
        await ReplaceAssessmentAsync(reportId, report.Assessment, ct);
    }

    // ── Exec Summary (table 2) ──────────────────────────────────────────────
    private async Task<IReadOnlyList<RoVisitExecSummaryRow>> ListExecSummaryAsync(long reportId, CancellationToken ct)
    {
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID, SR_NO, FOCUS_AREA, OBSERVATION, ACTION_REQUIRED, TARGET_DATE FROM RP_RO_VISIT_EXEC_SUMMARY WHERE REPORT_ID=:p_rid ORDER BY SR_NO", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(r => new RoVisitExecSummaryRow
        {
            Id = Long(r, "ID"), SrNo = (int)Long(r, "SR_NO"), FocusArea = Str(r, "FOCUS_AREA"),
            Observation = StrN(r, "OBSERVATION"), ActionRequired = StrN(r, "ACTION_REQUIRED"), TargetDate = DtN(r, "TARGET_DATE")
        }, ct);
    }

    private async Task ReplaceExecSummaryAsync(long reportId, List<RoVisitExecSummaryRow> rows, CancellationToken ct)
    {
        using (var del = _uow.OracleConn.Cmd("DELETE FROM RP_RO_VISIT_EXEC_SUMMARY WHERE REPORT_ID=:p_rid", _uow.OracleTx))
        { del.Parameters.AddIn("p_rid", reportId); await del.ExecAsync(ct); }

        foreach (var row in rows)
        {
            using var ins = _uow.OracleConn.Cmd(
                @"INSERT INTO RP_RO_VISIT_EXEC_SUMMARY (REPORT_ID, SR_NO, FOCUS_AREA, OBSERVATION, ACTION_REQUIRED, TARGET_DATE)
                  VALUES (:p_rid,:p_sr,:p_area,:p_obs,:p_act,:p_target)", _uow.OracleTx);
            ins.Parameters.AddIn("p_rid", reportId);
            ins.Parameters.AddIn("p_sr", row.SrNo);
            ins.Parameters.AddIn("p_area", row.FocusArea);
            ins.Parameters.AddIn("p_obs", (object?)row.Observation ?? DBNull.Value);
            ins.Parameters.AddIn("p_act", (object?)row.ActionRequired ?? DBNull.Value);
            ins.Parameters.AddIn("p_target", OracleDbType.Date, (object?)row.TargetDate ?? DBNull.Value);
            await ins.ExecAsync(ct);
        }
    }

    // ── Metrics (tables 3-9 + 10) ────────────────────────────────────────────
    private async Task<IReadOnlyList<RoVisitMetricCell>> ListMetricsAsync(long reportId, CancellationToken ct)
    {
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT PANEL_CODE, ROW_CODE, COL_CODE, VALUE_TEXT, IS_AUTO_FILLED FROM RP_RO_VISIT_METRIC WHERE REPORT_ID=:p_rid", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(r => new RoVisitMetricCell
        {
            PanelCode = Str(r, "PANEL_CODE"), RowCode = Str(r, "ROW_CODE"), ColCode = Str(r, "COL_CODE"),
            ValueText = StrN(r, "VALUE_TEXT"), IsAutoFilled = Bool(r, "IS_AUTO_FILLED")
        }, ct);
    }

    private async Task ReplaceMetricsAsync(long reportId, List<RoVisitMetricCell> cells, CancellationToken ct)
    {
        using (var del = _uow.OracleConn.Cmd("DELETE FROM RP_RO_VISIT_METRIC WHERE REPORT_ID=:p_rid", _uow.OracleTx))
        { del.Parameters.AddIn("p_rid", reportId); await del.ExecAsync(ct); }

        foreach (var c in cells)
        {
            if (string.IsNullOrWhiteSpace(c.ValueText)) continue;
            using var ins = _uow.OracleConn.Cmd(
                @"INSERT INTO RP_RO_VISIT_METRIC (REPORT_ID, PANEL_CODE, ROW_CODE, COL_CODE, VALUE_TEXT, IS_AUTO_FILLED)
                  VALUES (:p_rid,:p_panel,:p_row,:p_col,:p_val,:p_auto)", _uow.OracleTx);
            ins.Parameters.AddIn("p_rid", reportId);
            ins.Parameters.AddIn("p_panel", c.PanelCode);
            ins.Parameters.AddIn("p_row", c.RowCode);
            ins.Parameters.AddIn("p_col", c.ColCode);
            ins.Parameters.AddIn("p_val", c.ValueText);
            ins.Parameters.AddIn("p_auto", c.IsAutoFilled ? 1 : 0);
            await ins.ExecAsync(ct);
        }
    }

    // ── Findings (table 11) ──────────────────────────────────────────────────
    private async Task<IReadOnlyList<RoVisitFindingRow>> ListFindingsAsync(long reportId, CancellationToken ct)
    {
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID, SR_NO, FINDING, RISK_SEVERITY, ACTION_REQUIRED, TARGET_DATE FROM RP_RO_VISIT_FINDING WHERE REPORT_ID=:p_rid ORDER BY SR_NO", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(r => new RoVisitFindingRow
        {
            Id = Long(r, "ID"), SrNo = (int)Long(r, "SR_NO"), Finding = StrN(r, "FINDING"),
            RiskSeverity = StrN(r, "RISK_SEVERITY"), ActionRequired = StrN(r, "ACTION_REQUIRED"), TargetDate = DtN(r, "TARGET_DATE")
        }, ct);
    }

    private async Task ReplaceFindingsAsync(long reportId, List<RoVisitFindingRow> rows, CancellationToken ct)
    {
        using (var del = _uow.OracleConn.Cmd("DELETE FROM RP_RO_VISIT_FINDING WHERE REPORT_ID=:p_rid", _uow.OracleTx))
        { del.Parameters.AddIn("p_rid", reportId); await del.ExecAsync(ct); }

        foreach (var row in rows)
        {
            using var ins = _uow.OracleConn.Cmd(
                @"INSERT INTO RP_RO_VISIT_FINDING (REPORT_ID, SR_NO, FINDING, RISK_SEVERITY, ACTION_REQUIRED, TARGET_DATE)
                  VALUES (:p_rid,:p_sr,:p_find,:p_risk,:p_act,:p_target)", _uow.OracleTx);
            ins.Parameters.AddIn("p_rid", reportId);
            ins.Parameters.AddIn("p_sr", row.SrNo);
            ins.Parameters.AddIn("p_find", (object?)row.Finding ?? DBNull.Value);
            ins.Parameters.AddIn("p_risk", (object?)row.RiskSeverity ?? DBNull.Value);
            ins.Parameters.AddIn("p_act", (object?)row.ActionRequired ?? DBNull.Value);
            ins.Parameters.AddIn("p_target", OracleDbType.Date, (object?)row.TargetDate ?? DBNull.Value);
            await ins.ExecAsync(ct);
        }
    }

    // ── Pending Issues (table 12) ────────────────────────────────────────────
    private async Task<IReadOnlyList<RoVisitPendingIssueRow>> ListPendingIssuesAsync(long reportId, CancellationToken ct)
    {
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID, SR_NO, ISSUE, SUPPORT_REQUIRED, PRIORITY, EXPECTED_CLOSURE FROM RP_RO_VISIT_PENDING_ISSUE WHERE REPORT_ID=:p_rid ORDER BY SR_NO", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(r => new RoVisitPendingIssueRow
        {
            Id = Long(r, "ID"), SrNo = (int)Long(r, "SR_NO"), Issue = StrN(r, "ISSUE"),
            SupportRequired = StrN(r, "SUPPORT_REQUIRED"), Priority = StrN(r, "PRIORITY"), ExpectedClosure = DtN(r, "EXPECTED_CLOSURE")
        }, ct);
    }

    private async Task ReplacePendingIssuesAsync(long reportId, List<RoVisitPendingIssueRow> rows, CancellationToken ct)
    {
        using (var del = _uow.OracleConn.Cmd("DELETE FROM RP_RO_VISIT_PENDING_ISSUE WHERE REPORT_ID=:p_rid", _uow.OracleTx))
        { del.Parameters.AddIn("p_rid", reportId); await del.ExecAsync(ct); }

        foreach (var row in rows)
        {
            using var ins = _uow.OracleConn.Cmd(
                @"INSERT INTO RP_RO_VISIT_PENDING_ISSUE (REPORT_ID, SR_NO, ISSUE, SUPPORT_REQUIRED, PRIORITY, EXPECTED_CLOSURE)
                  VALUES (:p_rid,:p_sr,:p_issue,:p_supp,:p_pri,:p_closure)", _uow.OracleTx);
            ins.Parameters.AddIn("p_rid", reportId);
            ins.Parameters.AddIn("p_sr", row.SrNo);
            ins.Parameters.AddIn("p_issue", (object?)row.Issue ?? DBNull.Value);
            ins.Parameters.AddIn("p_supp", (object?)row.SupportRequired ?? DBNull.Value);
            ins.Parameters.AddIn("p_pri", (object?)row.Priority ?? DBNull.Value);
            ins.Parameters.AddIn("p_closure", OracleDbType.Date, (object?)row.ExpectedClosure ?? DBNull.Value);
            await ins.ExecAsync(ct);
        }
    }

    // ── Overall Assessment ───────────────────────────────────────────────────
    private async Task<IReadOnlyList<RoVisitAssessmentRow>> ListAssessmentAsync(long reportId, CancellationToken ct)
    {
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID, AREA_CODE, AREA_LABEL, ASSESSMENT, COMMENTS FROM RP_RO_VISIT_ASSESSMENT WHERE REPORT_ID=:p_rid ORDER BY ID", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(r => new RoVisitAssessmentRow
        {
            Id = Long(r, "ID"), AreaCode = Str(r, "AREA_CODE"), AreaLabel = Str(r, "AREA_LABEL"),
            Assessment = StrN(r, "ASSESSMENT"), Comments = StrN(r, "COMMENTS")
        }, ct);
    }

    private async Task ReplaceAssessmentAsync(long reportId, List<RoVisitAssessmentRow> rows, CancellationToken ct)
    {
        using (var del = _uow.OracleConn.Cmd("DELETE FROM RP_RO_VISIT_ASSESSMENT WHERE REPORT_ID=:p_rid", _uow.OracleTx))
        { del.Parameters.AddIn("p_rid", reportId); await del.ExecAsync(ct); }

        foreach (var row in rows)
        {
            using var ins = _uow.OracleConn.Cmd(
                @"INSERT INTO RP_RO_VISIT_ASSESSMENT (REPORT_ID, AREA_CODE, AREA_LABEL, ASSESSMENT, COMMENTS)
                  VALUES (:p_rid,:p_code,:p_label,:p_assess,:p_comments)", _uow.OracleTx);
            ins.Parameters.AddIn("p_rid", reportId);
            ins.Parameters.AddIn("p_code", row.AreaCode);
            ins.Parameters.AddIn("p_label", row.AreaLabel);
            ins.Parameters.AddIn("p_assess", (object?)row.Assessment ?? DBNull.Value);
            ins.Parameters.AddIn("p_comments", (object?)row.Comments ?? DBNull.Value);
            await ins.ExecAsync(ct);
        }
    }

    // ── mapping helpers ──────────────────────────────────────────────────────
    private static void BindReport(OracleCommand cmd, RoVisitReport r)
    {
        cmd.Parameters.AddIn("p_rtype", r.ReportType);
        cmd.Parameters.AddIn("p_sol", r.RegionSolId);
        cmd.Parameters.AddIn("p_rname", (object?)r.RegionName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_zname", (object?)r.ZoneName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rhcode", (object?)r.RegionalHeadEmpCode ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rhname", (object?)r.RegionalHeadName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rhsince", OracleDbType.Date, (object?)r.RhWorkingSince ?? DBNull.Value);
        cmd.Parameters.AddIn("p_ahcode", (object?)r.AssuranceHeadEmpCode ?? DBNull.Value);
        cmd.Parameters.AddIn("p_ahname", (object?)r.AssuranceHeadName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_ahsince", OracleDbType.Date, (object?)r.AhWorkingSince ?? DBNull.Value);
        cmd.Parameters.AddIn("p_vocode", r.VisitingOfficialEmpCode);
        cmd.Parameters.AddIn("p_voname", r.VisitingOfficialName);
        cmd.Parameters.AddIn("p_desg", r.Designation);
        cmd.Parameters.AddIn("p_dov", OracleDbType.Date, (object?)r.DateOfVisit ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rdate", OracleDbType.Date, (object?)r.ReportDate ?? DBNull.Value);
        cmd.Parameters.AddIn("p_suspcom", OracleDbType.Clob, (object?)r.SuspenseComments ?? DBNull.Value);
        cmd.Parameters.AddIn("p_remarks", OracleDbType.Clob, (object?)r.OverallRemarks ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rahname", (object?)r.RahName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rahcode", (object?)r.RahEmpCode ?? DBNull.Value);
        cmd.Parameters.AddIn("p_status", r.Status);
        cmd.Parameters.AddIn("p_bname", (object?)r.BranchName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_bcode", (object?)r.BranchCode ?? DBNull.Value);
        cmd.Parameters.AddIn("p_dopen", OracleDbType.Date, (object?)r.DateOfOpening ?? DBNull.Value);
        cmd.Parameters.AddIn("p_bcat", (object?)r.BranchCategory ?? DBNull.Value);
        cmd.Parameters.AddIn("p_arating", (object?)r.AuditRating ?? DBNull.Value);
        cmd.Parameters.AddIn("p_qyear", (object?)r.QuarterYear ?? DBNull.Value);
        cmd.Parameters.AddIn("p_bmname", (object?)r.BranchManagerName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_bmsince", OracleDbType.Date, (object?)r.BmWorkingSince ?? DBNull.Value);
        cmd.Parameters.AddIn("p_bmsignoff", (object?)r.BranchManagerSignoffName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_regcom", OracleDbType.Clob, (object?)r.RegistersComments ?? DBNull.Value);
    }

    private static RoVisitReport MapReport(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), ReportType = StrN(r, "REPORT_TYPE") ?? "RO_VISIT",
        RegionSolId = Str(r, "REGION_SOL_ID"), RegionName = StrN(r, "REGION_NAME"),
        ZoneName = StrN(r, "ZONE_NAME"), RegionalHeadEmpCode = StrN(r, "REGIONAL_HEAD_EMP_CODE"),
        RegionalHeadName = StrN(r, "REGIONAL_HEAD_NAME"), RhWorkingSince = DtN(r, "RH_WORKING_SINCE"),
        AssuranceHeadEmpCode = StrN(r, "ASSURANCE_HEAD_EMP_CODE"), AssuranceHeadName = StrN(r, "ASSURANCE_HEAD_NAME"),
        AhWorkingSince = DtN(r, "AH_WORKING_SINCE"), VisitingOfficialEmpCode = Str(r, "VISITING_OFFICIAL_EMP_CODE"),
        VisitingOfficialName = Str(r, "VISITING_OFFICIAL_NAME"), Designation = StrN(r, "DESIGNATION") ?? "Zonal Assurance Head",
        DateOfVisit = DtN(r, "DATE_OF_VISIT"), ReportDate = DtN(r, "REPORT_DATE"),
        SuspenseComments = StrN(r, "SUSPENSE_COMMENTS"), OverallRemarks = StrN(r, "OVERALL_REMARKS"),
        RahName = StrN(r, "RAH_NAME"), RahEmpCode = StrN(r, "RAH_EMP_CODE"), Status = Str(r, "STATUS"),
        CreatedByEmp = Str(r, "CREATED_BY_EMP"), CreatedAt = Dt(r, "CREATED_AT"),
        BranchName = StrN(r, "BRANCH_NAME"), BranchCode = StrN(r, "BRANCH_CODE"), DateOfOpening = DtN(r, "DATE_OF_OPENING"),
        BranchCategory = StrN(r, "BRANCH_CATEGORY"), AuditRating = StrN(r, "AUDIT_RATING"), QuarterYear = StrN(r, "QUARTER_YEAR"),
        BranchManagerName = StrN(r, "BRANCH_MANAGER_NAME"), BmWorkingSince = DtN(r, "BM_WORKING_SINCE"),
        BranchManagerSignoffName = StrN(r, "BRANCH_MANAGER_SIGNOFF_NAME"), RegistersComments = StrN(r, "REGISTERS_COMMENTS")
    };
}
