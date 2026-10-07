using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Models;
using RequestPortal.Core.Services;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class AttachmentRepo : IAttachmentRepo
{
    private readonly AdoUnitOfWork _uow;
    public AttachmentRepo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<long> InsertAsync(Attachment a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_REQUEST_ATTACHMENT(ID,REQUEST_ID,ACTION_ID,FILE_NAME,MIME,SIZE_BYTES,STORAGE_KEY,UPLOADED_BY_EMP,SHA256)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_req,:p_act,:p_fname,:p_mime,:p_size,:p_key,:p_by,:p_sha)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_req", a.RequestId);
        cmd.Parameters.AddIn("p_act", OracleDbType.Int64, a.ActionId);
        cmd.Parameters.AddIn("p_fname", a.FileName);
        cmd.Parameters.AddIn("p_mime", a.Mime);
        cmd.Parameters.AddIn("p_size", a.SizeBytes);
        cmd.Parameters.AddIn("p_key", a.StorageKey);
        cmd.Parameters.AddIn("p_by", a.UploadedByEmp);
        cmd.Parameters.AddIn("p_sha", OracleDbType.Varchar2, a.Sha256);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task<Attachment?> GetAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,REQUEST_ID,ACTION_ID,FILE_NAME,MIME,SIZE_BYTES,STORAGE_KEY,UPLOADED_BY_EMP,UPLOADED_AT,SHA256
            FROM RP_REQUEST_ATTACHMENT WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        return await cmd.QueryOneAsync(Map, ct);
    }

    public async Task<IReadOnlyList<Attachment>> ListByRequestAsync(long requestId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,REQUEST_ID,ACTION_ID,FILE_NAME,MIME,SIZE_BYTES,STORAGE_KEY,UPLOADED_BY_EMP,UPLOADED_AT,SHA256
            FROM RP_REQUEST_ATTACHMENT WHERE REQUEST_ID=:p_req ORDER BY UPLOADED_AT DESC", _uow.OracleTx);
        cmd.Parameters.AddIn("p_req", requestId);
        return await cmd.QueryAsync(Map, ct);
    }

    private static Attachment Map(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), RequestId = Long(r, "REQUEST_ID"), ActionId = LongN(r, "ACTION_ID"),
        FileName = Str(r, "FILE_NAME"), Mime = Str(r, "MIME"), SizeBytes = Long(r, "SIZE_BYTES"),
        StorageKey = Str(r, "STORAGE_KEY"), UploadedByEmp = Str(r, "UPLOADED_BY_EMP"),
        UploadedAt = Dt(r, "UPLOADED_AT"), Sha256 = StrN(r, "SHA256")
    };
}
