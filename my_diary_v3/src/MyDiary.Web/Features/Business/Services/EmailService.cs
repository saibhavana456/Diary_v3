using System.Net.Mail;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendChargeReportAsync(
        string subject,
        string body,
        string toEmail,
        byte[] pdfBytes,
        string pdfFileName)
    {
        var message = new MailMessage
        {
            From = new MailAddress("921604@unionbankofindia.bank.in"),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.To.Add(toEmail);

        message.Attachments.Add(
            new Attachment(
                new MemoryStream(pdfBytes),
                pdfFileName,
                "application/pdf"));

        var smtp = new SmtpClient
        {
            Host = _config["Smtp:Host"],
            Port = int.Parse(_config["Smtp:Port"]),
            EnableSsl = true,
            UseDefaultCredentials = true
        };

        await smtp.SendMailAsync(message);
    }
}