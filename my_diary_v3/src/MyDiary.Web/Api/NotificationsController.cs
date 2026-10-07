using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Dtos;

namespace MyDiary.Web.Api;

/// <summary>
/// Server-to-server bell-notification API. External systems push notifications
/// to portal users and read/mark them on a user's behalf. All endpoints require
/// the API-key scheme (X-Api-Key header). Users are addressed by RP_USER.ID.
///   POST /api/notifications                              — push a notification
///   GET  /api/notifications/user/{userId}                — list a user's notifications
///   GET  /api/notifications/user/{userId}/unread-count   — unread count
///   POST /api/notifications/user/{userId}/{id}/read      — mark one read
///   POST /api/notifications/user/{userId}/read-all       — mark all read
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize(Policy = "ApiKey")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationApi _api;
    private readonly ILogger<NotificationsController> _logger;
    public NotificationsController(INotificationApi api, ILogger<NotificationsController> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <summary>Returns the api_client claim identifying the calling system.</summary>
    private string ApiClient => User.FindFirst("api_client")?.Value ?? "unknown";

    [HttpPost]
    public async Task<ActionResult<NotificationView>> Create([FromBody] CreateNotificationRequest req, CancellationToken ct)
    {
        if (req is null || req.UserId <= 0 || string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { error = "userId and title are required." });
        _logger.LogInformation("[NotificationsApi] Create notification for userId={UserId} by api_client={Client}", req.UserId, ApiClient);
        var view = await _api.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Mine), new { userId = view.UserId }, view);
    }

    [HttpGet("user/{userId:long}")]
    public async Task<ActionResult<IReadOnlyList<NotificationView>>> Mine(
        long userId, [FromQuery] int take = 20, [FromQuery] bool unreadOnly = false, CancellationToken ct = default)
    {
        _logger.LogInformation("[NotificationsApi] List userId={UserId} by api_client={Client}", userId, ApiClient);
        return Ok(await _api.ListForUserAsync(userId, take, unreadOnly, ct));
    }

    [HttpGet("user/{userId:long}/unread-count")]
    public async Task<ActionResult<UnreadCountResult>> UnreadCount(long userId, CancellationToken ct)
    {
        _logger.LogInformation("[NotificationsApi] UnreadCount userId={UserId} by api_client={Client}", userId, ApiClient);
        return Ok(new UnreadCountResult { Count = await _api.GetUnreadCountAsync(userId, ct) });
    }

    [HttpPost("user/{userId:long}/{id:long}/read")]
    public async Task<IActionResult> MarkRead(long userId, long id, CancellationToken ct)
    {
        _logger.LogInformation("[NotificationsApi] MarkRead userId={UserId} notifId={NotifId} by api_client={Client}", userId, id, ApiClient);
        await _api.MarkReadAsync(userId, id, ct);
        return NoContent();
    }

    [HttpPost("user/{userId:long}/read-all")]
    public async Task<IActionResult> MarkAllRead(long userId, CancellationToken ct)
    {
        _logger.LogInformation("[NotificationsApi] MarkAllRead userId={UserId} by api_client={Client}", userId, ApiClient);
        await _api.MarkAllReadAsync(userId, ct);
        return NoContent();
    }
}
