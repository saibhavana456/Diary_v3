namespace MyDiary.Web.Features.VisitingCard.Services;

/// <summary>
/// Sends the logged-in employee's own visiting-card details to their own registered
/// mobile number via the bank's SMS / WhatsApp gateways — ported from the standalone
/// Visiting Card app's SMSMsgService/WAMsgService. Gateway URLs/keys are config-driven
/// (see appsettings "ApiSettings"/"SmsDefaults"/"SendRcm") and are placeholders in
/// dev/this sandbox; real values must be supplied via environment variables on the
/// actual UAT/prod host, same convention as the app's SMTP/OTP integrations.
/// </summary>
public interface IVisitingCardMessagingService
{
    Task<MessagingResult> SendSmsAsync(string employeeId, CancellationToken ct = default);
    Task<MessagingResult> SendWhatsAppAsync(string employeeId, CancellationToken ct = default);
}

public sealed record MessagingResult(bool Success, string? Message);
