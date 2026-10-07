using MyCore = MyDiary.Core.Abstractions;
using RpCore = RequestPortal.Core.Services;

namespace MyDiary.Web.RequestPortal;

/// <summary>
/// Bridges Project A's IEmailSender/ISmsSender (RequestPortal.Core.Services) to
/// Project C's unified MailKit-based senders, so A's NotificationService works
/// without duplicating the SMTP implementation.
/// </summary>
public sealed class RequestPortalEmailSenderAdapter : RpCore.IEmailSender
{
    private readonly MyCore.IEmailSender _inner;
    public RequestPortalEmailSenderAdapter(MyCore.IEmailSender inner) => _inner = inner;

    public Task SendAsync(string toAddr, string subject, string htmlBody, CancellationToken ct = default)
        => _inner.SendAsync(toAddr, subject, htmlBody, ct);
}

public sealed class RequestPortalSmsSenderAdapter : RpCore.ISmsSender
{
    private readonly MyCore.ISmsSender _inner;
    public RequestPortalSmsSenderAdapter(MyCore.ISmsSender inner) => _inner = inner;

    public Task SendAsync(string toNumber, string text, CancellationToken ct = default)
        => _inner.SendAsync(toNumber, text, ct);
}

/// <summary>
/// Bridges A's notification-created event to the in-process pub/sub
/// (RequestPortal.Web.Hubs.NotificationStream) that NotificationBell.razor and
/// Notifications.razor subscribe to, so newly created notifications show up live
/// without a page refresh — for connections on this server instance.
/// </summary>
public sealed class InProcessNotificationPublisher : global::RequestPortal.Core.Abstractions.INotificationPublisher
{
    private readonly global::RequestPortal.Web.Hubs.NotificationStream _stream;
    public InProcessNotificationPublisher(global::RequestPortal.Web.Hubs.NotificationStream stream) => _stream = stream;

    public Task PublishAsync(global::RequestPortal.Core.Dtos.NotificationView notification, CancellationToken ct = default)
    {
        _stream.Push(notification);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Minimal broadcast audience resolver — returns an empty audience. Broadcast
/// fan-out is out of scope for the migrated Request-portal page slice.
/// </summary>
public sealed class EmptyBroadcastDirectory : global::RequestPortal.Core.Abstractions.IBroadcastDirectory
{
    public Task<IReadOnlyList<global::RequestPortal.Core.Models.AppUser>> ResolveAudienceAsync(
        global::RequestPortal.Core.Abstractions.BroadcastScope scope, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<global::RequestPortal.Core.Models.AppUser>>(
            System.Array.Empty<global::RequestPortal.Core.Models.AppUser>());
}

/// <summary>
/// Self-contained filesystem attachment store for the migrated Request portal.
/// Files are written under the configured Attachments:RootPath (defaults to a temp
/// folder). Satisfies A's IAttachmentStore without dragging the Demo store chain.
/// </summary>
public sealed class FileSystemAttachmentStore : RpCore.IAttachmentStore
{
    private readonly string _root;

    public FileSystemAttachmentStore(IConfiguration config)
    {
        _root = config["Attachments:RootPath"]
                ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MyDiaryV3", "Attachments");
        System.IO.Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        var ext = System.IO.Path.GetExtension(originalFileName);
        var key = $"{Guid.NewGuid():N}{ext}";
        var path = System.IO.Path.Combine(_root, key);
        await using var fs = new System.IO.FileStream(path, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write);
        await content.CopyToAsync(fs, ct);
        return key;
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default)
    {
        // Guard against path traversal — only the bare file name is honoured
        var safeKey = System.IO.Path.GetFileName(storageKey);
        var path = System.IO.Path.Combine(_root, safeKey);
        Stream s = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
        return Task.FromResult(s);
    }
}
