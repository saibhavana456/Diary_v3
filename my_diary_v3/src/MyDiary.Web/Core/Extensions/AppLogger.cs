using Microsoft.Extensions.Logging;

namespace MyDiary.Web.Core.Extensions
{
    /// <summary>
    /// Static logger wrapper for use in Razor components where injecting ILogger per-component
    /// is impractical. Replaces Console.WriteLine with structured logging.
    /// Initialized once at startup from Program.cs.
    /// </summary>
    public static class AppLogger
    {
        private static ILogger? _logger;

        /// <summary>Call once at startup: AppLogger.Initialize(app.Services)</summary>
        public static void Initialize(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("App");
        }

        public static void LogError(string message) => _logger?.LogError(message);
        public static void LogError(Exception ex, string message) => _logger?.LogError(ex, message);
        public static void LogWarning(string message) => _logger?.LogWarning(message);
        public static void LogInfo(string message) => _logger?.LogInformation(message);
    }
}
