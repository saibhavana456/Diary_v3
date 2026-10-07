using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MyDiary.Web.Hubs;

/// <summary>
/// SignalR hub for real-time in-app notifications (migrated from Project A).
/// Clients subscribe to their employee-specific group.
/// Event emitted: "notification:new" with the notification payload.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var emplId = Context.User?.FindFirst("emplid")?.Value;
        if (!string.IsNullOrEmpty(emplId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{emplId}");
        else
            _logger.LogWarning("NotificationHub: client connected without emplid claim. ConnectionId={ConnectionId}", Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
            _logger.LogWarning(exception, "NotificationHub: client disconnected with error. ConnectionId={ConnectionId}", Context.ConnectionId);

        var emplId = Context.User?.FindFirst("emplid")?.Value;
        if (!string.IsNullOrEmpty(emplId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{emplId}");

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sends a notification to a specific employee across all their connections.
    /// </summary>
    public static Task PushToUserAsync(IHubContext<NotificationHub> hub, string emplId, object payload)
        => hub.Clients.Group($"user:{emplId}").SendAsync("notification:new", payload);
}
