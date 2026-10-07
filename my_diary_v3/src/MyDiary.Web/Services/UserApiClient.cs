namespace MyDiary.Web.Services
{
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.Logging;
    using MyDiary.Web.Features.Reports.Models;

    public sealed class UserApiClient
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<UserApiClient> _logger;

        public UserApiClient(HttpClient http, ILogger<UserApiClient> logger)
        {
            _http = http;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<UserResponse?> GetUserAsync(string baseUrl, string userId, CancellationToken ct = default)
        {
            var uri = new Uri(QueryHelpers.AddQueryString(baseUrl, new Dictionary<string, string?>
            {
                ["userId"] = userId
            }));

            // Optional: Accept header
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("application/json");

            try
            {
                using var resp = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogError("GET {Uri} failed with StatusCode={StatusCode} ReasonPhrase={ReasonPhrase}. ResponseBody: {Body}",
                        uri, (int)resp.StatusCode, resp.ReasonPhrase, body);
                    throw new HttpRequestException($"GET {uri} failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
                }

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                return await JsonSerializer.DeserializeAsync<UserResponse>(stream, _jsonOptions, ct);
            }
            catch (HttpRequestException ex) when (ex.Message.StartsWith("GET"))
            {
                // Already logged above — rethrow without double-logging.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling user API at {Uri} for UserId={UserId}", uri, userId);
                throw;
            }
        }
    }

}
