using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class AuditRepo : IAuditRepo
{
    private readonly AdoUnitOfWork _uow;
    public AuditRepo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<string?> GetLastHashAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT HASH_CURR FROM (SELECT HASH_CURR FROM RP_AUDIT_LOG ORDER BY ID DESC) WHERE ROWNUM=1",
            _uow.OracleTx);
        return await cmd.ScalarAsync<string?>(ct);
    }

    public async Task InsertAsync(AuditLog log, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_AUDIT_LOG(ID,ENTITY,ENTITY_ID,ACTION,ACTOR_EMP_CODE,OLD_JSON,NEW_JSON,IP,USER_AGENT,HASH_PREV,HASH_CURR)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_ent,:p_eid,:p_action,:p_actor,:p_old,:p_new,:p_ip,:p_ua,:p_hprev,:p_hcurr)",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_ent", log.Entity); cmd.Parameters.AddIn("p_eid", log.EntityId);
        cmd.Parameters.AddIn("p_action", log.Action); cmd.Parameters.AddIn("p_actor", log.ActorEmpCode);
        cmd.Parameters.AddIn("p_old", OracleDbType.Clob, log.OldJson);
        cmd.Parameters.AddIn("p_new", OracleDbType.Clob, log.NewJson);
        cmd.Parameters.AddIn("p_ip", OracleDbType.Varchar2, log.Ip);
        cmd.Parameters.AddIn("p_ua", OracleDbType.Varchar2, log.UserAgent);
        cmd.Parameters.AddIn("p_hprev", OracleDbType.Varchar2, log.HashPrev);
        cmd.Parameters.AddIn("p_hcurr", log.HashCurr);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> ListByEntityAsync(string entity, long entityId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        //using var cmd = _uow.OracleConn.Cmd(@"
        //    SELECT ID,ENTITY,ENTITY_ID,ACTION,ACTOR_EMP_CODE,ACTED_AT,
        //           DBMS_LOB.SUBSTR(OLD_JSON,32000,1) AS OLD_JSON,
        //           DBMS_LOB.SUBSTR(NEW_JSON,32000,1) AS NEW_JSON,
        //           IP,USER_AGENT,HASH_PREV,HASH_CURR
        //    FROM RP_AUDIT_LOG WHERE ENTITY=:p_e AND ENTITY_ID=:p_id ORDER BY ID", _uow.OracleTx);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,ENTITY,ENTITY_ID,ACTION,ACTOR_EMP_CODE,ACTED_AT,
                   OLD_JSON,
                   NEW_JSON,
                   IP,USER_AGENT,HASH_PREV,HASH_CURR
            FROM RP_AUDIT_LOG WHERE ENTITY=:p_e AND ENTITY_ID=:p_id ORDER BY ID", _uow.OracleTx);
        cmd.Parameters.AddIn("p_e", entity); cmd.Parameters.AddIn("p_id", entityId);
        return await cmd.QueryAsync(MapLog, ct);
    }

    public async Task<IReadOnlyList<AuditLog>> ListSetupAuditAsync(
        int take, string? entityFilter = null, string? actorEmpCode = null,
        string? actionFilter = null, DateTime? fromUtc = null, DateTime? toUtc = null,
        CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        //using var cmd = _uow.OracleConn.Cmd(@"
        //    SELECT * FROM (
        //        SELECT ID,ENTITY,ENTITY_ID,ACTION,ACTOR_EMP_CODE,ACTED_AT,
        //               DBMS_LOB.SUBSTR(OLD_JSON,32000,1) AS OLD_JSON,
        //               DBMS_LOB.SUBSTR(NEW_JSON,32000,1) AS NEW_JSON,
        //               IP,USER_AGENT,HASH_PREV,HASH_CURR
        //        FROM RP_AUDIT_LOG
        //        WHERE (:p_ent IS NULL OR UPPER(ENTITY) LIKE '%'||UPPER(:p_ent)||'%')
        //          AND (:p_actor IS NULL OR UPPER(ACTOR_EMP_CODE)=UPPER(:p_actor))
        //          AND (:p_action IS NULL OR UPPER(ACTION) LIKE '%'||UPPER(:p_action)||'%')
        //          AND (:p_from IS NULL OR ACTED_AT>=:p_from)
        //          AND (:p_to IS NULL OR ACTED_AT<=:p_to)
        //        ORDER BY ACTED_AT DESC
        //    ) WHERE ROWNUM<=:p_take", _uow.OracleTx);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT * FROM (
                SELECT ID,ENTITY,ENTITY_ID,ACTION,ACTOR_EMP_CODE,ACTED_AT,
                       OLD_JSON,
                       NEW_JSON,
                       IP,USER_AGENT,HASH_PREV,HASH_CURR
                FROM RP_AUDIT_LOG
                WHERE (:p_ent IS NULL OR UPPER(ENTITY) LIKE '%'||UPPER(:p_ent)||'%')
                  AND (:p_actor IS NULL OR UPPER(ACTOR_EMP_CODE)=UPPER(:p_actor))
                  AND (:p_action IS NULL OR UPPER(ACTION) LIKE '%'||UPPER(:p_action)||'%')
                  AND (:p_from IS NULL OR ACTED_AT>=:p_from)
                  AND (:p_to IS NULL OR ACTED_AT<=:p_to)
                ORDER BY ACTED_AT DESC
            ) WHERE ROWNUM<=:p_take", _uow.OracleTx);
        cmd.Parameters.AddIn("p_ent", OracleDbType.Varchar2, entityFilter);
        cmd.Parameters.AddIn("p_actor", OracleDbType.Varchar2, actorEmpCode);
        cmd.Parameters.AddIn("p_action", OracleDbType.Varchar2, actionFilter);
        cmd.Parameters.AddIn("p_from", OracleDbType.TimeStamp, fromUtc);
        cmd.Parameters.AddIn("p_to", OracleDbType.TimeStamp, toUtc);
        cmd.Parameters.AddIn("p_take", take);
        return await cmd.QueryAsync(MapLog, ct);
    }

    public async Task<bool> VerifyChainAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        //using var cmd = _uow.OracleConn.Cmd(@"
        //    SELECT ID,ENTITY,ENTITY_ID,ACTION,ACTOR_EMP_CODE,ACTED_AT,
        //           DBMS_LOB.SUBSTR(OLD_JSON,32000,1) AS OLD_JSON,
        //           DBMS_LOB.SUBSTR(NEW_JSON,32000,1) AS NEW_JSON,
        //           IP,USER_AGENT,HASH_PREV,HASH_CURR
        //    FROM RP_AUDIT_LOG ORDER BY ID", _uow.OracleTx);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,ENTITY,ENTITY_ID,ACTION,ACTOR_EMP_CODE,ACTED_AT,
                   OLD_JSON,
                   NEW_JSON,
                   IP,USER_AGENT,HASH_PREV,HASH_CURR
            FROM RP_AUDIT_LOG ORDER BY ID", _uow.OracleTx);
        var rows = await cmd.QueryAsync(MapLog, ct);
        string? prev = null;
        foreach (var r in rows)
        {
            var canon = AuditCanonicaliser.Build(r);
            var expected = AuditCanonicaliser.Hash(prev, canon);
            if (!string.Equals(expected, r.HashCurr, StringComparison.Ordinal)) return false;
            if (!string.Equals(prev, r.HashPrev, StringComparison.Ordinal)) return false;
            prev = r.HashCurr;
        }
        return true;
    }

    private static AuditLog MapLog(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), Entity = Str(r, "ENTITY"), EntityId = Long(r, "ENTITY_ID"),
        Action = Str(r, "ACTION"), ActorEmpCode = Str(r, "ACTOR_EMP_CODE"),
        ActedAt = Dt(r, "ACTED_AT"), OldJson = StrN(r, "OLD_JSON"), NewJson = StrN(r, "NEW_JSON"),
        Ip = StrN(r, "IP"), UserAgent = StrN(r, "USER_AGENT"),
        HashPrev = StrN(r, "HASH_PREV"), HashCurr = Str(r, "HASH_CURR")
    };
}

public static class AuditCanonicaliser
{
    public static string Build(AuditLog l) =>
        $"{l.Entity}|{l.EntityId}|{l.Action}|{l.ActorEmpCode}|{l.ActedAt:O}|{l.OldJson}|{l.NewJson}|{l.Ip}|{l.UserAgent}";

    public static string Hash(string? prev, string canonical)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes((prev ?? "") + "" + canonical);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }
}
