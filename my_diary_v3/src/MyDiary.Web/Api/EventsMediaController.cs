using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RequestPortal.Core.Abstractions;

namespace MyDiary.Web.Api;

/// <summary>Streams Union Hub (Corporate Events) photos from server storage.</summary>
/// <remarks>
/// Anonymous by design: this app authenticates users through the Blazor
/// session-storage (AES) provider, which a plain &lt;img&gt; request cannot carry,
/// so a [Authorize] here always 302s and the image breaks. Photos are non-sensitive
/// internal event images addressed by opaque ids. To harden later, issue short-lived
/// signed URLs from the page and validate the token here instead of [AllowAnonymous].
/// </remarks>
[ApiController]
[Route("api/events")]
[AllowAnonymous]
public sealed class EventsMediaController : ControllerBase
{
    private readonly IEventFeedRepo _repo;
    private readonly IEventMediaStore _store;
    private readonly ILogger<EventsMediaController> _logger;

    public EventsMediaController(IEventFeedRepo repo, IEventMediaStore store, ILogger<EventsMediaController> logger)
    { _repo = repo; _store = store; _logger = logger; }

    [HttpGet("media/{id:long}")]
    public async Task<IActionResult> Media(long id, CancellationToken ct = default)
    {
        var img = await _repo.GetImageAsync(id, ct);
        if (img is null)
        {
            _logger.LogWarning("Event media image record not found for Id={ImageId}", id);
            return NotFound();
        }

        var stream = _store.OpenRead(img.FilePath);
        if (stream is null)
        {
            _logger.LogWarning("Event media file could not be opened at path: {FilePath} for Id={ImageId}", img.FilePath, id);
            return NotFound();
        }

        var mime = string.IsNullOrWhiteSpace(img.ContentType) ? "application/octet-stream" : img.ContentType;
        return File(stream, mime, enableRangeProcessing: true);
    }
}
