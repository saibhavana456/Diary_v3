public interface IEmailService
{
    Task SendChargeReportAsync(
        string subject,
        string body,
        string toEmail,
        byte[] pdfBytes,
        string pdfFileName);
}