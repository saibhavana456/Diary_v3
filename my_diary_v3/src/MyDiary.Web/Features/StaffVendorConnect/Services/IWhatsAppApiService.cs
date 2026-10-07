namespace MyDiary.Web.Features.StaffVendorConnect.Services;

public sealed class WhatsAppSendResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? StatusDesc { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Karix ConnectOne RCM/WABA API client ("ConnectOne RCM API" doc v2.0) — token
/// generation + POST Template/Media-Template message. One-recipient-per-call from
/// SvcCommunicationService (it already loops recipients for per-recipient SEND_STATUS
/// tracking), even though the underlying API also supports a "recipient" array for bulk sends.</summary>
public interface IWhatsAppApiService
{
    /// <summary>Sends a text TEMPLATE message. parameterValues are positional — index 0 fills
    /// the template's {{1}}, index 1 fills {{2}}, etc. (the API's own JSON keys are "0","1",...).</summary>
    Task<WhatsAppSendResult> SendTemplateMessageAsync(string toMobile10Digit, string templateId, string language, IReadOnlyList<string> parameterValues);

    /// <summary>Sends a MEDIA_TEMPLATE message — mediaUrl must be a URL the Karix gateway can
    /// fetch itself (not raw bytes); mediaType is "image" | "video" | "document" per the API doc.</summary>
    Task<WhatsAppSendResult> SendMediaTemplateMessageAsync(string toMobile10Digit, string templateId, string mediaType, string mediaUrl);
}
