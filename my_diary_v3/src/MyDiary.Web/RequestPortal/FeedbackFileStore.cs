using MyDiary.Web.Core.Extensions;
using RequestPortal.Core.Abstractions;

namespace MyDiary.Web.RequestPortal;

/// <summary>Stores Feedback ticket attachments under the NFS-aware static-asset
/// root (wwwroot/MyDiary/uploads/feedback under NFS, wwwroot/uploads/feedback
/// locally). Feedback's own file store, decoupled from the Request Portal's
/// attachment service (Feedback tickets live in their own RP_FEEDBACK_TICKET
/// table, not RP_REQUEST).
///
/// Feedback attachments are served as plain static-file URLs (see FeedbackDetail
/// /FeedbackTicketView: &lt;MudLink Href="/{FilePath}"&gt;), so both the physical
/// write location and the returned relative URL are resolved through
/// <see cref="IStaticAssetPathResolver"/> to stay consistent with how images,
/// downloads and UBINET are served.</summary>
public sealed class FeedbackFileStore : IFeedbackFileStore
{
    private const string StoreFolder = "uploads/feedback";

    private readonly IStaticAssetPathResolver _assets;
    private readonly ILogger<FeedbackFileStore> _logger;

    public FeedbackFileStore(IStaticAssetPathResolver assets, ILogger<FeedbackFileStore> logger)
    {
        _assets = assets;
        _logger = logger;
    }

    public async Task<(string RelativePath, string FileName)> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        var safe = Sanitize(originalFileName);
        var storedName = $"{Guid.NewGuid():N}_{safe}";

        var fullDir = _assets.Resolve(StoreFolder);
        Directory.CreateDirectory(fullDir);

        var fullPath = Path.Combine(fullDir, storedName);
        await using (var fs = File.Create(fullPath))
        {
            await content.CopyToAsync(fs, ct);
        }

        // Served as a static URL — prefix with the NFS subfolder when NFS is enabled
        // (e.g. MyDiary/uploads/feedback/xyz) so the static-file middleware finds it.
        var relWebPath = _assets.ResolveUrl($"{StoreFolder}/{storedName}");
        _logger.LogInformation("Feedback attachment saved: Path={RelativePath}", relWebPath);
        return (relWebPath, safe);
    }

    private static string Sanitize(string name)
    {
        var cleaned = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        cleaned = cleaned.Replace("..", "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned;
    }
}
