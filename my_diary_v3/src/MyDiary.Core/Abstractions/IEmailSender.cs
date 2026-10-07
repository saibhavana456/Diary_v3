namespace MyDiary.Core.Abstractions;

/// <summary>
/// Sends transactional email. Implemented by SmtpEmailSender (MailKit) in the Web layer.
/// Migrated from Project A's notification subsystem.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>
/// Sends transactional SMS. Default implementation is a no-op until a gateway is wired.
/// </summary>
public interface ISmsSender
{
    Task SendAsync(string toNumber, string text, CancellationToken ct = default);
}
