using RequestPortal.Core.Dtos;

namespace RequestPortal.Web.Hubs;

/// <summary>
/// Server-side pub/sub for in-process Blazor bell/notification components
/// (migrated from Project A). Subscribers must dispose the returned handle.
/// </summary>
public sealed class NotificationStream
{
    public event Action<NotificationView>? OnNotification;

    public void Push(NotificationView n) => OnNotification?.Invoke(n);

    public IDisposable Subscribe(Action<NotificationView> handler)
    {
        OnNotification += handler;
        return new Subscription(this, handler);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly NotificationStream _s;
        private Action<NotificationView>? _h;
        public Subscription(NotificationStream s, Action<NotificationView> h) { _s = s; _h = h; }
        public void Dispose()
        {
            if (_h is not null) { _s.OnNotification -= _h; _h = null; }
        }
    }
}
