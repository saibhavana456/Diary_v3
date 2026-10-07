using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class EventFeedRepo : IEventFeedRepo
{
    private readonly AdoUnitOfWork _uow;
    public EventFeedRepo(AdoUnitOfWork uow) => _uow = uow;

    // ── Verticals ─────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<Vertical>> ListActiveVerticalsAsync(CancellationToken ct = default)
    {
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,CODE,NAME FROM RP_M_VERTICAL WHERE IS_ACTIVE=1 and TYPE='1021' ORDER BY NAME", _uow.OracleTx);
        return await cmd.QueryAsync(r => new Vertical { Id = Long(r, "ID"), Code = Str(r, "CODE"), Name = Str(r, "NAME") }, ct);
    }

    public async Task<IReadOnlyList<Vertical>> ListAdminVerticalsAsync(string empCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT v.ID,v.CODE,v.NAME FROM RP_RBAC_VERTICAL_ADMIN a
              JOIN RP_M_VERTICAL v ON v.ID=a.VERTICAL_ID
              WHERE a.EMP_CODE=:p_emp AND a.IS_ACTIVE=1 AND v.IS_ACTIVE=1
              ORDER BY v.NAME", _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", empCode);
        return await cmd.QueryAsync(r => new Vertical { Id = Long(r, "ID"), Code = Str(r, "CODE"), Name = Str(r, "NAME") }, ct);
    }

    // ── Tags ──────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<EventTag>> ListTagsAsync(bool onlyEnabled, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var sql = "SELECT ID,NAME,IS_ENABLED FROM EV_TAG" + (onlyEnabled ? " WHERE IS_ENABLED=1" : "") + " ORDER BY NAME";
        using var cmd = _uow.OracleConn.Cmd(sql, _uow.OracleTx);
        return await cmd.QueryAsync(r => new EventTag { Id = Long(r, "ID"), Name = Str(r, "NAME"), IsEnabled = Bool(r, "IS_ENABLED") }, ct);
    }

    public async Task<long> InsertTagAsync(string name, string actorEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO EV_TAG(NAME,CREATED_BY_EMP) VALUES(:p_name,:p_emp) RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_name", name);
        cmd.Parameters.AddIn("p_emp", actorEmp);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task SetTagEnabledAsync(long id, bool enabled, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE EV_TAG SET IS_ENABLED=:p_en WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_en", enabled ? 1 : 0);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    // ── Events ────────────────────────────────────────────────────────────────
    public async Task<long> InsertEventAsync(CreateEventDto dto, string actorEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO EV_EVENT(VERTICAL_ID,NAME,EVENT_DATE,TIME_TEXT,VENUE,POSTED_BY_EMP)
              VALUES(:p_vid,:p_name,:p_date,:p_time,:p_venue,:p_emp) RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_vid", dto.VerticalId);
        cmd.Parameters.AddIn("p_name", dto.Name);
        cmd.Parameters.AddIn("p_date", OracleDbType.Date, (object?)dto.EventDate ?? DBNull.Value);
        cmd.Parameters.AddIn("p_time", (object?)dto.TimeText ?? DBNull.Value);
        cmd.Parameters.AddIn("p_venue", (object?)dto.Venue ?? DBNull.Value);
        cmd.Parameters.AddIn("p_emp", actorEmp);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task<IReadOnlyList<UpcomingEventItem>> ListEventsAsync(long? verticalId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var sql = @"SELECT e.ID,e.VERTICAL_ID,v.NAME AS VNAME,e.NAME,e.EVENT_DATE,e.TIME_TEXT,e.VENUE,e.POSTED_BY_EMP,e.POSTED_AT
                    FROM EV_EVENT e JOIN RP_M_VERTICAL v ON v.ID=e.VERTICAL_ID
                    WHERE e.IS_ACTIVE=1";
        if (verticalId.HasValue) sql += " AND e.VERTICAL_ID=:p_vid";
        sql += " ORDER BY e.POSTED_AT DESC";
        using var cmd = _uow.OracleConn.Cmd(sql, _uow.OracleTx);
        if (verticalId.HasValue) cmd.Parameters.AddIn("p_vid", verticalId.Value);
        return await cmd.QueryAsync(r => new UpcomingEventItem
        {
            Id = Long(r, "ID"), VerticalId = Long(r, "VERTICAL_ID"), VerticalName = Str(r, "VNAME"),
            Name = Str(r, "NAME"), EventDate = DtN(r, "EVENT_DATE"), TimeText = StrN(r, "TIME_TEXT"),
            Venue = StrN(r, "VENUE"), PostedByEmp = Str(r, "POSTED_BY_EMP"), PostedAt = Dt(r, "POSTED_AT")
        }, ct);
    }

    // ── Posts ─────────────────────────────────────────────────────────────────
    public async Task<long> InsertPostAsync(CreatePostDto dto, string actorEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO EV_POST(VERTICAL_ID,EVENT_ID,TAG_ID,CAPTION,POSTED_BY_EMP)
              VALUES(:p_vid,:p_event,:p_tag,:p_cap,:p_emp) RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_vid", dto.VerticalId);
        cmd.Parameters.AddIn("p_event", OracleDbType.Int64, (object?)dto.EventId ?? DBNull.Value);
        cmd.Parameters.AddIn("p_tag", OracleDbType.Int64, (object?)dto.TagId ?? DBNull.Value);
        cmd.Parameters.AddIn("p_cap", OracleDbType.Clob, (object?)dto.Caption ?? DBNull.Value);
        cmd.Parameters.AddIn("p_emp", actorEmp);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task InsertPostImageAsync(long postId, string filePath, string fileName, string contentType, long size, int seqNo, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO EV_POST_IMAGE(POST_ID,FILE_PATH,FILE_NAME,CONTENT_TYPE,FILE_SIZE,SEQ_NO)
              VALUES(:p_pid,:p_path,:p_name,:p_ct,:p_size,:p_seq)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_pid", postId);
        cmd.Parameters.AddIn("p_path", filePath);
        cmd.Parameters.AddIn("p_name", fileName);
        cmd.Parameters.AddIn("p_ct", contentType);
        cmd.Parameters.AddIn("p_size", size);
        cmd.Parameters.AddIn("p_seq", seqNo);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<FeedPost>> ListFeedAsync(long? verticalId, int take, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var sql = @"SELECT * FROM (
                      SELECT p.ID,p.VERTICAL_ID,v.CODE AS VCODE,v.NAME AS VNAME,p.EVENT_ID,e.NAME AS ENAME,
                             p.TAG_ID,t.NAME AS TNAME,p.CAPTION,p.POSTED_BY_EMP,p.POSTED_AT
                      FROM EV_POST p
                      JOIN RP_M_VERTICAL v ON v.ID=p.VERTICAL_ID
                      LEFT JOIN EV_EVENT e ON e.ID=p.EVENT_ID
                      LEFT JOIN EV_TAG t ON t.ID=p.TAG_ID
                      WHERE p.IS_ARCHIVED=0";
        if (verticalId.HasValue) sql += " AND p.VERTICAL_ID=:p_vid";
        sql += " ORDER BY p.POSTED_AT DESC) WHERE ROWNUM <= :p_take";
        using var cmd = _uow.OracleConn.Cmd(sql, _uow.OracleTx);
        if (verticalId.HasValue) cmd.Parameters.AddIn("p_vid", verticalId.Value);
        cmd.Parameters.AddIn("p_take", take);
        var posts = await cmd.QueryAsync(r => new FeedPost
        {
            Id = Long(r, "ID"), VerticalId = Long(r, "VERTICAL_ID"), VerticalCode = Str(r, "VCODE"),
            VerticalName = Str(r, "VNAME"), EventId = LongN(r, "EVENT_ID"), EventName = StrN(r, "ENAME"),
            TagId = LongN(r, "TAG_ID"), TagName = StrN(r, "TNAME"), Caption = ReadClob(r, "CAPTION"),
            PostedByEmp = Str(r, "POSTED_BY_EMP"), PostedAt = Dt(r, "POSTED_AT")
        }, ct);

        foreach (var p in posts) p.Images = (await LoadImagesAsync(p.Id, ct)).ToList();
        return posts;
    }

    private async Task<List<FeedPostImage>> LoadImagesAsync(long postId, CancellationToken ct)
    {
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,POST_ID,FILE_PATH,FILE_NAME,CONTENT_TYPE,FILE_SIZE,SEQ_NO,IS_ARCHIVED FROM EV_POST_IMAGE WHERE POST_ID=:p_pid ORDER BY SEQ_NO", _uow.OracleTx);
        cmd.Parameters.AddIn("p_pid", postId);
        return await cmd.QueryAsync(MapImage, ct);
    }

    public async Task<FeedPostImage?> GetImageAsync(long imageId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,POST_ID,FILE_PATH,FILE_NAME,CONTENT_TYPE,FILE_SIZE,SEQ_NO,IS_ARCHIVED FROM EV_POST_IMAGE WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", imageId);
        return await cmd.QueryOneAsync(MapImage, ct);
    }

    private static FeedPostImage MapImage(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), PostId = Long(r, "POST_ID"), FilePath = Str(r, "FILE_PATH"),
        FileName = Str(r, "FILE_NAME"), ContentType = Str(r, "CONTENT_TYPE"),
        FileSize = LongN(r, "FILE_SIZE") ?? 0, SeqNo = Int(r, "SEQ_NO"), IsArchived = Bool(r, "IS_ARCHIVED")
    };

    // ── Activity log ──────────────────────────────────────────────────────────
    public async Task LogAsync(string activity, string entityType, long? entityId, string actorEmp, long? verticalId, string? details, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO EV_ACTIVITY_LOG(ACTIVITY,ENTITY_TYPE,ENTITY_ID,ACTOR_EMP,VERTICAL_ID,DETAILS)
              VALUES(:p_act,:p_et,:p_eid,:p_emp,:p_vid,:p_det)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_act", activity);
        cmd.Parameters.AddIn("p_et", entityType);
        cmd.Parameters.AddIn("p_eid", OracleDbType.Int64, (object?)entityId ?? DBNull.Value);
        cmd.Parameters.AddIn("p_emp", actorEmp);
        cmd.Parameters.AddIn("p_vid", OracleDbType.Int64, (object?)verticalId ?? DBNull.Value);
        cmd.Parameters.AddIn("p_det", (object?)(details is { Length: > 2000 } ? details[..2000] : details) ?? DBNull.Value);
        await cmd.ExecAsync(ct);
    }

    // ── Archival ──────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<FeedPost>> ListPostsToArchiveAsync(DateTime olderThan, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,VERTICAL_ID,POSTED_BY_EMP,POSTED_AT FROM EV_POST WHERE IS_ARCHIVED=0 AND POSTED_AT < :p_cut", _uow.OracleTx);
        cmd.Parameters.AddIn("p_cut", OracleDbType.TimeStamp, olderThan);
        var posts = await cmd.QueryAsync(r => new FeedPost
        {
            Id = Long(r, "ID"), VerticalId = Long(r, "VERTICAL_ID"),
            PostedByEmp = Str(r, "POSTED_BY_EMP"), PostedAt = Dt(r, "POSTED_AT")
        }, ct);
        foreach (var p in posts) p.Images = (await LoadImagesAsync(p.Id, ct)).ToList();
        return posts;
    }

    public async Task MarkPostArchivedAsync(long postId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using (var c1 = _uow.OracleConn.Cmd("UPDATE EV_POST SET IS_ARCHIVED=1, ARCHIVED_AT=SYSTIMESTAMP WHERE ID=:p_id", _uow.OracleTx))
        {
            c1.Parameters.AddIn("p_id", postId);
            await c1.ExecAsync(ct);
        }
        using var c2 = _uow.OracleConn.Cmd("UPDATE EV_POST_IMAGE SET IS_ARCHIVED=1 WHERE POST_ID=:p_id", _uow.OracleTx);
        c2.Parameters.AddIn("p_id", postId);
        await c2.ExecAsync(ct);
    }

    public async Task UpdateImagePathArchivedAsync(long imageId, string newPath, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE EV_POST_IMAGE SET FILE_PATH=:p_path, IS_ARCHIVED=1 WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_path", newPath);
        cmd.Parameters.AddIn("p_id", imageId);
        await cmd.ExecAsync(ct);
    }

    public async Task<int> PurgeLogsOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("DELETE FROM EV_ACTIVITY_LOG WHERE CREATED_AT < :p_cut", _uow.OracleTx);
        cmd.Parameters.AddIn("p_cut", OracleDbType.TimeStamp, cutoff);
        return await cmd.ExecAsync(ct);
    }

    private static string ReadClob(OracleDataReader r, string col)
        => r[col] is DBNull ? "" : r[col].ToString() ?? "";
}
