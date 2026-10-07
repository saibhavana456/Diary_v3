using System.Text;
using System.Text.Json;

namespace MyDiary.Web.Features.StaffVendorConnect.Services;

/// <summary>
/// Karix ConnectOne RCM API client, per "ConnectOne RCM API" doc (v2.0, 14/08/2025):
///   1. POST {TokenUrl} with header auth_key: {AccessKeyAuthHeader}, body {"Username": "{EncryptedUsername}"}
///      -> a token, cached in-memory for WhatsApp:TokenCacheMinutes (the doc gives no expiry, so
///      this is a conservative default rather than a documented value).
///   2. POST {SendUrl} with header token: {token}, body per the TEMPLATE / MEDIA_TEMPLATE shape
///      the doc specifies exactly (channel WABA, recipient array, sender.from, preferences.webHookDNId,
///      metaData.version/originator).
/// Errors surface as WhatsAppSendResult.Success=false with the API's own statusDesc/Error-NNN
/// description where possible — SvcCommunicationService then marks that recipient FAILED (not
/// silently QUEUED), since this channel is now actually wired up.
/// </summary>
public sealed class WhatsAppApiService : IWhatsAppApiService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppApiService> _logger;

    private static string? _cachedToken;
    private static DateTime _cachedTokenExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

    public WhatsAppApiService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<WhatsAppApiService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    private static readonly Dictionary<string, string> ErrorCodes = new()
    {
        ["Error-101"] = "Destination Not Found", ["Error-102"] = "User Details Not Found",
        ["Error-103"] = "Sender Id Not Found", ["Error-104"] = "Invalid Message Type",
        ["Error-105"] = "Message Not Found", ["Error-106"] = "Message Too Long",
        ["Error-107"] = "Invalid User Credentials", ["Error-108"] = "Invalid Message Priority",
        ["Error-109"] = "User Does Not Have International Termination Permission",
        ["Error-110"] = "Invalid Message Validity", ["Error-111"] = "Invalid Concat Parameter",
        ["Error-112"] = "Unable To Validate Packet", ["Error-113"] = "Unable To Save Packet",
        ["Error-114"] = "IP Address Not Registered", ["Error-115"] = "Invalid Destination",
        ["Error-116"] = "Invalid Message", ["Error-117"] = "Unable To Process",
        ["Error-118"] = "Message ID Not Found", ["Error-119"] = "The Content ID Not Found",
        ["Error-120"] = "The Content Values Not Found", ["Error-121"] = "No Contents Are Not Registered",
        ["Error-122"] = "The Default Language ID Not Found", ["Error-123"] = "Invalid Content ID",
        ["Error-124"] = "The Invalid Authkey", ["Error-125"] = "The Authkey Expired",
        ["Error-126"] = "The Decryption Failed", ["Error-127"] = "Empty Channel Received",
        ["Error-128"] = "Empty Media Template Received", ["Error-129"] = "Empty Body Parameter Values Received",
        ["Error-130"] = "Empty Media Object Received", ["Error-131"] = "Meta Data Empty",
        ["Error-132"] = "Meta Data Version Empty", ["Error-133"] = "Empty Text", ["Error-134"] = "Empty WebHook DNID"
    };

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _cachedTokenExpiry)
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow < _cachedTokenExpiry)
                return _cachedToken;

            var tokenUrl = _config["WhatsApp:TokenUrl"];
            var authKey = _config["WhatsApp:AccessKeyAuthHeader"];
            var username = _config["WhatsApp:EncryptedUsername"];
            if (string.IsNullOrWhiteSpace(tokenUrl) || string.IsNullOrWhiteSpace(authKey) || string.IsNullOrWhiteSpace(username)
                || tokenUrl.StartsWith("<") || authKey == "CHANGE_ME" || username == "CHANGE_ME")
                throw new InvalidOperationException("WhatsApp gateway is not configured (WhatsApp:TokenUrl/AccessKeyAuthHeader/EncryptedUsername).");

            var http = _httpFactory.CreateClient("WhatsAppGateway");
            using var req = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
            req.Headers.Add("auth_key", authKey);
            req.Content = new StringContent(JsonSerializer.Serialize(new { Username = username }), Encoding.UTF8, "application/json");

            using var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"WhatsApp token request failed ({(int)resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            // The doc shows a "Success Response" section with no rendered field names (image-only
            // in the source PDF) — accept common shapes defensively (token / Token / access_token).
            string? token = TryGetString(doc.RootElement, "token") ?? TryGetString(doc.RootElement, "Token") ?? TryGetString(doc.RootElement, "access_token");
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException($"WhatsApp token response had no recognizable token field: {body}");

            _cachedToken = token;
            var minutes = _config.GetValue<int?>("WhatsApp:TokenCacheMinutes") ?? 10;
            _cachedTokenExpiry = DateTime.UtcNow.AddMinutes(minutes);
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string? TryGetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public async Task<WhatsAppSendResult> SendTemplateMessageAsync(string toMobile10Digit, string templateId, string language, IReadOnlyList<string> parameterValues)
    {
        var parameterValuesObj = new Dictionary<string, string>();
        for (int i = 0; i < parameterValues.Count; i++) parameterValuesObj[i.ToString()] = parameterValues[i];

        var payload = new
        {
            message = new
            {
                channel = "WABA",
                content = new
                {
                    preview_url = false,
                    shorten_url = true,
                    type = "TEMPLATE",
                    template = new { templateId, parameterValues = parameterValuesObj, language = string.IsNullOrWhiteSpace(language) ? "en" : language }
                },
                recipient = new[] { new { to = "91" + toMobile10Digit, recipient_type = "individual" } },
                sender = new { from = _config["WhatsApp:SenderFrom"] },
                preferences = new { webHookDNId = _config["WhatsApp:WebHookDNId"] }
            },
            metaData = new { version = _config["WhatsApp:MetaVersion"], originator = _config["WhatsApp:Originator"] }
        };
        return await PostSendAsync(payload);
    }

    public async Task<WhatsAppSendResult> SendMediaTemplateMessageAsync(string toMobile10Digit, string templateId, string mediaType, string mediaUrl)
    {
        var payload = new
        {
            message = new
            {
                channel = "WABA",
                content = new
                {
                    preview_url = true,
                    shorten_url = false,
                    type = "MEDIA_TEMPLATE",
                    mediaTemplate = new
                    {
                        templateId,
                        paramPrefix = "Param",
                        media = new { type = mediaType, url = mediaUrl },
                        bodyParameterValues = new { }
                    }
                },
                recipient = new[] { new { to = "91" + toMobile10Digit, recipient_type = "individual" } },
                sender = new { from = _config["WhatsApp:SenderFrom"] },
                preferences = new { webHookDNId = _config["WhatsApp:WebHookDNId"] }
            },
            metaData = new { version = _config["WhatsApp:MetaVersion"], originator = _config["WhatsApp:Originator"] }
        };
        return await PostSendAsync(payload);
    }

    private async Task<WhatsAppSendResult> PostSendAsync(object payload)
    {
        try
        {
            var token = await GetTokenAsync(CancellationToken.None);
            var sendUrl = _config["WhatsApp:SendUrl"];
            if (string.IsNullOrWhiteSpace(sendUrl) || sendUrl.StartsWith("<"))
                return new WhatsAppSendResult { Success = false, ErrorMessage = "WhatsApp gateway is not configured (WhatsApp:SendUrl)." };

            var http = _httpFactory.CreateClient("WhatsAppGateway");
            using var req = new HttpRequestMessage(HttpMethod.Post, sendUrl);
            req.Headers.Add("token", token);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var resp = await http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var requestStatus = TryGetString(root, "requestStatus");

            if (!resp.IsSuccessStatusCode || !string.Equals(requestStatus, "Success", StringComparison.OrdinalIgnoreCase))
            {
                var statusDesc = TryGetString(root, "statusDesc") ?? requestStatus ?? $"HTTP {(int)resp.StatusCode}";
                var friendly = ErrorCodes.TryGetValue(statusDesc, out var desc) ? $"{statusDesc}: {desc}" : statusDesc;
                return new WhatsAppSendResult { Success = false, StatusDesc = statusDesc, ErrorMessage = friendly };
            }

            string? messageId = null, msgStatusDesc = null;
            if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0)
            {
                var first = messages[0];
                messageId = TryGetString(first, "messageId");
                msgStatusDesc = TryGetString(first, "statusDesc");
                var msgRequestStatus = TryGetString(first, "requestStatus");
                if (!string.Equals(msgRequestStatus, "success", StringComparison.OrdinalIgnoreCase))
                    return new WhatsAppSendResult { Success = false, StatusDesc = msgStatusDesc, ErrorMessage = msgStatusDesc ?? "Message rejected by gateway." };
            }

            return new WhatsAppSendResult { Success = true, MessageId = messageId, StatusDesc = msgStatusDesc ?? "Request accepted successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp send failed");
            return new WhatsAppSendResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
