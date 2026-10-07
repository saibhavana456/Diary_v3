using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Services;

namespace RequestPortal.Data.Repositories;

/// <summary>
/// Stores request attachment bytes as a BLOB in RP_ATTACHMENT_BLOB (created by
/// RP_10). Satisfies <see cref="IAttachmentStore"/> so the existing
/// AttachmentService flow persists file content in the database rather than the
/// filesystem. The storage key it returns is saved on RP_REQUEST_ATTACHMENT.
/// </summary>
public sealed class DbAttachmentStore : IAttachmentStore
{
    private readonly IDbConnectionFactory _factory;
    public DbAttachmentStore(IDbConnectionFactory factory) => _factory = factory;

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var key = $"{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}{Path.GetExtension(originalFileName)}";

        using var conn = (OracleConnection)await _factory.OpenAsync(ct);
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO RP_ATTACHMENT_BLOB (STORAGE_KEY, CONTENT) VALUES (:p_key, :p_content)";
        cmd.Parameters.Add(new OracleParameter("p_key", OracleDbType.Varchar2) { Value = key });
        cmd.Parameters.Add(new OracleParameter("p_content", OracleDbType.Blob) { Value = bytes });
        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return key;
    }

    public async Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default)
    {
        using var conn = (OracleConnection)await _factory.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CONTENT FROM RP_ATTACHMENT_BLOB WHERE STORAGE_KEY = :p_key";
        cmd.Parameters.Add(new OracleParameter("p_key", OracleDbType.Varchar2) { Value = storageKey });

        using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.IsDBNull(0))
            throw new FileNotFoundException($"Attachment blob '{storageKey}' not found.");

        using var blob = reader.GetOracleBlob(0);
        // Copy into a detached MemoryStream so the caller can read after the
        // connection/reader are disposed.
        return new MemoryStream(blob.Value, writable: false);
    }
}
