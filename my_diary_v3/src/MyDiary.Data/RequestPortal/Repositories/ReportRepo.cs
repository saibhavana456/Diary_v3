using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class ReportRepo : IReportRepo
{
    private readonly AdoUnitOfWork _uow;
    public ReportRepo(AdoUnitOfWork uow) => _uow = uow;

    private const string SelectCols = @"
        d.ID, d.REPORT_CODE, d.REPORT_NAME, d.VERTICAL_ID, v.NAME AS VNAME, d.DEPARTMENT_ID, dep.NAME AS DNAME,
        d.SOURCE_TYPE, d.RUNNING_FROM, d.CONNECTION_NAME, d.FILE_TYPE,
        d.FILE_PATH, d.SFTP_HOST, d.SFTP_PORT, d.SFTP_USER, d.SFTP_PASSWORD, d.DB_CONN_STRING,
        d.FILE_PATH_DR, d.SFTP_HOST_DR, d.SFTP_PORT_DR, d.SFTP_USER_DR, d.SFTP_PASSWORD_DR, d.DB_CONN_STRING_DR,
        d.SQL_QUERY, d.IS_ACTIVE, d.CREATED_BY_EMP, d.CREATED_AT";

    public async Task<IReadOnlyList<ReportDef>> ListDefsAsync(long? verticalId, long? departmentId, string? nameSearch, bool activeOnly, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var sql = $@"SELECT {SelectCols}
                    FROM RP_M_REPORT_DEF d
                    JOIN RP_M_VERTICAL v ON v.ID = d.VERTICAL_ID
                    JOIN RP_M_DEPARTMENT dep ON dep.ID = d.DEPARTMENT_ID
                    WHERE 1=1";
        if (activeOnly) sql += " AND d.IS_ACTIVE=1";
        if (verticalId.HasValue) sql += " AND d.VERTICAL_ID=:p_vid";
        if (departmentId.HasValue) sql += " AND d.DEPARTMENT_ID=:p_did";
        if (!string.IsNullOrWhiteSpace(nameSearch)) sql += " AND UPPER(d.REPORT_NAME) LIKE UPPER(:p_name)";
        sql += " ORDER BY d.REPORT_NAME";

        using var cmd = _uow.OracleConn.Cmd(sql, _uow.OracleTx);
        if (verticalId.HasValue) cmd.Parameters.AddIn("p_vid", verticalId.Value);
        if (departmentId.HasValue) cmd.Parameters.AddIn("p_did", departmentId.Value);
        if (!string.IsNullOrWhiteSpace(nameSearch)) cmd.Parameters.AddIn("p_name", $"%{nameSearch}%");

        return await cmd.QueryAsync(Map, ct);
    }

    public async Task<ReportDef?> GetDefAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            $@"SELECT {SelectCols}
              FROM RP_M_REPORT_DEF d
              JOIN RP_M_VERTICAL v ON v.ID = d.VERTICAL_ID
              JOIN RP_M_DEPARTMENT dep ON dep.ID = d.DEPARTMENT_ID
              WHERE d.ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        return await cmd.QueryOneAsync(Map, ct);
    }

    public async Task<long> InsertDefAsync(ReportDefForm f, string createdByEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_M_REPORT_DEF
                (REPORT_CODE, REPORT_NAME, VERTICAL_ID, DEPARTMENT_ID, SOURCE_TYPE, RUNNING_FROM, CONNECTION_NAME, FILE_TYPE,
                 FILE_PATH, SFTP_HOST, SFTP_PORT, SFTP_USER, SFTP_PASSWORD, DB_CONN_STRING,
                 FILE_PATH_DR, SFTP_HOST_DR, SFTP_PORT_DR, SFTP_USER_DR, SFTP_PASSWORD_DR, DB_CONN_STRING_DR,
                 SQL_QUERY, CREATED_BY_EMP)
              VALUES
                (:p_code,:p_name,:p_vid,:p_did,:p_src,:p_run,:p_connname,:p_ftype,
                 :p_fpath,:p_host,:p_port,:p_user,:p_pwd,:p_conn,
                 :p_fpath_dr,:p_host_dr,:p_port_dr,:p_user_dr,:p_pwd_dr,:p_conn_dr,
                 :p_query,:p_emp)
              RETURNING ID INTO :p_id", _uow.OracleTx);
        BindForm(cmd, f);
        cmd.Parameters.AddIn("p_emp", createdByEmp);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        var id = outId.OutId();

        await LogAuditAsync(id, f.ReportName, "CREATE", createdByEmp, $"Code={f.ReportCode}, Source={f.SourceType}, RunningFrom={f.RunningFrom}", ct);
        return id;
    }

    public async Task UpdateDefAsync(ReportDefForm f, string actorEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"UPDATE RP_M_REPORT_DEF SET
                REPORT_CODE=:p_code, REPORT_NAME=:p_name, VERTICAL_ID=:p_vid, DEPARTMENT_ID=:p_did, SOURCE_TYPE=:p_src,
                RUNNING_FROM=:p_run, CONNECTION_NAME=:p_connname, FILE_TYPE=:p_ftype,
                FILE_PATH=:p_fpath, SFTP_HOST=:p_host, SFTP_PORT=:p_port, SFTP_USER=:p_user,
                SFTP_PASSWORD=:p_pwd, DB_CONN_STRING=:p_conn,
                FILE_PATH_DR=:p_fpath_dr, SFTP_HOST_DR=:p_host_dr, SFTP_PORT_DR=:p_port_dr,
                SFTP_USER_DR=:p_user_dr, SFTP_PASSWORD_DR=:p_pwd_dr, DB_CONN_STRING_DR=:p_conn_dr,
                SQL_QUERY=:p_query, UPDATED_AT=SYSTIMESTAMP
              WHERE ID=:p_id", _uow.OracleTx);
        BindForm(cmd, f);
        cmd.Parameters.AddIn("p_id", f.Id!.Value);
        await cmd.ExecAsync(ct);

        await LogAuditAsync(f.Id!.Value, f.ReportName, "UPDATE", actorEmp, $"Code={f.ReportCode}, Source={f.SourceType}, RunningFrom={f.RunningFrom}", ct);
    }

    public async Task SetActiveAsync(long id, bool isActive, string actorEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        string reportName;
        using (var nameCmd = _uow.OracleConn.Cmd("SELECT REPORT_NAME FROM RP_M_REPORT_DEF WHERE ID=:p_id", _uow.OracleTx))
        {
            nameCmd.Parameters.AddIn("p_id", id);
            reportName = await nameCmd.ScalarAsync<string>(ct) ?? "";
        }

        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_REPORT_DEF SET IS_ACTIVE=:p_a WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", isActive ? 1 : 0);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);

        await LogAuditAsync(id, reportName, isActive ? "ENABLE" : "DISABLE", actorEmp, null, ct);
    }

    // ── Report Code generation ───────────────────────────────────────────────
    public async Task<string> GenerateReportCodeAsync(long verticalId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        string verticalName;
        using (var vCmd = _uow.OracleConn.Cmd("SELECT NAME FROM RP_M_VERTICAL WHERE ID=:p_id", _uow.OracleTx))
        {
            vCmd.Parameters.AddIn("p_id", verticalId);
            verticalName = await vCmd.ScalarAsync<string>(ct) ?? "GEN";
        }

        // Prefix: first 4 alphanumeric characters of the vertical name, uppercased,
        // padded with X if the name is shorter (e.g. "HR" -> "HRXX").
        var alnum = new string(verticalName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var prefix = (alnum.Length >= 4 ? alnum[..4] : alnum.PadRight(4, 'X'));
        if (prefix.Length == 0) prefix = "GEN";

        using var countCmd = _uow.OracleConn.Cmd(
            "SELECT COUNT(*) FROM RP_M_REPORT_DEF WHERE REPORT_CODE LIKE :p_pfx", _uow.OracleTx);
        countCmd.Parameters.AddIn("p_pfx", $"{prefix}-%");
        var existing = await countCmd.ScalarAsync<int>(ct);

        return $"{prefix}-{(existing + 1):0000}";
    }

    // ── Filters (Zone/Region/Branch/Custom) ──────────────────────────────────
    public async Task<IReadOnlyList<ReportFilterDef>> ListFiltersAsync(long reportId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT ID, REPORT_ID, FILTER_TYPE, COLUMN_NAME, LABEL, SORT_ORDER, IS_ACTIVE
              FROM RP_M_REPORT_FILTER WHERE REPORT_ID=:p_rid AND IS_ACTIVE=1 ORDER BY SORT_ORDER, ID", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(MapFilter, ct);
    }

    public async Task ReplaceFiltersAsync(long reportId, IReadOnlyList<ReportFilterDef> filters, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        using (var delCmd = _uow.OracleConn.Cmd("DELETE FROM RP_M_REPORT_FILTER WHERE REPORT_ID=:p_rid", _uow.OracleTx))
        {
            delCmd.Parameters.AddIn("p_rid", reportId);
            await delCmd.ExecAsync(ct);
        }

        int sort = 0;
        foreach (var f in filters)
        {
            using var insCmd = _uow.OracleConn.Cmd(
                @"INSERT INTO RP_M_REPORT_FILTER (REPORT_ID, FILTER_TYPE, COLUMN_NAME, LABEL, SORT_ORDER)
                  VALUES (:p_rid, :p_type, :p_col, :p_label, :p_sort)", _uow.OracleTx);
            insCmd.Parameters.AddIn("p_rid", reportId);
            insCmd.Parameters.AddIn("p_type", FilterTypeCode(f.FilterType));
            insCmd.Parameters.AddIn("p_col", f.ColumnName);
            insCmd.Parameters.AddIn("p_label", f.Label);
            insCmd.Parameters.AddIn("p_sort", sort++);
            await insCmd.ExecAsync(ct);
        }
    }

    private static string FilterTypeCode(ReportFilterType t) => t switch
    {
        ReportFilterType.Zone => "ZONE",
        ReportFilterType.Region => "REGION",
        ReportFilterType.Branch => "BRANCH",
        _ => "CUSTOM"
    };

    private static ReportFilterType ParseFilterType(string s) => s switch
    {
        "ZONE" => ReportFilterType.Zone,
        "REGION" => ReportFilterType.Region,
        "BRANCH" => ReportFilterType.Branch,
        _ => ReportFilterType.Custom
    };

    private static ReportFilterDef MapFilter(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), ReportId = Long(r, "REPORT_ID"),
        FilterType = ParseFilterType(Str(r, "FILTER_TYPE")),
        ColumnName = Str(r, "COLUMN_NAME"), Label = Str(r, "LABEL"),
        SortOrder = (int)Long(r, "SORT_ORDER"), IsActive = Bool(r, "IS_ACTIVE")
    };

    // ── Audit trail ──────────────────────────────────────────────────────────
    private async Task LogAuditAsync(long reportId, string reportName, string action, string actorEmp, string? details, CancellationToken ct)
    {
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_REPORT_AUDIT_LOG (REPORT_ID, REPORT_NAME, ACTION, ACTOR_EMP, DETAILS)
              VALUES (:p_rid, :p_rname, :p_act, :p_emp, :p_det)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        cmd.Parameters.AddIn("p_rname", reportName);
        cmd.Parameters.AddIn("p_act", action);
        cmd.Parameters.AddIn("p_emp", actorEmp);
        cmd.Parameters.AddIn("p_det", details);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<ReportAuditEntry>> ListAuditAsync(long? reportId, int take, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var sql = @"SELECT * FROM (
                      SELECT ID, REPORT_ID, REPORT_NAME, ACTION, ACTOR_EMP, ACTOR_AT, DETAILS
                      FROM RP_REPORT_AUDIT_LOG WHERE 1=1";
        if (reportId.HasValue) sql += " AND REPORT_ID=:p_rid";
        sql += " ORDER BY ACTOR_AT DESC) WHERE ROWNUM <= :p_take";

        using var cmd = _uow.OracleConn.Cmd(sql, _uow.OracleTx);
        if (reportId.HasValue) cmd.Parameters.AddIn("p_rid", reportId.Value);
        cmd.Parameters.AddIn("p_take", take);
        return await cmd.QueryAsync(r => new ReportAuditEntry
        {
            Id = Long(r, "ID"), ReportId = Long(r, "REPORT_ID"), ReportName = Str(r, "REPORT_NAME"),
            Action = Str(r, "ACTION"), ActorEmp = Str(r, "ACTOR_EMP"), ActorAt = Dt(r, "ACTOR_AT"),
            Details = StrN(r, "DETAILS")
        }, ct);
    }

    // ── Access log ───────────────────────────────────────────────────────────
    public async Task LogAccessAsync(long reportId, string reportName, string accessedBy, ReportSite? runningFrom, int? rowsReturned, bool success, string? errorMsg, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_REPORT_ACCESS_LOG (REPORT_ID, REPORT_NAME, ACCESSED_BY, RUNNING_FROM, ROWS_RETURNED, SUCCESS, ERROR_MSG)
              VALUES (:p_rid, :p_rname, :p_emp, :p_run, :p_rows, :p_ok, :p_err)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        cmd.Parameters.AddIn("p_rname", reportName);
        cmd.Parameters.AddIn("p_emp", accessedBy);
        cmd.Parameters.AddIn("p_run", runningFrom is null ? null : (runningFrom == ReportSite.Dr ? "DR" : "DC"));
        cmd.Parameters.AddIn("p_rows", OracleDbType.Int32, (object?)rowsReturned ?? DBNull.Value);
        cmd.Parameters.AddIn("p_ok", success ? 1 : 0);
        cmd.Parameters.AddIn("p_err", errorMsg is { Length: > 1900 } ? errorMsg[..1900] : errorMsg);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<ReportAccessEntry>> ListAccessAsync(long? reportId, int take, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var sql = @"SELECT * FROM (
                      SELECT ID, REPORT_ID, REPORT_NAME, ACCESSED_BY, ACCESSED_AT, RUNNING_FROM, ROWS_RETURNED, SUCCESS, ERROR_MSG
                      FROM RP_REPORT_ACCESS_LOG WHERE 1=1";
        if (reportId.HasValue) sql += " AND REPORT_ID=:p_rid";
        sql += " ORDER BY ACCESSED_AT DESC) WHERE ROWNUM <= :p_take";

        using var cmd = _uow.OracleConn.Cmd(sql, _uow.OracleTx);
        if (reportId.HasValue) cmd.Parameters.AddIn("p_rid", reportId.Value);
        cmd.Parameters.AddIn("p_take", take);
        return await cmd.QueryAsync(r => new ReportAccessEntry
        {
            Id = Long(r, "ID"), ReportId = Long(r, "REPORT_ID"), ReportName = Str(r, "REPORT_NAME"),
            AccessedBy = Str(r, "ACCESSED_BY"), AccessedAt = Dt(r, "ACCESSED_AT"),
            RunningFrom = StrN(r, "RUNNING_FROM") switch { "DC" => ReportSite.Dc, "DR" => ReportSite.Dr, _ => null },
            RowsReturned = IntN(r, "ROWS_RETURNED"), Success = Bool(r, "SUCCESS"), ErrorMsg = StrN(r, "ERROR_MSG")
        }, ct);
    }

    // ── mapping helpers ──────────────────────────────────────────────────────
    private static void BindForm(OracleCommand cmd, ReportDefForm f)
    {
        cmd.Parameters.AddIn("p_code", f.ReportCode);
        cmd.Parameters.AddIn("p_name", f.ReportName);
        cmd.Parameters.AddIn("p_vid", f.VerticalId!.Value);
        cmd.Parameters.AddIn("p_did", f.DepartmentId!.Value);
        cmd.Parameters.AddIn("p_src", SourceCode(f.SourceType));
        cmd.Parameters.AddIn("p_run", f.RunningFrom == ReportSite.Dr ? "DR" : "DC");
        cmd.Parameters.AddIn("p_connname", (object?)f.ConnectionName ?? DBNull.Value);
        cmd.Parameters.AddIn("p_ftype", f.FileType);

        cmd.Parameters.AddIn("p_fpath", f.FilePath);
        cmd.Parameters.AddIn("p_host", f.SftpHost);
        cmd.Parameters.AddIn("p_port", OracleDbType.Int32, (object?)f.SftpPort ?? DBNull.Value);
        cmd.Parameters.AddIn("p_user", f.SftpUser);
        cmd.Parameters.AddIn("p_pwd", EncryptOrNull(f.SftpPassword));
        cmd.Parameters.AddIn("p_conn", EncryptOrNull(f.DbConnectionString));

        cmd.Parameters.AddIn("p_fpath_dr", f.FilePathDr);
        cmd.Parameters.AddIn("p_host_dr", f.SftpHostDr);
        cmd.Parameters.AddIn("p_port_dr", OracleDbType.Int32, (object?)f.SftpPortDr ?? DBNull.Value);
        cmd.Parameters.AddIn("p_user_dr", f.SftpUserDr);
        cmd.Parameters.AddIn("p_pwd_dr", EncryptOrNull(f.SftpPasswordDr));
        cmd.Parameters.AddIn("p_conn_dr", EncryptOrNull(f.DbConnectionStringDr));

        // Admin-authored SQL text, encrypted at rest — a DB dump or an intercepted copy of this
        // row can't be read (or tampered with and re-saved, since decryption of altered
        // ciphertext fails) without the same AES-256-GCM key used here.
        cmd.Parameters.AddIn("p_query", OracleDbType.Clob, (object?)EncryptOrNull(f.SqlQuery) ?? DBNull.Value);
    }

    /// <summary>Same AES-256-GCM scheme already used for connection strings
    /// (MyDiary.Data.Extensions.ConnectionStringDecryptor) — reused here rather than a second
    /// encryption implementation. Null/blank pass through unencrypted (nothing to protect).</summary>
    private static string? EncryptOrNull(string? plain) =>
        string.IsNullOrEmpty(plain) ? plain : MyDiary.Data.Extensions.ConnectionStringDecryptor.Encrypt(plain);

    private static string SourceCode(ReportSourceType t) => t switch
    {
        ReportSourceType.FilePath => "FILE_PATH",
        ReportSourceType.Sftp => "SFTP",
        ReportSourceType.OracleDb => "ORACLE_DB",
        ReportSourceType.SqlServerDb => "SQLSERVER_DB",
        ReportSourceType.Impala => "IMPALA",
        _ => "ORACLE_DB"
    };

    private static ReportSourceType ParseSourceType(string s) => s switch
    {
        "FILE_PATH" => ReportSourceType.FilePath,
        "SFTP" => ReportSourceType.Sftp,
        "ORACLE_DB" => ReportSourceType.OracleDb,
        "SQLSERVER_DB" => ReportSourceType.SqlServerDb,
        "IMPALA" => ReportSourceType.Impala,
        _ => ReportSourceType.OracleDb
    };

    private static ReportDef Map(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), ReportCode = Str(r, "REPORT_CODE"), ReportName = Str(r, "REPORT_NAME"),
        VerticalId = Long(r, "VERTICAL_ID"), VerticalName = Str(r, "VNAME"),
        DepartmentId = Long(r, "DEPARTMENT_ID"), DepartmentName = Str(r, "DNAME"),
        SourceType = ParseSourceType(Str(r, "SOURCE_TYPE")),
        RunningFrom = Str(r, "RUNNING_FROM") == "DR" ? ReportSite.Dr : ReportSite.Dc,
        ConnectionName = StrN(r, "CONNECTION_NAME"),
        FileType = StrN(r, "FILE_TYPE"),
        FilePath = StrN(r, "FILE_PATH"), SftpHost = StrN(r, "SFTP_HOST"), SftpPort = IntN(r, "SFTP_PORT"),
        SftpUser = StrN(r, "SFTP_USER"), SftpPassword = DecryptOrNull(StrN(r, "SFTP_PASSWORD")), DbConnectionString = DecryptOrNull(StrN(r, "DB_CONN_STRING")),
        FilePathDr = StrN(r, "FILE_PATH_DR"), SftpHostDr = StrN(r, "SFTP_HOST_DR"), SftpPortDr = IntN(r, "SFTP_PORT_DR"),
        SftpUserDr = StrN(r, "SFTP_USER_DR"), SftpPasswordDr = DecryptOrNull(StrN(r, "SFTP_PASSWORD_DR")), DbConnectionStringDr = DecryptOrNull(StrN(r, "DB_CONN_STRING_DR")),
        SqlQuery = DecryptOrNull(StrN(r, "SQL_QUERY")),
        IsActive = Bool(r, "IS_ACTIVE"), CreatedByEmp = Str(r, "CREATED_BY_EMP"), CreatedAt = Dt(r, "CREATED_AT")
    };

    /// <summary>Best-effort decrypt — rows written before this encryption was added (or a
    /// hand-edited value) pass through unchanged instead of breaking the page.</summary>
    private static string? DecryptOrNull(string? stored) =>
        MyDiary.Data.Extensions.ConnectionStringDecryptor.TryDecryptOrPassThrough(stored);
}
