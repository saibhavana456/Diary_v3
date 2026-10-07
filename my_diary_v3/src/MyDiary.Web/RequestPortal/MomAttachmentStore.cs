using Microsoft.Extensions.Logging;
using RequestPortal.Core.Abstractions;

namespace MyDiary.Web.RequestPortal;

/// <summary>
/// Stores MoM Developer uploads (primary minutes + supporting docs) on persistent
/// storage (NFS in Production). Root is configurable via "Mom:StorageRoot":
///   - Empty/missing: defaults to ContentRoot/App_Data/MoM (local dev only)
///   - Relative path (e.g., "wwwroot/MyDiary/MoM"): resolved relative to ContentRoot
///   - Absolute path (e.g., "/mnt/nfs/MoM"): used as-is
/// Mirrors FileSystemEventMediaStore's NFS-aware path resolution.
/// Files land under {StorageRoot}/{momType.StorageRootPath}/{momTypeId}/{guid}_{sanitizedFileName}.
/// </summary>
public sealed class FileSystemMomAttachmentStore : IMomAttachmentStore
{
    private readonly string _root;
    private readonly ILogger<FileSystemMomAttachmentStore> _logger;

    public FileSystemMomAttachmentStore(IWebHostEnvironment env, IConfiguration cfg, ILogger<FileSystemMomAttachmentStore> logger)
    {
        _logger = logger;
        var configured = cfg["Mom:StorageRoot"];

        if (string.IsNullOrWhiteSpace(configured))
        {
            _root = Path.Combine(env.ContentRootPath, "App_Data", "MoM");
        }
        else if (Path.IsPathRooted(configured))
        {
            _root = configured;
        }
        else
        {
            // Relative path — resolve against ContentRootPath (e.g., wwwroot/MyDiary/MoM)
            _root = Path.Combine(env.ContentRootPath, configured);
        }

        Directory.CreateDirectory(_root);
        _logger.LogInformation("MomAttachmentStore initialized. StorageRoot={StorageRoot}", _root);
    }

    public async Task<(string RelativePath, string FileName)> SaveAsync(long momTypeId, string storageRootPath, Stream content, string originalFileName, CancellationToken ct = default)
    {
        var safe = Sanitize(originalFileName);
        var subFolder = SanitizeFolder(storageRootPath);
        var rel = $"{subFolder}/{momTypeId}/{Guid.NewGuid():N}_{safe}";
        var full = FullPath(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        _logger.LogInformation("MoM attachment saved: MomTypeId={MomTypeId}, Path={RelativePath}", momTypeId, rel);
        return (rel, safe);
    }

    public Task<Stream> OpenAsync(string relativePath, CancellationToken ct = default)
    {
        var full = FullPath(relativePath);
        if (!IsWithinRoot(full))
        {
            _logger.LogWarning("MoM attachment path traversal blocked: {RelativePath}", relativePath);
            throw new UnauthorizedAccessException("Path traversal outside storage root is not allowed.");
        }
        if (!File.Exists(full))
        {
            _logger.LogWarning("MoM attachment not found: {FullPath} (relative: {RelativePath})", full, relativePath);
            throw new FileNotFoundException("Stored file not found.", relativePath);
        }
        return Task.FromResult<Stream>(File.OpenRead(full));
    }

    private string FullPath(string rel) => Path.GetFullPath(Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar)));

    private bool IsWithinRoot(string fullPath)
    {
        var rootFull = Path.GetFullPath(_root + Path.DirectorySeparatorChar);
        return fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
    }

    private static string Sanitize(string name)
    {
        var cleaned = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        cleaned = cleaned.Replace("..", "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned;
    }

    private static string SanitizeFolder(string path)
    {
        var cleaned = path.Replace('\\', '/').Trim('/');
        var segments = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => new string(s.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()))
            .Where(s => s != "." && s != "..")
            .ToArray();
        return segments.Length == 0 ? "mom" : string.Join('/', segments);
    }
}
