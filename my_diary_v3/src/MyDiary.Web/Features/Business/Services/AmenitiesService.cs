using System.Text.Json;
using MyDiary.Web.Features.Business.Models;

namespace MyDiary.Web.Features.Business.Services;

public class AmenitiesService : IAmenitiesService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AmenitiesService> _logger;

    public AmenitiesService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<AmenitiesService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<List<AmenityItem>> GetAmenitiesAsync(string branchCode, string agendaHeader)
    {
        var items = new List<AmenityItem>();

        try
        {
            var apiUrl = _config["ApiEndpoints:CSECAmenitiesAPI"];
            if (string.IsNullOrEmpty(apiUrl))
            {
                _logger.LogWarning("CSECAmenitiesAPI endpoint not configured, returning empty data.");
                return FilterValid(items);
            }

            var client = _httpClientFactory.CreateClient();

            var queryParams = new Dictionary<string, string?>
            {
                ["branchid"] = branchCode,
                ["agenda_header"] = agendaHeader
            };

            var queryString = string.Join("&", queryParams
                .Where(q => !string.IsNullOrEmpty(q.Value))
                .Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value!)}"));

            var response = await client.GetAsync($"{apiUrl}?{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                items = JsonSerializer.Deserialize<List<AmenityItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new();
            }
            else
            {
                _logger.LogWarning("Amenities API returned {StatusCode} for branch {BranchCode}", response.StatusCode, branchCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching amenities for branch {BranchCode}, returning empty data.", branchCode);
        }

        return FilterValid(items);
    }

    /// <summary>Keep only items with both a description and a preview image.</summary>
    private static List<AmenityItem> FilterValid(List<AmenityItem> items) =>
        items.Where(a => !string.IsNullOrWhiteSpace(a.AgendaDescription)
                      && !string.IsNullOrWhiteSpace(a.PreviewBase64))
             .ToList();
}
