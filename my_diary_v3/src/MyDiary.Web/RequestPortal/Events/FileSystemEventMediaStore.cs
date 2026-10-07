using Microsoft.Extensions.Logging;
using RequestPortal.Core.Abstractions;

namespace MyDiary.Web.RequestPortal.Events;

/// <summary>
/// Stores Union Hub event photos on persistent storage (NFS in Production).
/// Root is configurable via "Events:StorageRoot":
///   - Empty/missing: defaults to ContentRoot/App_Data/EventMedia (local dev only)
///   - Relative path (e.g., "wwwroot/MyDiary/EventMedia"): resolved relative to ContentRoot
///   - Absolute path (e.g., "/mnt/nfs/EventMedia"): used as-is
/// Active files live under /active, archived files are moved to /archive.
/// </summary>
public sealed class FileSystemEventMediaStore : IEventMediaStore
{
    private readonly string _root;
    private readonly ILogger<FileSystemEventMediaStore> _logger;

    public FileSystemEventMediaStore(IWebHostEnvironment env, IConfiguration cfg, ILogger<FileSystemEventMediaStore> logger)
    {
        _logger = logger;
        var configured = cfg["Events:StorageRoot"];

        if (string.IsNullOrWhiteSpace(configured))
        {
            _root = Path.Combine(env.ContentRootPath, "App_Data", "EventMedia");
        }
        else if (Path.IsPathRooted(configured))
        {
            _root = configured;
        }
        else
        {
            // Relative path — resolve against ContentRootPath
            _root = Path.Combine(env.ContentRootPath, configured);
        }

        Directory.CreateDirectory(Path.Combine(_root, "active"));
        Directory.CreateDirectory(Path.Combine(_root, "archive"));
        _logger.LogInformation("EventMediaStore initialized. StorageRoot={StorageRoot}", _root);
    }

    public async Task<string> SaveAsync(long postId, string fileName, byte[] data, CancellationToken ct = default)
    {
        var safe = Sanitize(fileName);
        var rel = $"active/{postId}/{Guid.NewGuid():N}_{safe}";
        var full = FullPath(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, data, ct);
        _logger.LogInformation("EventMedia saved: PostId={PostId}, Path={RelativePath}, Size={Size} bytes", postId, rel, data.Length);
        return rel;
    }

    public Task<string> MoveToArchiveAsync(string relativePath, CancellationToken ct = default)
    {
        var newRel = relativePath.StartsWith("active/", StringComparison.OrdinalIgnoreCase)
            ? "archive/" + relativePath["active/".Length..]
            : "archive/" + relativePath;
        var src = FullPath(relativePath);
        var dst = FullPath(newRel);
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        if (File.Exists(src))
        {
            File.Move(src, dst, overwrite: true);
            _logger.LogInformation("EventMedia archived: {OldPath} → {NewPath}", relativePath, newRel);
        }
        else
        {
            _logger.LogWarning("EventMedia archive skipped — source not found: {Path}", src);
        }
        return Task.FromResult(newRel);
    }

    public Stream? OpenRead(string relativePath)
    {
        var full = FullPath(relativePath);
        if (File.Exists(full))
            return File.OpenRead(full);

        _logger.LogWarning("EventMedia file not found: {FullPath} (relative: {RelativePath})", full, relativePath);
        return null;
    }

    private string FullPath(string rel) => Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar));

    private static string Sanitize(string name)
    {
        var cleaned = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        cleaned = cleaned.Replace("..", "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "photo" : cleaned;
    }
}
