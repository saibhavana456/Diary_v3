using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MyDiary.Core.Abstractions;

namespace MyDiary.Web.Notify;

/// <summary>
/// SMTP options bound from the "Smtp" configuration section.
/// </summary>
public sealed class SmtpOptions
{
    public string  Host        { get; set; } = "";
    public int     Port        { get; set; } = 25;
    public string  FromName    { get; set; } = "My Diary";
    public string  FromAddress { get; set; } = "no-reply@bank.local";
    public string? Username    { get; set; }
    public string? Password    { get; set; }
    public bool    UseStartTls { get; set; } = false;
}

/// <summary>
/// MailKit-based email sender (migrated from Project A's SmtpEmailSender).
/// Sends an HTML message; falls back to plain text rendering by clients that
/// cannot display HTML.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _opts;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> opts, ILogger<SmtpEmailSender> logger)
    {
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.Host))
            throw new InvalidOperationException("SMTP Host is not configured (set Smtp:Host).");

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_opts.FromName, _opts.FromAddress));
        msg.To.Add(MailboxAddress.Parse(toAddress));
        msg.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            // Provide a plain-text alternative by stripping tags crudely
            TextBody = System.Text.RegularExpressions.Regex.Replace(htmlBody, "<.*?>", string.Empty)
        };
        msg.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(
                _opts.Host, _opts.Port,
                _opts.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                ct);

            if (!string.IsNullOrEmpty(_opts.Username))
                await client.AuthenticateAsync(_opts.Username, _opts.Password ?? string.Empty, ct);

            await client.SendAsync(msg, ct);
            _logger.LogInformation("Email sent to {To} — {Subject}", toAddress, subject);
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true, ct);
        }
    }
}

/// <summary>
/// No-op SMS sender — logs only. Replace with a gateway integration when available.
/// </summary>
public sealed class NoOpSmsSender : ISmsSender
{
    private readonly ILogger<NoOpSmsSender> _logger;
    public NoOpSmsSender(ILogger<NoOpSmsSender> logger) => _logger = logger;

    public Task SendAsync(string toNumber, string text, CancellationToken ct = default)
    {
        _logger.LogInformation("[NoOpSms] to={To} body={Body}", toNumber, text);
        return Task.CompletedTask;
    }
}
