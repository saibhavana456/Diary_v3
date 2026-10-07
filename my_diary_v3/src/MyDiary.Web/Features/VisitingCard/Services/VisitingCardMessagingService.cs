using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyDiary.Web.Features.Shared.Services;
using MyDiary.Web.Services;

namespace MyDiary.Web.Features.VisitingCard.Services;

/// <inheritdoc cref="IVisitingCardMessagingService" />
public class VisitingCardMessagingService : IVisitingCardMessagingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<VisitingCardMessagingService> _logger;
    private readonly IVCardService _vCardService;
    private readonly IEncryptionService _encryptionService;

    public VisitingCardMessagingService(
        IHttpClientFactory httpFactory, IConfiguration config,
        ILogger<VisitingCardMessagingService> logger, IVCardService vCardService,
        IEncryptionService encryptionService)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
        _vCardService = vCardService;
        _encryptionService = encryptionService;
    }

    public async Task<MessagingResult> SendSmsAsync(string employeeId, CancellationToken ct = default)
    {
        try
        {
            var card = await _vCardService.GetEmployeeCardAsync(employeeId);
            if (card is null || string.IsNullOrWhiteSpace(card.MobileNo))
                return new MessagingResult(false, "No mobile number found for this employee.");

            var uname = _config["ApiSettings:SMSUsername"];
            var powerApiKey = _config["ApiSettings:SMSPowerApiKey"];
            if (string.IsNullOrWhiteSpace(uname) || string.IsNullOrWhiteSpace(powerApiKey))
                return new MessagingResult(false, "SMS gateway is not configured.");

            var sender = _config["SmsDefaults:SenderCode"] ?? "UNIONB";
            var messageText = BuildMessageText(card);

            // AES-256-CBC encryption is delegated to the centralized CryptoService
            // (via IEncryptionService) instead of doing CBC crypto inline here.
            string messageTextEnc, mobileEnc;
            try
            {
                // Key material and IV source are both the powerApiKey — matching the
                // gateway's expected scheme (previously done inline via AesCbcCrypto).
                messageTextEnc = _encryptionService.EncryptAesCbc(messageText, powerApiKey, powerApiKey);
                mobileEnc = _encryptionService.EncryptAesCbc(card.MobileNo, powerApiKey, powerApiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VisitingCard: failed to encrypt SMS payload for {EmployeeId} via CryptoService.", employeeId);
                return new MessagingResult(false, "Failed to secure the message. Please try again later.");
            }

            var http = _httpFactory.CreateClient("VisitingCardGateway");

            var token = await GetTokenAsync(http, _config["ApiSettings:SMSTokenUrl"], _config["ApiSettings:SMSAuthKey"], uname, ct);

            var outbound = new SmsOutboundRequest
            {
                uname = uname,
                messages = new List<SmsMessage>
                {
                    new() { dest = mobileEnc, msg = messageTextEnc, send = sender, encrypt = _config["SmsDefaults:Encrypt"] ?? "0" }
                }
            };

            var actualUrl = _config["ApiSettings:SMSActualApiUrl"];
            if (string.IsNullOrWhiteSpace(actualUrl))
                return new MessagingResult(false, "SMS gateway is not configured.");

            using var req = new HttpRequestMessage(HttpMethod.Post, actualUrl);
            req.Headers.Add("token", token);
            req.Headers.Add("Accept", "application/json");
            req.Content = JsonContent.Create(outbound);
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            _logger.LogInformation("VisitingCard SMS send for {EmployeeId} — status {Status}", employeeId, res.StatusCode);
            return res.IsSuccessStatusCode
                ? new MessagingResult(true, "SMS sent.")
                : new MessagingResult(false, $"Gateway returned {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VisitingCard: SMS send failed for {EmployeeId}.", employeeId);
            return new MessagingResult(false, "Failed to send SMS. Please try again later.");
        }
    }

    public async Task<MessagingResult> SendWhatsAppAsync(string employeeId, CancellationToken ct = default)
    {
        try
        {
            var card = await _vCardService.GetEmployeeCardAsync(employeeId);
            if (card is null || string.IsNullOrWhiteSpace(card.MobileNo))
                return new MessagingResult(false, "No mobile number found for this employee.");

            var uname = _config["ApiSettings:WAUsername"];
            if (string.IsNullOrWhiteSpace(uname))
                return new MessagingResult(false, "WhatsApp gateway is not configured.");

            var mobile = card.MobileNo.Length <= 10 ? "91" + card.MobileNo : card.MobileNo;
            var messageText = BuildMessageText(card);

            var http = _httpFactory.CreateClient("VisitingCardGateway");
            var token = await GetTokenAsync(http, _config["ApiSettings:WATokenUrl"], _config["ApiSettings:WAAuthKey"], uname, ct);

            // Simplified TEXT-message payload — the original standalone app sent a
            // MEDIA_TEMPLATE pointing at a public QR-code image URL. There is no public
            // share page in this pass (see plan), so this sends a plain text message
            // instead. Swap back to a media/template payload here if/when the public
            // share page is added and the bank's WABA template is confirmed.
            var request = new
            {
                uname,
                message = new
                {
                    channel = "WABA",
                    content = new { type = "TEXT", text = messageText },
                    recipient = new[] { new { to = mobile, recipient_type = "individual" } },
                    sender = new { from = _config["SendRcm:SenderFrom"] ?? "" },
                },
                metaData = new
                {
                    version = _config["SendRcm:Version"] ?? "v1.0.9",
                    originator = _config["SendRcm:Originator"] ?? "API"
                }
            };

            var actualUrl = _config["ApiSettings:WAActualApiUrl"];
            if (string.IsNullOrWhiteSpace(actualUrl))
                return new MessagingResult(false, "WhatsApp gateway is not configured.");

            using var req = new HttpRequestMessage(HttpMethod.Post, actualUrl);
            req.Headers.Add("token", token);
            req.Headers.Add("Accept", "application/json");
            req.Content = JsonContent.Create(request);
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            _logger.LogInformation("VisitingCard WhatsApp send for {EmployeeId} — status {Status}", employeeId, res.StatusCode);
            return res.IsSuccessStatusCode
                ? new MessagingResult(true, "WhatsApp message sent.")
                : new MessagingResult(false, $"Gateway returned {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VisitingCard: WhatsApp send failed for {EmployeeId}.", employeeId);
            return new MessagingResult(false, "Failed to send WhatsApp message. Please try again later.");
        }
    }

    private static string BuildMessageText(VisitingCardDto card) =>
        $"Dear Sir/Madam, this is {card.EmpName}, {card.PositionDesignation}. " +
        "View my visiting card in My Diary → profile menu → Visiting Card. - Union Bank of India";

    private async Task<string> GetTokenAsync(HttpClient http, string? tokenUrl, string? authKey, string uname, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokenUrl) || string.IsNullOrWhiteSpace(authKey))
            throw new InvalidOperationException("Gateway token endpoint is not configured.");

        using var tokenReq = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        tokenReq.Headers.Add("auth_key", authKey);
        tokenReq.Content = JsonContent.Create(new TokenRequest { Username = uname });

        using var tokenRes = await http.SendAsync(tokenReq, ct);
        tokenRes.EnsureSuccessStatusCode();

        var tokenResponse = await tokenRes.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
                             ?? throw new InvalidOperationException("Empty token response.");

        return tokenResponse.Result?.Token ?? throw new InvalidOperationException("Token missing in token response.");
    }

    // ── Wire DTOs (ported from the standalone app's Model/DTO) ─────────────────────
    private sealed class TokenRequest { [JsonPropertyName("username")] public string Username { get; set; } = ""; }
    private sealed class TokenResponse
    {
        public int StatusCode { get; set; }
        public string? Message { get; set; }
        public TokenResult? Result { get; set; }
    }
    private sealed class TokenResult { public string? Token { get; set; } }

    private sealed class SmsOutboundRequest { public string uname { get; set; } = ""; public List<SmsMessage> messages { get; set; } = new(); }
    private sealed class SmsMessage { public string dest { get; set; } = ""; public string msg { get; set; } = ""; public string send { get; set; } = ""; public string encrypt { get; set; } = "0"; }
}
