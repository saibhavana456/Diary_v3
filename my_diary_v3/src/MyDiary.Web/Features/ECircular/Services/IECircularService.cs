using MyDiary.Web.Features.ECircular.Models;

namespace MyDiary.Web.Features.ECircular.Services;

/// <summary>
/// Service interface for ECircular search, filter lookup, and PDF generation.
/// </summary>
public interface IECircularService
{
    /// <summary>
    /// Gets the filter lookup data (departments, circular types, years).
    /// </summary>
    Task<ECircularFilterLookup> GetFilterDataAsync(CancellationToken ct = default);

    /// <summary>
    /// Searches circulars using the provided filters.
    /// </summary>
    Task<ECircularSearchResponse> SearchCircularsAsync(
        ECircularSearchRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads a circular PDF, applies watermark in-memory, and returns
    /// the watermarked PDF as a byte array for direct download (no temp files).
    /// </summary>
    Task<byte[]> GetWatermarkedPdfBytesAsync(
        ECircularPdfRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Loads a special listing (e.g., Caution Advice, Latest Banking News) by key.
    /// </summary>
    Task<ECircularSearchResponse> GetSpecialListingAsync(string key, int pageNo, CancellationToken ct = default);

    /// <summary>
    /// Resolves the full URL for a special listing that opens in a new tab.
    /// </summary>
    Task<string> GetSpecialListingUrl(string key, CancellationToken ct = default);
}
