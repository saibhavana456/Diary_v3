using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

/// <summary>
/// ADO.NET/Oracle implementation of <see cref="IExecBranchVisitRepo"/> — same
/// conventions as <see cref="RoVisitRepo"/> (delete+reinsert children, status-check
/// guard before update, freeze-to-JSON on submit).
/// </summary>
public sealed class ExecBranchVisitRepo : IExecBranchVisitRepo
{
    private readonly AdoUnitOfWork _uow;
    public ExecBranchVisitRepo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<ExecBranchVisit?> GetAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var visit = await GetHeaderAsync(id, ct);
        if (visit is null) return null;

        await LoadFieldsAsync(visit, ct);
        await LoadOpenRowsAsync(visit, ct);
        return visit;
    }

    public async Task<ExecBranchVisit?> GetLatestDraftForBranchAsync(string branchCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT ID FROM RP_EXEC_BRANCH_VISIT
              WHERE BRANCH_CODE = :p_bcode AND STATUS = 'DRAFT'
              ORDER BY CREATED_AT DESC FETCH FIRST 1 ROWS ONLY", _uow.OracleTx);
        cmd.Parameters.AddIn("p_bcode", branchCode);
        var id = await cmd.ScalarAsync<long?>(ct);
        return id.HasValue ? await GetAsync(id.Value, ct) : null;
    }

    public async Task<long> SaveDraftAsync(ExecBranchVisit visit, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        if (visit.Id != 0)
        {
            using var statusCmd = _uow.OracleConn.Cmd("SELECT STATUS FROM RP_EXEC_BRANCH_VISIT WHERE ID=:p_id", _uow.OracleTx);
            statusCmd.Parameters.AddIn("p_id", visit.Id);
            var status = await statusCmd.ScalarAsync<string>(ct);
            if (status == "SUBMITTED")
                throw new InvalidOperationException("This visit has already been submitted and can no longer be edited.");
        }

        long id;
        if (visit.Id == 0)
        {
            using var cmd = _uow.OracleConn.Cmd(
                @"INSERT INTO RP_EXEC_BRANCH_VISIT
                    (BRANCH_CODE, BRANCH_NAME, REGION_NAME, ZONE_NAME, BRANCH_CATEGORY, AUDIT_RATING,
                     DATE_OF_OPENING, BRANCH_MANAGER_NAME, BM_WORKING_SINCE, VISITING_OFFICIAL_EMP_CODE,
                     VISITING_OFFICIAL_NAME, DESIGNATION, DATE_OF_VISIT, STATUS, CREATED_BY_EMP, SAVED_AT)
                  VALUES
                    (:p_bcode,:p_bname,:p_rname,:p_zname,:p_bcat,:p_arating,
                     :p_dopen,:p_bmname,:p_bmsince,:p_vocode,
                     :p_voname,:p_desg,:p_dov,'DRAFT',:p_emp,SYSTIMESTAMP)
                  RETURNING ID INTO :p_id", _uow.OracleTx);
            BindHeader(cmd, visit);
            cmd.Parameters.AddIn("p_emp", visit.CreatedByEmp);
            var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
            await cmd.ExecAsync(ct);
            id = outId.OutId();
        }
        else
        {
            using var cmd = _uow.OracleConn.Cmd(
                @"UPDATE RP_EXEC_BRANCH_VISIT SET
                    BRANCH_CODE=:p_bcode, BRANCH_NAME=:p_bname, REGION_NAME=:p_rname, ZONE_NAME=:p_zname,
                    BRANCH_CATEGORY=:p_bcat, AUDIT_RATING=:p_arating, DATE_OF_OPENING=:p_dopen,
                    BRANCH_MANAGER_NAME=:p_bmname, BM_WORKING_SINCE=:p_bmsince,
                    VISITING_OFFICIAL_EMP_CODE=:p_vocode, VISITING_OFFICIAL_NAME=:p_voname,
                    DESIGNATION=:p_desg, DATE_OF_VISIT=:p_dov, SAVED_AT=SYSTIMESTAMP, UPDATED_AT=SYSTIMESTAMP
                  WHERE ID=:p_id", _uow.OracleTx);
            BindHeader(cmd, visit);
            cmd.Parameters.AddIn("p_id", visit.Id);
            await cmd.ExecAsync(ct);
            id = visit.Id;
        }

        await ReplaceFieldsAsync(id, visit, ct);
        await ReplaceOpenRowsAsync(id, visit, ct);
        return id;
    }

    public async Task SubmitAsync(long draftId, string submittedByEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        var visit = await GetAsync(draftId, ct) ?? throw new InvalidOperationException("Draft visit not found.");
        if (visit.Status == "SUBMITTED")
            throw new InvalidOperationException("This visit has already been submitted.");

        using (var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_EXEC_BRANCH_VISIT_SUBMITTED
                (DRAFT_ID, BRANCH_CODE, HEADER_SNAPSHOT_JSON, FIELDS_SNAPSHOT_JSON, OPENROWS_SNAPSHOT_JSON, SUBMITTED_BY_EMP)
              VALUES
                (:p_draft,:p_bcode,:p_header,:p_fields,:p_openrows,:p_emp)", _uow.OracleTx))
        {
            cmd.Parameters.AddIn("p_draft", draftId);
            cmd.Parameters.AddIn("p_bcode", visit.BranchCode);
            cmd.Parameters.AddIn("p_header", OracleDbType.Clob, JsonSerializer.Serialize(visit));
            cmd.Parameters.AddIn("p_fields", OracleDbType.Clob, JsonSerializer.Serialize(visit.Fields));
            cmd.Parameters.AddIn("p_openrows", OracleDbType.Clob, JsonSerializer.Serialize(visit.OpenRows));
            cmd.Parameters.AddIn("p_emp", submittedByEmp);
            await cmd.ExecAsync(ct);
        }

        using (var lockCmd = _uow.OracleConn.Cmd(
            "UPDATE RP_EXEC_BRANCH_VISIT SET STATUS='SUBMITTED', SUBMITTED_AT=SYSTIMESTAMP, UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id", _uow.OracleTx))
        {
            lockCmd.Parameters.AddIn("p_id", draftId);
            await lockCmd.ExecAsync(ct);
        }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────
    // Both DRAFT and SUBMITTED rows live in RP_EXEC_BRANCH_VISIT itself (unlike RO
    // Visit's two-table split) — the submitted table only holds an immutable JSON
    // snapshot, not queryable columns — so dashboard queries filter one table by STATUS.

    public async Task<ExecBranchVisitCounts> GetCountsAsync(ExecBranchVisitFilter filter, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        var baseSql = "SELECT STATUS, NVL(REGION_NAME, '(Not specified)') RG, COUNT(*) N FROM RP_EXEC_BRANCH_VISIT WHERE 1=1"
            + BuildFilter(filter, out var ps) + " GROUP BY STATUS, NVL(REGION_NAME, '(Not specified)')";
        using var cmd = _uow.OracleConn.Cmd(baseSql, _uow.OracleTx);
        foreach (var p in ps) cmd.Parameters.AddIn(p.Key, p.Value);

        var rows = await cmd.QueryAsync(r => new
        {
            Status = Str(r, "STATUS"),
            Region = Str(r, "RG"),
            N = (int)Long(r, "N")
        }, ct);

        var counts = new ExecBranchVisitCounts
        {
            TotalDraft = rows.Where(x => x.Status == "DRAFT").Sum(x => x.N),
            TotalSubmitted = rows.Where(x => x.Status == "SUBMITTED").Sum(x => x.N)
        };
        foreach (var g in rows.Where(x => x.Status == "SUBMITTED").GroupBy(x => x.Region))
            counts.ByRegion[g.Key] = g.Sum(x => x.N);

        return counts;
    }

    public async Task<IReadOnlyList<ExecBranchVisitSummary>> ListAsync(ExecBranchVisitFilter filter, string? statusFilter, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        var sql = @"SELECT ID, BRANCH_CODE, BRANCH_NAME, REGION_NAME, ZONE_NAME, VISITING_OFFICIAL_NAME,
                            DATE_OF_VISIT, STATUS, SAVED_AT, SUBMITTED_AT
                    FROM RP_EXEC_BRANCH_VISIT WHERE 1=1" + BuildFilter(filter, out var ps);
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            sql += " AND STATUS = :f_status";
            ps["f_status"] = statusFilter;
        }
        sql += " ORDER BY COALESCE(SUBMITTED_AT, SAVED_AT) DESC NULLS LAST";

        using var cmd = _uow.OracleConn.Cmd(sql, _uow.OracleTx);
        foreach (var p in ps) cmd.Parameters.AddIn(p.Key, p.Value);

        return await cmd.QueryAsync(r => new ExecBranchVisitSummary
        {
            Id = Long(r, "ID"),
            BranchCode = Str(r, "BRANCH_CODE"),
            BranchName = StrN(r, "BRANCH_NAME"),
            RegionName = StrN(r, "REGION_NAME"),
            ZoneName = StrN(r, "ZONE_NAME"),
            VisitingOfficialName = Str(r, "VISITING_OFFICIAL_NAME"),
            DateOfVisit = DtN(r, "DATE_OF_VISIT"),
            Status = Str(r, "STATUS"),
            SavedAt = DtN(r, "SAVED_AT"),
            SubmittedAt = DtN(r, "SUBMITTED_AT")
        }, ct);
    }

    private static string BuildFilter(ExecBranchVisitFilter f, out Dictionary<string, object> ps)
    {
        ps = new Dictionary<string, object>();
        var sql = "";
        if (!string.IsNullOrWhiteSpace(f.BranchCode)) { sql += " AND BRANCH_CODE = :f_bcode"; ps["f_bcode"] = f.BranchCode; }
        if (!string.IsNullOrWhiteSpace(f.RegionName)) { sql += " AND UPPER(REGION_NAME) LIKE UPPER(:f_region)"; ps["f_region"] = $"%{f.RegionName}%"; }
        if (!string.IsNullOrWhiteSpace(f.ZoneName)) { sql += " AND UPPER(ZONE_NAME) LIKE UPPER(:f_zone)"; ps["f_zone"] = $"%{f.ZoneName}%"; }
        if (f.FromDate.HasValue) { sql += " AND DATE_OF_VISIT >= :f_from"; ps["f_from"] = f.FromDate.Value; }
        if (f.ToDate.HasValue) { sql += " AND DATE_OF_VISIT <= :f_to"; ps["f_to"] = f.ToDate.Value; }
        return sql;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void BindHeader(OracleCommand cmd, ExecBranchVisit v)
    {
        cmd.Parameters.AddIn("p_bcode", v.BranchCode);
        cmd.Parameters.AddIn("p_bname", (object?)v.BranchName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rname", (object?)v.RegionName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_zname", (object?)v.ZoneName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_bcat", (object?)v.BranchCategory ?? DBNull.Value);
        cmd.Parameters.AddIn("p_arating", (object?)v.AuditRating ?? DBNull.Value);
        cmd.Parameters.AddIn("p_dopen", OracleDbType.Date, (object?)v.DateOfOpening ?? DBNull.Value);
        cmd.Parameters.AddIn("p_bmname", (object?)v.BranchManagerName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_bmsince", OracleDbType.Date, (object?)v.BmWorkingSince ?? DBNull.Value);
        cmd.Parameters.AddIn("p_vocode", v.VisitingOfficialEmpCode);
        cmd.Parameters.AddIn("p_voname", v.VisitingOfficialName);
        cmd.Parameters.AddIn("p_desg", (object?)v.Designation ?? DBNull.Value);
        cmd.Parameters.AddIn("p_dov", OracleDbType.Date, (object?)v.DateOfVisit ?? DBNull.Value);
    }

    private async Task<ExecBranchVisit?> GetHeaderAsync(long id, CancellationToken ct)
    {
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT ID, BRANCH_CODE, BRANCH_NAME, REGION_NAME, ZONE_NAME, BRANCH_CATEGORY, AUDIT_RATING,
                     DATE_OF_OPENING, BRANCH_MANAGER_NAME, BM_WORKING_SINCE, VISITING_OFFICIAL_EMP_CODE,
                     VISITING_OFFICIAL_NAME, DESIGNATION, DATE_OF_VISIT, STATUS, CREATED_BY_EMP, SAVED_AT, SUBMITTED_AT
              FROM RP_EXEC_BRANCH_VISIT WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);

        var rows = await cmd.QueryAsync(r => new ExecBranchVisit
        {
            Id = Long(r, "ID"),
            BranchCode = Str(r, "BRANCH_CODE"),
            BranchName = StrN(r, "BRANCH_NAME"),
            RegionName = StrN(r, "REGION_NAME"),
            ZoneName = StrN(r, "ZONE_NAME"),
            BranchCategory = StrN(r, "BRANCH_CATEGORY"),
            AuditRating = StrN(r, "AUDIT_RATING"),
            DateOfOpening = DtN(r, "DATE_OF_OPENING"),
            BranchManagerName = StrN(r, "BRANCH_MANAGER_NAME"),
            BmWorkingSince = DtN(r, "BM_WORKING_SINCE"),
            VisitingOfficialEmpCode = Str(r, "VISITING_OFFICIAL_EMP_CODE"),
            VisitingOfficialName = Str(r, "VISITING_OFFICIAL_NAME"),
            Designation = StrN(r, "DESIGNATION"),
            DateOfVisit = DtN(r, "DATE_OF_VISIT"),
            Status = Str(r, "STATUS"),
            CreatedByEmp = Str(r, "CREATED_BY_EMP"),
            SavedAt = DtN(r, "SAVED_AT"),
            SubmittedAt = DtN(r, "SUBMITTED_AT")
        }, ct);

        return rows.FirstOrDefault();
    }

    private async Task LoadFieldsAsync(ExecBranchVisit visit, CancellationToken ct)
    {
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT FIELD_KEY, VALUE_TEXT, IS_AUTO_FILLED FROM RP_EXEC_BRANCH_VISIT_FIELD WHERE VISIT_ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", visit.Id);

        var rows = await cmd.QueryAsync(r => new
        {
            Key = Str(r, "FIELD_KEY"),
            Value = StrN(r, "VALUE_TEXT") ?? "",
            Auto = Long(r, "IS_AUTO_FILLED") == 1
        }, ct);

        foreach (var row in rows)
        {
            visit.Fields[row.Key] = row.Value;
            if (row.Auto) visit.AutoFilledKeys.Add(row.Key);
        }
    }

    private async Task ReplaceFieldsAsync(long visitId, ExecBranchVisit visit, CancellationToken ct)
    {
        using (var del = _uow.OracleConn.Cmd("DELETE FROM RP_EXEC_BRANCH_VISIT_FIELD WHERE VISIT_ID=:p_id", _uow.OracleTx))
        { del.Parameters.AddIn("p_id", visitId); await del.ExecAsync(ct); }

        foreach (var (key, value) in visit.Fields)
        {
            if (string.IsNullOrEmpty(value)) continue; // don't persist empty cells
            using var ins = _uow.OracleConn.Cmd(
                @"INSERT INTO RP_EXEC_BRANCH_VISIT_FIELD (VISIT_ID, FIELD_KEY, VALUE_TEXT, IS_AUTO_FILLED)
                  VALUES (:p_id,:p_key,:p_val,:p_auto)", _uow.OracleTx);
            ins.Parameters.AddIn("p_id", visitId);
            ins.Parameters.AddIn("p_key", key);
            ins.Parameters.AddIn("p_val", value);
            ins.Parameters.AddIn("p_auto", visit.AutoFilledKeys.Contains(key) ? 1 : 0);
            await ins.ExecAsync(ct);
        }
    }

    private async Task LoadOpenRowsAsync(ExecBranchVisit visit, CancellationToken ct)
    {
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT TABLE_KEY, ROW_LABEL, SORT_ORDER FROM RP_EXEC_BRANCH_VISIT_OPENROW
              WHERE VISIT_ID=:p_id ORDER BY TABLE_KEY, SORT_ORDER", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", visit.Id);

        var rows = await cmd.QueryAsync(r => new
        {
            TableKey = Str(r, "TABLE_KEY"),
            RowLabel = Str(r, "ROW_LABEL")
        }, ct);

        foreach (var row in rows)
        {
            if (!visit.OpenRows.TryGetValue(row.TableKey, out var list))
                visit.OpenRows[row.TableKey] = list = new List<string>();
            list.Add(row.RowLabel);
        }
    }

    private async Task ReplaceOpenRowsAsync(long visitId, ExecBranchVisit visit, CancellationToken ct)
    {
        using (var del = _uow.OracleConn.Cmd("DELETE FROM RP_EXEC_BRANCH_VISIT_OPENROW WHERE VISIT_ID=:p_id", _uow.OracleTx))
        { del.Parameters.AddIn("p_id", visitId); await del.ExecAsync(ct); }

        foreach (var (tableKey, rowLabels) in visit.OpenRows)
        {
            for (var i = 0; i < rowLabels.Count; i++)
            {
                using var ins = _uow.OracleConn.Cmd(
                    @"INSERT INTO RP_EXEC_BRANCH_VISIT_OPENROW (VISIT_ID, TABLE_KEY, ROW_LABEL, SORT_ORDER)
                      VALUES (:p_id,:p_tkey,:p_rlabel,:p_sort)", _uow.OracleTx);
                ins.Parameters.AddIn("p_id", visitId);
                ins.Parameters.AddIn("p_tkey", tableKey);
                ins.Parameters.AddIn("p_rlabel", rowLabels[i]);
                ins.Parameters.AddIn("p_sort", i);
                await ins.ExecAsync(ct);
            }
        }
    }
}
