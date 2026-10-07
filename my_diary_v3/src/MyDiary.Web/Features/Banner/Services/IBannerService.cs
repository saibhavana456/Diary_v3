using MyDiary.Web.Features.Banner.Models;

namespace MyDiary.Web.Features.Banner.Services;

public interface IBannerService
{
    /// <summary>
    /// Gets the currently active banner to display.
    /// Returns null if no active banner is configured or if scheduling has expired.
    /// </summary>
    Task<BannerConfig?> GetActiveBannerAsync();
}
