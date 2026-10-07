using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

public sealed class AttachmentOptions
{
    public string RootPath { get; set; } = "";
    public string[] AllowedMime { get; set; } = new[]
    {
        "application/pdf",
        "image/png", "image/jpeg",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
}

public interface IAttachmentStore
{
    Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);
    Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default);
}

public interface IAttachmentRepo
{
    Task<long> InsertAsync(Attachment a, CancellationToken ct = default);
    Task<Attachment?> GetAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<Attachment>> ListByRequestAsync(long requestId, CancellationToken ct = default);
}

public sealed class AttachmentService : IAttachmentService
{
    private readonly IAttachmentStore _store;
    private readonly IAttachmentRepo _repo;
    private readonly AttachmentOptions _opts;
    private readonly IAuditService _audit;

    public AttachmentService(IAttachmentStore store, IAttachmentRepo repo, IOptions<AttachmentOptions> opts, IAuditService audit)
    {
        _store = store; _repo = repo; _opts = opts.Value; _audit = audit;
    }

    public async Task<long> StoreAsync(long requestId, long? actionId, string fileName, string mime, Stream content, string uploadedByEmp, CancellationToken ct = default)
    {
        if (!_opts.AllowedMime.Contains(mime, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Mime type {mime} not allowed");
        if (content.CanSeek && content.Length > _opts.MaxBytes)
            throw new InvalidOperationException($"File exceeds {_opts.MaxBytes} bytes");

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        if (ms.Length > _opts.MaxBytes) throw new InvalidOperationException("File too large");
        ms.Position = 0;

        var sha = Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
        ms.Position = 0;

        var storageKey = await _store.SaveAsync(ms, fileName, ct);

        var id = await _repo.InsertAsync(new Attachment
        {
            RequestId = requestId,
            ActionId = actionId,
            FileName = fileName,
            Mime = mime,
            SizeBytes = ms.Length,
            StorageKey = storageKey,
            UploadedByEmp = uploadedByEmp,
            UploadedAt = DateTime.UtcNow,
            Sha256 = sha
        }, ct);

        await _audit.LogAsync("RP_REQUEST_ATTACHMENT", id, "Create", uploadedByEmp, null,
            new { requestId, fileName, mime, size = ms.Length, sha }, ct: ct);
        return id;
    }

    public async Task<(Stream Stream, string Mime, string FileName)> ReadAsync(long attachmentId, string actorEmpCode, CancellationToken ct = default)
    {
        var a = await _repo.GetAsync(attachmentId, ct) ?? throw new FileNotFoundException();
        var s = await _store.OpenAsync(a.StorageKey, ct);
        return (s, a.Mime, a.FileName);
    }
}

public sealed class FileSystemAttachmentStore : IAttachmentStore
{
    private readonly AttachmentOptions _opts;
    public FileSystemAttachmentStore(IOptions<AttachmentOptions> opts) => _opts = opts.Value;

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_opts.RootPath);
        var key = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{Path.GetExtension(originalFileName)}";
        var full = Path.Combine(_opts.RootPath, key);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return key;
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default)
    {
        var full = Path.Combine(_opts.RootPath, storageKey);
        return Task.FromResult<Stream>(File.OpenRead(full));
    }
}
