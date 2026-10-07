using Microsoft.AspNetCore.Components.Server.Circuits;
using MyDiary.Web.Core.Models;
using System.Text.Json;
using System.Collections.Concurrent;

namespace MyDiary.Web.Core.Services.Authentication
{
    public class SessionCircuitHandler : CircuitHandler
    {
        private readonly ILogger<SessionCircuitHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly ConcurrentDictionary<string, SessionData> _sessionStore = new();
        
        public SessionData? CurrentSession { get; private set; }

        public SessionCircuitHandler(
            ILogger<SessionCircuitHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Circuit connected: {CircuitId}", circuit.Id);
            
            // Try to capture session from HttpContext when circuit connects
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                try
                {
                    // First try HttpContext.Session
                    var sessionJson = httpContext.Session.GetString("UserSession");
                    if (!string.IsNullOrEmpty(sessionJson))
                    {
                        CurrentSession = JsonSerializer.Deserialize<SessionData>(sessionJson);
                        _logger.LogInformation("Session loaded from HttpContext.Session: User {UserId}", CurrentSession?.UserId);
                        return Task.CompletedTask;
                    }
                    
                    // If not in HttpContext.Session, try to get session ID from cookie
                    var sessionId = httpContext.Request.Cookies["UBI_Session_Temp"];
                    if (!string.IsNullOrEmpty(sessionId) && _sessionStore.TryGetValue(sessionId, out var storedSession))
                    {
                        if (!storedSession.IsExpired)
                        {
                            CurrentSession = storedSession;
                            _logger.LogInformation("Session loaded from temp store: User {UserId}", CurrentSession?.UserId);
                        }
                        else
                        {
                            _sessionStore.TryRemove(sessionId, out _);
                            _logger.LogInformation("Expired session removed from temp store");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading session into circuit");
                }
            }
            
            return Task.CompletedTask;
        }

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Circuit disconnected: {CircuitId}", circuit.Id);
            CurrentSession = null;
            return Task.CompletedTask;
        }

        public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Circuit opened: {CircuitId}", circuit.Id);
            return Task.CompletedTask;
        }

        public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Circuit closed: {CircuitId}", circuit.Id);
            CurrentSession = null;
            return Task.CompletedTask;
        }

        public void SetSession(SessionData session)
        {
            CurrentSession = session;
            
            // Store in static dictionary with session ID as key
            var sessionId = Guid.NewGuid().ToString("N");
            _sessionStore[sessionId] = session;
            
            // Try to set a temporary cookie for session continuity
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                try
                {
                    httpContext.Response.Cookies.Append("UBI_Session_Temp", sessionId, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = httpContext.Request.IsHttps,
                        SameSite = SameSiteMode.Strict,
                        MaxAge = TimeSpan.FromHours(24)
                    });
                    _logger.LogInformation("Temporary session cookie set: {SessionId}", sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not set temporary session cookie");
                }
            }
            
            _logger.LogInformation("Session set in circuit: User {UserId}", session.UserId);
        }

        public void ClearSession()
        {
            CurrentSession = null;
            
            // Clear temporary cookie
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                try
                {
                    var sessionId = httpContext.Request.Cookies["UBI_Session_Temp"];
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        _sessionStore.TryRemove(sessionId, out _);
                        httpContext.Response.Cookies.Delete("UBI_Session_Temp");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not clear temporary session cookie");
                }
            }
        }
        
        // Cleanup expired sessions periodically
        public static void CleanupExpiredSessions()
        {
            var expiredKeys = _sessionStore
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var key in expiredKeys)
            {
                _sessionStore.TryRemove(key, out _);
            }
        }
    }
}
