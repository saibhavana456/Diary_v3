using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class NotifRepo : INotifRepo
{
    private readonly AdoUnitOfWork _uow;
    public NotifRepo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<NotifTemplate?> GetTemplateAsync(string eventCode, NotifChannel channel, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        //using var cmd = _uow.OracleConn.Cmd(@"
        //    SELECT ID,EVENT_CODE,CHANNEL,SUBJECT,DBMS_LOB.SUBSTR(BODY,32000,1) AS BODY,IS_ACTIVE
        //    FROM RP_M_NOTIF_TEMPLATE WHERE EVENT_CODE=:p_e AND CHANNEL=:p_c AND IS_ACTIVE=1",
        //    _uow.OracleTx);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,EVENT_CODE,CHANNEL,SUBJECT,BODY,IS_ACTIVE
            FROM RP_M_NOTIF_TEMPLATE WHERE EVENT_CODE=:p_e AND CHANNEL=:p_c AND IS_ACTIVE=1",
           _uow.OracleTx);
        cmd.Parameters.AddIn("p_e", eventCode);
        cmd.Parameters.AddIn("p_c", (int)channel);
        return await cmd.QueryOneAsync(MapTemplate, ct);
    }

    public async Task<long> EnqueueAsync(NotifOutboxItem item, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_NOTIF_OUTBOX(ID,EVENT_CODE,CHANNEL,TO_ADDR,PAYLOAD_JSON,STATUS)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_e,:p_c,:p_to,:p_json,0)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_e", item.EventCode);
        cmd.Parameters.AddIn("p_c", (int)item.Channel);
        cmd.Parameters.AddIn("p_to", item.ToAddr);
        cmd.Parameters.AddIn("p_json", item.PayloadJson);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task<IReadOnlyList<NotifOutboxItem>> ListPendingAsync(int batchSize, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        //using var cmd = _uow.OracleConn.Cmd(@"
        //    SELECT ID,EVENT_CODE,CHANNEL,TO_ADDR,DBMS_LOB.SUBSTR(PAYLOAD_JSON,32000,1) AS PAYLOAD_JSON,
        //           STATUS,ATTEMPTS,LAST_ERROR,CREATED_AT,SENT_AT
        //    FROM RP_NOTIF_OUTBOX WHERE STATUS=0 AND ROWNUM<=:p_take ORDER BY CREATED_AT",
        //    _uow.OracleTx);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,EVENT_CODE,CHANNEL,TO_ADDR,PAYLOAD_JSON,
                   STATUS,ATTEMPTS,LAST_ERROR,CREATED_AT,SENT_AT
            FROM RP_NOTIF_OUTBOX WHERE STATUS=0 AND ROWNUM<=:p_take ORDER BY CREATED_AT",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_take", batchSize);
        return await cmd.QueryAsync(MapOutbox, ct);
    }

    public async Task MarkSentAsync(long id, DateTime sentAt, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_NOTIF_OUTBOX SET STATUS=1,SENT_AT=:p_at WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_at", sentAt); cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            UPDATE RP_NOTIF_OUTBOX
            SET ATTEMPTS=ATTEMPTS+1, LAST_ERROR=:p_err,
                STATUS=CASE WHEN ATTEMPTS+1>=5 THEN 2 ELSE 0 END
            WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_err", error.Length > 1900 ? error[..1900] : error);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<NotifTemplate>> ListTemplatesAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        //using var cmd = _uow.OracleConn.Cmd(@"
        //    SELECT ID,EVENT_CODE,CHANNEL,SUBJECT,DBMS_LOB.SUBSTR(BODY,32000,1) AS BODY,IS_ACTIVE
        //    FROM RP_M_NOTIF_TEMPLATE ORDER BY EVENT_CODE,CHANNEL", _uow.OracleTx);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,EVENT_CODE,CHANNEL,SUBJECT,BODY,IS_ACTIVE
            FROM RP_M_NOTIF_TEMPLATE ORDER BY EVENT_CODE,CHANNEL", _uow.OracleTx);
        return await cmd.QueryAsync(MapTemplate, ct);
    }

    public async Task<long> InsertTemplateAsync(NotifTemplate m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_M_NOTIF_TEMPLATE(ID,EVENT_CODE,CHANNEL,SUBJECT,BODY,IS_ACTIVE)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_e,:p_c,:p_subj,:p_body,:p_active)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_e", m.EventCode); cmd.Parameters.AddIn("p_c", (int)m.Channel);
        cmd.Parameters.AddIn("p_subj", m.Subject); cmd.Parameters.AddIn("p_body", m.Body);
        cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task UpdateTemplateAsync(NotifTemplate m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            UPDATE RP_M_NOTIF_TEMPLATE
            SET EVENT_CODE=:p_e,CHANNEL=:p_c,SUBJECT=:p_subj,BODY=:p_body,IS_ACTIVE=:p_active
            WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_e", m.EventCode); cmd.Parameters.AddIn("p_c", (int)m.Channel);
        cmd.Parameters.AddIn("p_subj", m.Subject); cmd.Parameters.AddIn("p_body", m.Body);
        cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0); cmd.Parameters.AddIn("p_id", m.Id);
        await cmd.ExecAsync(ct);
    }

    public async Task SetTemplateActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_NOTIF_TEMPLATE SET IS_ACTIVE=:p_a WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", isActive ? 1 : 0); cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static NotifTemplate MapTemplate(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), EventCode = Str(r, "EVENT_CODE"),
        Channel = (NotifChannel)Int(r, "CHANNEL"), Subject = StrN(r, "SUBJECT"),
        Body = Str(r, "BODY"), IsActive = Bool(r, "IS_ACTIVE")
    };

    private static NotifOutboxItem MapOutbox(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), EventCode = Str(r, "EVENT_CODE"),
        Channel = (NotifChannel)Int(r, "CHANNEL"), ToAddr = Str(r, "TO_ADDR"),
        PayloadJson = Str(r, "PAYLOAD_JSON"), Status = (NotifOutboxStatus)Int(r, "STATUS"),
        Attempts = Int(r, "ATTEMPTS"), LastError = StrN(r, "LAST_ERROR"),
        CreatedAt = Dt(r, "CREATED_AT"), SentAt = DtN(r, "SENT_AT")
    };
}
