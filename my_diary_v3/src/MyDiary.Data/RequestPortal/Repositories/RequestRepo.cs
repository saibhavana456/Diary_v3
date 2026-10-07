using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Dtos;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class RequestRepo : IRequestRepo
{
    private readonly AdoUnitOfWork _uow;
    private readonly IOrganisationsDbFactory _orgFactory;
    public RequestRepo(AdoUnitOfWork uow, IOrganisationsDbFactory orgFactory)
    {
        _uow = uow;
        _orgFactory = orgFactory;
    }

    public async Task<long> InsertAsync(Request r, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_REQUEST(ID,REQ_NO,REQUEST_TYPE_ID,UNIT_ID,VERTICAL_ID,DEPARTMENT_ID,ACTIVITY_ID,
                                   RAISED_BY,RAISED_BY_EMP,SUBJECT,DESCRIPTION,CURRENT_LEVEL,STATUS,SLA_DUE_UTC,
                                   EXT_ALERT_ID,EXT_SOURCE)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:ReqNo,:RequestTypeId,:UnitId,:VerticalId,:DepartmentId,:ActivityId,
                   :RaisedBy,:RaisedByEmp,:Subject,:Description,:CurrentLevel,:Status,:SlaDueUtc,
                   :ExtAlertId,:ExtSource)
            RETURNING ID INTO :NewId", _uow.OracleTx);
        cmd.Parameters.AddIn("ReqNo", r.ReqNo);
        cmd.Parameters.AddIn("RequestTypeId", r.RequestTypeId);
        cmd.Parameters.AddIn("UnitId", r.UnitId);
        cmd.Parameters.AddIn("VerticalId", r.VerticalId);
        cmd.Parameters.AddIn("DepartmentId", r.DepartmentId);
        cmd.Parameters.AddIn("ActivityId", r.ActivityId);
        cmd.Parameters.AddIn("RaisedBy", OracleDbType.Int64, r.RaisedBy == 0 ? (object?)null : r.RaisedBy);
        cmd.Parameters.AddIn("RaisedByEmp", r.RaisedByEmp);
        cmd.Parameters.AddIn("Subject", r.Subject);
        cmd.Parameters.AddIn("Description", r.Description);
        cmd.Parameters.AddIn("CurrentLevel", r.CurrentLevel);
        cmd.Parameters.AddIn("Status", (int)r.Status);
        cmd.Parameters.AddIn("SlaDueUtc", OracleDbType.TimeStamp, r.SlaDueUtc);
        cmd.Parameters.AddIn("ExtAlertId", OracleDbType.Varchar2, string.IsNullOrEmpty(r.ExtAlertId) ? (object?)null : r.ExtAlertId);
        cmd.Parameters.AddIn("ExtSource", OracleDbType.Varchar2, string.IsNullOrEmpty(r.ExtSource) ? (object?)null : r.ExtSource);
        var outId = cmd.Parameters.AddOut("NewId", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task<Request?> GetAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,REQ_NO,REQUEST_TYPE_ID,UNIT_ID,VERTICAL_ID,DEPARTMENT_ID,ACTIVITY_ID,
                   RAISED_BY,RAISED_BY_EMP,SUBJECT,DESCRIPTION,CURRENT_LEVEL,STATUS,SLA_DUE_UTC,
                   CREATED_AT,UPDATED_AT,CLOSED_AT,ROW_VERSION
            FROM RP_REQUEST WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        return await cmd.QueryOneAsync(MapRequest, ct);
    }

    public async Task<RequestDetail?> GetDetailAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        // Step 1: Fetch request data from RP_Owner (no cross-DB join)
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT r.ID, r.REQ_NO, rt.NAME AS REQUEST_TYPE_NAME, u.NAME AS UNIT_NAME,
                   v.NAME AS VERTICAL_NAME, d.NAME AS DEPARTMENT_NAME, a.NAME AS ACTIVITY_NAME,
                   r.SUBJECT, DBMS_LOB.SUBSTR(r.DESCRIPTION,4000,1) AS DESCRIPTION,
                   r.CURRENT_LEVEL,
                   CASE r.STATUS WHEN 0 THEN 'Draft' WHEN 1 THEN 'Submitted' WHEN 2 THEN 'InProgress'
                     WHEN 3 THEN 'ClarificationSought' WHEN 4 THEN 'ReturnedToEmployee'
                     WHEN 5 THEN 'Resolved' WHEN 6 THEN 'Closed' WHEN 7 THEN 'Reopened' WHEN 8 THEN 'Cancelled'
                   END AS STATUS,
                   r.SLA_DUE_UTC, r.CREATED_AT, r.CLOSED_AT, r.RAISED_BY_EMP
            FROM RP_REQUEST r
            JOIN RP_M_REQUEST_TYPE rt ON rt.ID=r.REQUEST_TYPE_ID
            JOIN RP_M_UNIT u ON u.ID=r.UNIT_ID
            JOIN RP_M_VERTICAL v ON v.ID=r.VERTICAL_ID
            JOIN RP_M_DEPARTMENT d ON d.ID=r.DEPARTMENT_ID
            JOIN RP_M_ACTIVITY a ON a.ID=r.ACTIVITY_ID
            WHERE r.ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        var detail = await cmd.QueryOneAsync(r => new
        {
            Id = Long(r, "ID"), ReqNo = Str(r, "REQ_NO"),
            RequestTypeName = Str(r, "REQUEST_TYPE_NAME"), UnitName = Str(r, "UNIT_NAME"),
            VerticalName = Str(r, "VERTICAL_NAME"), DepartmentName = Str(r, "DEPARTMENT_NAME"),
            ActivityName = Str(r, "ACTIVITY_NAME"), Subject = Str(r, "SUBJECT"),
            Description = Str(r, "DESCRIPTION"), CurrentLevel = Int(r, "CURRENT_LEVEL"),
            Status = Str(r, "STATUS"), SlaDueUtc = DtN(r, "SLA_DUE_UTC"),
            CreatedAt = Dt(r, "CREATED_AT"), ClosedAt = DtN(r, "CLOSED_AT"),
            RaisedByEmp = StrN(r, "RAISED_BY_EMP")
        }, ct);

        if (detail is null) return null;

        // Step 2: Fetch staff name/email from Organisations database
        string? raisedByName = null, raisedByEmail = null;
        if (!string.IsNullOrEmpty(detail.RaisedByEmp))
        {
            await using var orgConn = await _orgFactory.OpenAsync(ct);
            using var staffCmd = orgConn.Cmd(
                "SELECT name, email_id AS EMAIL FROM staff_details WHERE pf_no=:p_emp", null);
            staffCmd.Parameters.AddIn("p_emp", detail.RaisedByEmp);
            var staffRows = await staffCmd.QueryAsync(r => new StaffInfo(Str(r, "NAME"), StrN(r, "EMAIL")), ct);
            var staff = staffRows.FirstOrDefault();
            if (staff is not null)
            {
                raisedByName = staff.Name;
                raisedByEmail = staff.Email;
            }
        }

        return new RequestDetail(
            detail.Id, detail.ReqNo,
            detail.RequestTypeName, detail.UnitName,
            detail.VerticalName, detail.DepartmentName,
            detail.ActivityName, detail.Subject,
            detail.Description, detail.CurrentLevel,
            detail.Status, detail.SlaDueUtc,
            detail.CreatedAt, detail.ClosedAt,
            raisedByName ?? "", raisedByEmail
        );
    }

    public async Task<(IReadOnlyList<RequestListItem> Items, int Total)> ListAsync(
        FilterDto f, string? assignedToEmpCode, string? raisedByEmpCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        var where = new List<string>();
        var paramList = new List<(string Name, OracleDbType Type, object Value)>();

        void AddWhere(string clause, string name, OracleDbType type, object val)
        { where.Add(clause); paramList.Add((name, type, val)); }

        if (f.RequestTypeId.HasValue) AddWhere("r.REQUEST_TYPE_ID=:p_rt", "p_rt", OracleDbType.Int64, f.RequestTypeId.Value);
        if (f.UnitTypeId.HasValue)    AddWhere("u.UNIT_TYPE_ID=:p_ut",    "p_ut", OracleDbType.Int64, f.UnitTypeId.Value);
        if (f.UnitId.HasValue)        AddWhere("r.UNIT_ID=:p_unit",        "p_unit", OracleDbType.Int64, f.UnitId.Value);
        if (f.VerticalId.HasValue)    AddWhere("r.VERTICAL_ID=:p_v",       "p_v", OracleDbType.Int64, f.VerticalId.Value);
        if (f.DepartmentId.HasValue)  AddWhere("r.DEPARTMENT_ID=:p_dep",   "p_dep", OracleDbType.Int64, f.DepartmentId.Value);
        if (f.ActivityId.HasValue)    AddWhere("r.ACTIVITY_ID=:p_act",     "p_act", OracleDbType.Int64, f.ActivityId.Value);
        if (f.Level.HasValue)         AddWhere("r.CURRENT_LEVEL=:p_lvl",   "p_lvl", OracleDbType.Int32, f.Level.Value);
        if (!string.IsNullOrWhiteSpace(f.Status) && Enum.TryParse<RequestStatus>(f.Status, true, out var st))
            AddWhere("r.STATUS=:p_st", "p_st", OracleDbType.Int32, (int)st);
        if (f.BreachedOnly == true)
            where.Add("r.SLA_DUE_UTC IS NOT NULL AND r.SLA_DUE_UTC < SYS_EXTRACT_UTC(SYSTIMESTAMP)");
        if (f.FromDate.HasValue) AddWhere("r.CREATED_AT>=:p_from", "p_from", OracleDbType.TimeStamp, f.FromDate.Value);
        if (f.ToDate.HasValue)   AddWhere("r.CREATED_AT<:p_to",    "p_to",   OracleDbType.TimeStamp, f.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(raisedByEmpCode))
            AddWhere("r.RAISED_BY_EMP=:p_raiser", "p_raiser", OracleDbType.Varchar2, raisedByEmpCode!);
        if (!string.IsNullOrWhiteSpace(assignedToEmpCode))
        {
            where.Add("EXISTS(SELECT 1 FROM RP_REQUEST_ASSIGNEE ra WHERE ra.REQUEST_ID=r.ID AND ra.IS_ACTIVE=1 AND ra.EMP_CODE=:p_assignee)");
            paramList.Add(("p_assignee", OracleDbType.Varchar2, assignedToEmpCode!));
        }

        var whereSql  = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var sortCol   = (f.SortBy ?? "CREATED_AT") switch
        {
            "ReqNo" => "r.REQ_NO", "Subject" => "r.SUBJECT",
            "CurrentLevel" => "r.CURRENT_LEVEL", "SlaDueUtc" => "r.SLA_DUE_UTC", _ => "r.CREATED_AT"
        };
        var dir      = f.SortDesc ? "DESC" : "ASC";
        var pageSize = Math.Clamp(f.PageSize, 1, 100);
        var offset   = (Math.Max(1, f.Page) - 1) * pageSize;

        var listSql = $@"
            SELECT r.ID, r.REQ_NO, rt.NAME AS REQUEST_TYPE_NAME, r.SUBJECT, r.CURRENT_LEVEL,
                   CASE r.STATUS WHEN 0 THEN 'Draft' WHEN 1 THEN 'Submitted' WHEN 2 THEN 'InProgress'
                     WHEN 3 THEN 'ClarificationSought' WHEN 4 THEN 'ReturnedToEmployee'
                     WHEN 5 THEN 'Resolved' WHEN 6 THEN 'Closed' WHEN 7 THEN 'Reopened' WHEN 8 THEN 'Cancelled'
                   END AS STATUS,
                   r.SLA_DUE_UTC, r.CREATED_AT, r.RAISED_BY_EMP, u.NAME AS UNIT_NAME
            FROM RP_REQUEST r
            JOIN RP_M_REQUEST_TYPE rt ON rt.ID=r.REQUEST_TYPE_ID
            JOIN RP_M_UNIT u ON u.ID=r.UNIT_ID
            {whereSql}
            ORDER BY {sortCol} {dir}
            OFFSET :p_offset ROWS FETCH NEXT :p_take ROWS ONLY";

        var countSql = $"SELECT COUNT(*) FROM RP_REQUEST r JOIN RP_M_UNIT u ON u.ID=r.UNIT_ID {whereSql}";

        using var listCmd = _uow.OracleConn.Cmd(listSql, _uow.OracleTx);
        foreach (var (n, t, v) in paramList) listCmd.Parameters.AddIn(n, t, v);
        listCmd.Parameters.AddIn("p_offset", OracleDbType.Int32, offset);
        listCmd.Parameters.AddIn("p_take", OracleDbType.Int32, pageSize);

        // Fetch from RP_Owner (no cross-DB join)
        var rawItems = await listCmd.QueryAsync(r => new
        {
            Id = Long(r, "ID"), ReqNo = Str(r, "REQ_NO"),
            RequestTypeName = Str(r, "REQUEST_TYPE_NAME"), Subject = Str(r, "SUBJECT"),
            CurrentLevel = Int(r, "CURRENT_LEVEL"), Status = Str(r, "STATUS"),
            SlaDueUtc = DtN(r, "SLA_DUE_UTC"), CreatedAt = Dt(r, "CREATED_AT"),
            RaisedByEmp = StrN(r, "RAISED_BY_EMP"), UnitName = Str(r, "UNIT_NAME")
        }, ct);

        // Enrich with staff names from Organisations database
        var empCodes = rawItems.Where(x => !string.IsNullOrEmpty(x.RaisedByEmp))
                               .Select(x => x.RaisedByEmp!).Distinct().ToList();
        var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (empCodes.Count > 0)
        {
            await using var orgConn = await _orgFactory.OpenAsync(ct);
            // Batch lookup: fetch all needed names in one query
            var inList = string.Join(",", empCodes.Select((_, i) => $":p{i}"));
            using var nameCmd = orgConn.Cmd(
                $"SELECT pf_no, name FROM staff_details WHERE pf_no IN ({inList})", null);
            for (int i = 0; i < empCodes.Count; i++)
                nameCmd.Parameters.AddIn($"p{i}", empCodes[i]);
            var staffRows = await nameCmd.QueryAsync(r => (PfNo: Str(r, "PF_NO"), Name: Str(r, "NAME")), ct);
            foreach (var s in staffRows)
                nameMap[s.PfNo] = s.Name;
        }

        var items = rawItems.Select(r => new RequestListItem(
            r.Id, r.ReqNo, r.RequestTypeName, r.Subject,
            r.CurrentLevel, r.Status, r.SlaDueUtc, r.CreatedAt,
            nameMap.TryGetValue(r.RaisedByEmp ?? "", out var name) ? name : "",
            r.UnitName
        )).ToList();

        using var countCmd = _uow.OracleConn.Cmd(countSql, _uow.OracleTx);
        foreach (var (n, t, v) in paramList) countCmd.Parameters.AddIn(n, t, v);
        var total = await countCmd.ScalarAsync<int>(ct);

        return (items, total);
    }

    public async Task UpdateStatusLevelAsync(long id, RequestStatus status, int currentLevel, DateTime? slaDueUtc, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_REQUEST SET STATUS=:p_st,CURRENT_LEVEL=:p_lvl,SLA_DUE_UTC=:p_due,UPDATED_AT=SYSTIMESTAMP,ROW_VERSION=ROW_VERSION+1 WHERE ID=:p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_st", (int)status);
        cmd.Parameters.AddIn("p_lvl", currentLevel);
        cmd.Parameters.AddIn("p_due", OracleDbType.TimeStamp, slaDueUtc);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    public async Task SetClosedAsync(long id, DateTime closedAt, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_REQUEST SET CLOSED_AT=:p_at,STATUS=6,UPDATED_AT=SYSTIMESTAMP,ROW_VERSION=ROW_VERSION+1 WHERE ID=:p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_at", closedAt); cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    public async Task DeactivateAssigneesAsync(long id, int level, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_REQUEST_ASSIGNEE SET IS_ACTIVE=0 WHERE REQUEST_ID=:p_id AND LEVEL_NO=:p_lvl AND IS_ACTIVE=1",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); cmd.Parameters.AddIn("p_lvl", level);
        await cmd.ExecAsync(ct);
    }

    public async Task InsertAssigneeAsync(RequestAssignee a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_REQUEST_ASSIGNEE(ID,REQUEST_ID,LEVEL_NO,EMP_CODE,IS_ACTIVE) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_req,:p_lvl,:p_emp,1)",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_req", a.RequestId); cmd.Parameters.AddIn("p_lvl", a.LevelNo);
        cmd.Parameters.AddIn("p_emp", a.EmpCode);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetActiveAssigneeEmpCodesAsync(long id, int level, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT EMP_CODE FROM RP_REQUEST_ASSIGNEE WHERE REQUEST_ID=:p_id AND LEVEL_NO=:p_lvl AND IS_ACTIVE=1",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); cmd.Parameters.AddIn("p_lvl", level);
        return await cmd.QueryAsync(r => Str(r, "EMP_CODE"), ct);
    }

    public async Task InsertActionAsync(RequestAction a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_REQUEST_ACTION(ID,REQUEST_ID,LEVEL_NO,ACTOR_EMP_CODE,ACTION,REMARKS) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_req,:p_lvl,:p_actor,:p_action,:p_remarks)",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_req", a.RequestId); cmd.Parameters.AddIn("p_lvl", a.LevelNo);
        cmd.Parameters.AddIn("p_actor", a.ActorEmpCode); cmd.Parameters.AddIn("p_action", a.Action);
        cmd.Parameters.AddIn("p_remarks", a.Remarks);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT a.ACTED_AT,a.LEVEL_NO,a.ACTION,a.REMARKS,vw.EMPLOYEE_NAME AS ACTOR_NAME
            FROM RP_REQUEST_ACTION a
            LEFT JOIN VW_STAFF_USER_SUMMARY vw ON vw.EMPLOYEE_CODE=a.ACTOR_EMP_CODE
            WHERE a.REQUEST_ID=:p_id ORDER BY a.ACTED_AT", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        return await cmd.QueryAsync(r => new TimelineEvent(
            Dt(r, "ACTED_AT"), Int(r, "LEVEL_NO"),
            Str(r, "ACTION"), StrN(r, "REMARKS"),
            Str(r, "ACTOR_NAME")
        ), ct);
    }

    public async Task<IReadOnlyList<long>> ListIdsDueForEscalationAsync(int batchSize, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID FROM RP_REQUEST
            WHERE STATUS=2 AND CURRENT_LEVEL<4
              AND SLA_DUE_UTC IS NOT NULL AND SLA_DUE_UTC<SYS_EXTRACT_UTC(SYSTIMESTAMP)
              AND ROWNUM<=:p_take", _uow.OracleTx);
        cmd.Parameters.AddIn("p_take", batchSize);
        return await cmd.QueryAsync(r => Long(r, "ID"), ct);
    }

    public async Task InsertClarificationAsync(Clarification c, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_REQUEST_CLARIFICATION(ID,REQUEST_ID,ASKED_BY,QUESTION) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_req,:p_asked,:p_q)",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_req", c.RequestId); cmd.Parameters.AddIn("p_asked", c.AskedByEmpCode);
        cmd.Parameters.AddIn("p_q", c.Question);
        await cmd.ExecAsync(ct);
    }

    public async Task<Clarification?> GetClarificationAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,REQUEST_ID,ASKED_BY,ASKED_AT,REPLIED_AT,QUESTION,ANSWER FROM RP_REQUEST_CLARIFICATION WHERE ID=:p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        return await cmd.QueryOneAsync(MapClarification, ct);
    }

    public async Task<Clarification?> GetLatestOpenClarificationAsync(long requestId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,REQUEST_ID,ASKED_BY,ASKED_AT,REPLIED_AT,QUESTION,ANSWER
            FROM RP_REQUEST_CLARIFICATION
            WHERE REQUEST_ID=:p_id AND REPLIED_AT IS NULL
            ORDER BY ASKED_AT DESC FETCH FIRST 1 ROWS ONLY", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", requestId);
        return await cmd.QueryOneAsync(MapClarification, ct);
    }

    public async Task UpdateClarificationReplyAsync(long id, string answer, DateTime repliedAt, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_REQUEST_CLARIFICATION SET ANSWER=:p_a,REPLIED_AT=:p_at WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", answer); cmd.Parameters.AddIn("p_at", repliedAt);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    public async Task InsertSlaPauseAsync(SlaPause sp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_SLA_PAUSE_LOG(ID,REQUEST_ID,PAUSED_AT,REASON) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_req,:p_at,:p_reason)",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_req", sp.RequestId); cmd.Parameters.AddIn("p_at", sp.PausedAt);
        cmd.Parameters.AddIn("p_reason", sp.Reason); await cmd.ExecAsync(ct);
    }

    public async Task ResumeOpenSlaPauseAsync(long requestId, DateTime resumedAt, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_SLA_PAUSE_LOG SET RESUMED_AT=:p_at WHERE REQUEST_ID=:p_id AND RESUMED_AT IS NULL",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_at", resumedAt); cmd.Parameters.AddIn("p_id", requestId);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<SlaPause>> GetSlaPausesAsync(long requestId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,REQUEST_ID,PAUSED_AT,RESUMED_AT,REASON FROM RP_SLA_PAUSE_LOG WHERE REQUEST_ID=:p_id ORDER BY PAUSED_AT",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", requestId);
        return await cmd.QueryAsync(r => new SlaPause
        {
            Id = Long(r, "ID"), RequestId = Long(r, "REQUEST_ID"),
            PausedAt = Dt(r, "PAUSED_AT"), ResumedAt = DtN(r, "RESUMED_AT"),
            Reason = StrN(r, "REASON")
        }, ct);
    }

    public async Task InsertFeedbackAsync(Feedback fb, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_REQUEST_FEEDBACK(ID,REQUEST_ID,RATING,COMMENTS) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_req,:p_rating,:p_comments)",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_req", fb.RequestId); cmd.Parameters.AddIn("p_rating", fb.Rating);
        cmd.Parameters.AddIn("p_comments", fb.Comments); await cmd.ExecAsync(ct);
    }

    private static Request MapRequest(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), ReqNo = Str(r, "REQ_NO"),
        RequestTypeId = Long(r, "REQUEST_TYPE_ID"), UnitId = Long(r, "UNIT_ID"),
        VerticalId = Long(r, "VERTICAL_ID"), DepartmentId = Long(r, "DEPARTMENT_ID"),
        ActivityId = Long(r, "ACTIVITY_ID"), RaisedBy = LongN(r, "RAISED_BY") ?? 0,
        RaisedByEmp = Str(r, "RAISED_BY_EMP"),
        Subject = Str(r, "SUBJECT"), Description = StrN(r, "DESCRIPTION"),
        CurrentLevel = Int(r, "CURRENT_LEVEL"),
        Status = (RequestStatus)Int(r, "STATUS"),
        SlaDueUtc = DtN(r, "SLA_DUE_UTC"), CreatedAt = Dt(r, "CREATED_AT"),
        UpdatedAt = Dt(r, "UPDATED_AT"), ClosedAt = DtN(r, "CLOSED_AT"),
        RowVersion = Int(r, "ROW_VERSION")
    };

    private static Clarification MapClarification(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), RequestId = Long(r, "REQUEST_ID"), AskedByEmpCode = Str(r, "ASKED_BY"),
        AskedAt = Dt(r, "ASKED_AT"), RepliedAt = DtN(r, "REPLIED_AT"),
        Question = Str(r, "QUESTION"), Answer = StrN(r, "ANSWER")
    };

    /// <summary>Helper class for staff lookup from Organisations DB (reference type for QueryAsync).</summary>
    private sealed record StaffInfo(string Name, string? Email);
}
