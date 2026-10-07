using System.Net.Mail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using MyDiary.Web.Api;
using MyDiary.Web.Features.StaffVendorConnect.Models.DTO;

namespace MyDiary.Web.Features.StaffVendorConnect.Services;

/// <summary>
/// Notification center for Staff and Vendor Connect. Email is sent for real over the app's
/// existing SMTP config (same MailHog-in-dev setup as Positive Branch List). WhatsApp is sent
/// for real via the Karix ConnectOne RCM API (WhatsAppApiService) — "the apis will be shared
/// separately" has since been fulfilled for this channel. SMS still has no gateway doc, so it
/// stays logged with SEND_STATUS='QUEUED' until one is provided.
/// </summary>
public sealed class SvcCommunicationService : ISvcCommunicationService
{
    private readonly string _connString;
    private readonly string _sqlServerConnString;
    private readonly IConfiguration _config;
    private readonly ILogger<SvcCommunicationService> _logger;
    private readonly IWhatsAppApiService _whatsApp;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly global::RequestPortal.Core.Abstractions.INotifRepo _notifRepo;

    public SvcCommunicationService(IConfiguration config, ILogger<SvcCommunicationService> logger, IWhatsAppApiService whatsApp,
        IDataProtectionProvider dataProtection, global::RequestPortal.Core.Abstractions.INotifRepo notifRepo)
    {
        _connString = config.GetConnectionString("RP_Owner")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:RP_Owner in appsettings.json");
        // SQL Server (OrganizationsDB) — STAFF recipients now come from the same HRMS StaffDetails
        // table that backs the Staff Directory, so the recipient list matches what the page shows.
        _sqlServerConnString = config.GetConnectionString("OrganisationsSQLCon")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:OrganisationsSQLCon in appsettings.json");
        _config = config;
        _logger = logger;
        _whatsApp = whatsApp;
        _dataProtection = dataProtection;
        _notifRepo = notifRepo;
    }

    public async Task<List<global::RequestPortal.Core.Models.NotifTemplate>> GetApprovedSmsTemplatesAsync()
    {
        var all = await _notifRepo.ListTemplatesAsync();
        return all.Where(t => t.Channel == global::RequestPortal.Core.NotifChannel.Sms && t.IsActive)
                   .OrderBy(t => t.EventCode).ToList();
    }

    private static string InferMediaType(string contentType) => contentType.ToLowerInvariant() switch
    {
        var c when c.StartsWith("image/") => "image",
        var c when c.StartsWith("video/") => "video",
        _ => "document"
    };

    /// <summary>The attachment row must exist (already inserted) before this is called — the
    /// signed URL just names it, it doesn't create it.</summary>
    private Task<string> BuildTempAttachmentUrlAsync(OracleConnection cn, long commId, SvcAttachmentFile _)
    {
        // The attachment's own DB id (not commId) is what the public URL keys off; look it up
        // by (COMM_ID, FILE_NAME) since that's the only identity available at this call site.
        return ResolveAttachmentIdAndBuildUrlAsync(cn, commId, _.FileName);
    }

    private async Task<string> ResolveAttachmentIdAndBuildUrlAsync(OracleConnection cn, long commId, string fileName)
    {
        await using var cmd = new OracleCommand(
            "SELECT ID FROM RP_SVC_COMM_ATTACHMENT WHERE COMM_ID = :p_comm AND FILE_NAME = :p_name AND ROWNUM = 1", cn);
        cmd.Parameters.Add("p_comm", commId);
        cmd.Parameters.Add("p_name", fileName);
        var idObj = await cmd.ExecuteScalarAsync();
        var attachmentId = Convert.ToInt64(idObj);
        return SvcCommAttachmentController.BuildPublicUrl(_config, _dataProtection, attachmentId);
    }

    private OracleConnection Conn() => new(_connString);

    public async Task<List<SvcRecipientCandidate>> GetRecipientCandidatesAsync(string targetModule, string? categoryCode)
    {
        var result = new List<SvcRecipientCandidate>();

        if (targetModule == "STAFF")
        {
            // STAFF recipients from the HRMS StaffDetails table (SQL Server / OrganizationsDB),
            // mirroring the Staff Directory: active staff (HR_STATUS='A') with a Category
            // (UBI_SPCL_OFFICER present); categoryCode filters on that same column.
            //   EMPLID → Id, NAME → Name, EMAIL → Email, PHONE → Mobile
            await using var sqlCn = new SqlConnection(_sqlServerConnString);
            await sqlCn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT EMPLID, NAME, EMAIL, PHONE FROM StaffDetails " +
                "WHERE HR_STATUS = 'A' " +
                "AND UBI_SPCL_OFFICER IS NOT NULL AND LTRIM(RTRIM(UBI_SPCL_OFFICER)) <> '' " +
                (categoryCode is not null ? "AND UBI_SPCL_OFFICER = @p_cat " : "") +
                "ORDER BY NAME", sqlCn);
            if (categoryCode is not null) cmd.Parameters.AddWithValue("@p_cat", categoryCode);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new SvcRecipientCandidate
                {
                    Id = r.GetString(0), Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Email = r.IsDBNull(2) ? null : r.GetString(2), Mobile = r.IsDBNull(3) ? null : r.GetString(3)
                });
        }
        else // VENDOR — pull each record's own captured email/mobile field values, if the category has them
        {
            await using var cn = Conn();
            await cn.OpenAsync();
            await using var cmd = new OracleCommand(@"
                SELECT r.RECORD_ID,
                       MAX(CASE WHEN f.FIELD_KEY IN ('EMAIL_ID') THEN v.FIELD_VALUE END) AS EMAIL,
                       MAX(CASE WHEN f.FIELD_KEY IN ('MOBILE_NO','GUARD_CONTACT','PSA_CONTACT') THEN v.FIELD_VALUE END) AS MOBILE,
                       MAX(CASE WHEN f.FIELD_KEY IN ('FIRM_NAME','CONTACT_PERSON','GUARD_NAME','PSA_NAME') THEN v.FIELD_VALUE END) AS NAME
                FROM RP_SVC_VENDOR_RECORD r
                JOIN RP_SVC_VENDOR_FIELD_VALUE v ON v.RECORD_ID = r.RECORD_ID
                JOIN RP_SVC_FIELD_DEFINITION f ON f.FIELD_ID = v.FIELD_ID
                WHERE r.STATUS = 'ACTIVE'" + (categoryCode is not null ? " AND r.CATEGORY_CODE = :p_cat" : "") + @"
                GROUP BY r.RECORD_ID", cn);
            if (categoryCode is not null) cmd.Parameters.Add("p_cat", categoryCode);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new SvcRecipientCandidate
                {
                    Id = r.GetInt64(0).ToString(),
                    Name = r.IsDBNull(3) ? $"Record #{r.GetInt64(0)}" : r.GetString(3),
                    Email = r.IsDBNull(1) ? null : r.GetString(1),
                    Mobile = r.IsDBNull(2) ? null : r.GetString(2)
                });
        }
        return result;
    }

    public async Task<(long CommId, int Sent, int Failed)> SendAsync(SvcSendCommunicationRequest request, string actor)
    {
        var candidates = await GetRecipientCandidatesAsync(request.TargetModule, null);
        var targets = request.SendToAll
            ? candidates
            : candidates.Where(c => request.SelectedRecipientIds.Contains(c.Id)).ToList();

        await using var cn = Conn();
        await cn.OpenAsync();

        long commId;
        await using (var ins = new OracleCommand(@"
            INSERT INTO RP_SVC_COMM_LOG (COMM_ID, TARGET_MODULE, CHANNEL, SUBJECT, BODY, TEMPLATE_NAME,
                RECIPIENT_SCOPE, RECIPIENT_COUNT, SENT_BY)
            VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_mod, :p_chan, :p_subj, :p_body, :p_tmpl, :p_scope, :p_count, :p_by)
            RETURNING COMM_ID INTO :p_outid", cn) { BindByName = true })
        {
            ins.Parameters.Add("p_mod", request.TargetModule);
            ins.Parameters.Add("p_chan", request.Channel);
            ins.Parameters.Add("p_subj", (object?)request.Subject ?? DBNull.Value);
            ins.Parameters.Add(new OracleParameter("p_body", OracleDbType.Clob) { Value = request.Body });
            ins.Parameters.Add("p_tmpl", (object?)request.TemplateName ?? DBNull.Value);
            ins.Parameters.Add("p_scope", request.SendToAll ? "ALL" : "SELECTED");
            ins.Parameters.Add("p_count", targets.Count);
            ins.Parameters.Add("p_by", actor);
            var outId = new OracleParameter("p_outid", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            ins.Parameters.Add(outId);
            await ins.ExecuteNonQueryAsync();
            commId = Convert.ToInt64(((OracleDecimal)outId.Value).Value);
        }

        // Attachments (Email/WhatsApp only — the dialog never collects any for SMS) are stored
        // once per communication, not once per recipient.
        foreach (var att in request.Attachments)
        {
            await using var attCmd = new OracleCommand(@"
                INSERT INTO RP_SVC_COMM_ATTACHMENT (ID, COMM_ID, FILE_NAME, MIME, CONTENT)
                VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_comm, :p_name, :p_mime, :p_content)", cn) { BindByName = true };
            attCmd.Parameters.Add("p_comm", commId);
            attCmd.Parameters.Add("p_name", att.FileName);
            attCmd.Parameters.Add("p_mime", att.ContentType);
            attCmd.Parameters.Add(new OracleParameter("p_content", OracleDbType.Blob) { Value = att.Content });
            await attCmd.ExecuteNonQueryAsync();
        }

        int sent = 0, failed = 0;
        foreach (var target in targets)
        {
            string status; string? error = null; string address;

            if (request.Channel == "EMAIL")
            {
                address = target.Email ?? "";
                if (string.IsNullOrWhiteSpace(address)) { status = "FAILED"; error = "No email address on file."; }
                else
                {
                    try
                    {
                        await SendEmailAsync(address, request.Subject ?? "(no subject)", request.Body, request.Attachments);
                        status = "SENT";
                    }
                    catch (Exception ex)
                    {
                        status = "FAILED"; error = ex.Message;
                        _logger.LogWarning(ex, "SVC comm: email failed for {Address}", address);
                    }
                }
            }
            else if (request.Channel == "WHATSAPP")
            {
                address = target.Mobile ?? "";
                if (string.IsNullOrWhiteSpace(address)) { status = "FAILED"; error = "No mobile number on file."; }
                else if (string.IsNullOrWhiteSpace(request.WhatsAppTemplateId)) { status = "FAILED"; error = "No WhatsApp template selected."; }
                else
                {
                    var result = request.Attachments.Count > 0
                        ? await _whatsApp.SendMediaTemplateMessageAsync(address, request.WhatsAppTemplateId, InferMediaType(request.Attachments[0].ContentType), await BuildTempAttachmentUrlAsync(cn, commId, request.Attachments[0]))
                        : await _whatsApp.SendTemplateMessageAsync(address, request.WhatsAppTemplateId, request.WhatsAppLanguage, request.WhatsAppParameterValues);
                    status = result.Success ? "SENT" : "FAILED";
                    error = result.Success ? null : result.ErrorMessage;
                }
            }
            else // SMS — no gateway doc provided yet; queue for when one is
            {
                address = target.Mobile ?? "";
                status = string.IsNullOrWhiteSpace(address) ? "FAILED" : "QUEUED";
                if (status == "FAILED") error = "No mobile number on file.";
            }

            if (status is "SENT" or "QUEUED") sent++; else failed++;

            await using var rec = new OracleCommand(@"
                INSERT INTO RP_SVC_COMM_RECIPIENT (COMM_ID, RECIPIENT_ID, RECIPIENT_NAME, RECIPIENT_ADDRESS, SEND_STATUS, ERROR_MESSAGE)
                VALUES (:p_comm, :p_rid, :p_name, :p_addr, :p_status, :p_err)", cn) { BindByName = true };
            rec.Parameters.Add("p_comm", commId);
            rec.Parameters.Add("p_rid", target.Id);
            rec.Parameters.Add("p_name", target.Name);
            // Oracle stores '' as NULL, and RECIPIENT_ADDRESS is NOT NULL — an empty address
            // (no email/mobile on file) must be written as a real placeholder, not "".
            rec.Parameters.Add("p_addr", string.IsNullOrEmpty(address) ? "(none on file)" : address);
            rec.Parameters.Add("p_status", status);
            rec.Parameters.Add("p_err", (object?)error ?? DBNull.Value);
            await rec.ExecuteNonQueryAsync();
        }

        return (commId, sent, failed);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, List<SvcAttachmentFile> attachments)
    {
        var host = _config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("SMTP Host is not configured (Smtp:Host).");

        using var message = new MailMessage
        {
            From = new MailAddress(_config["Smtp:FromAddress"] ?? "no-reply@dev.local", _config["Smtp:FromName"] ?? "My Diary"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        // MailMessage.Dispose() disposes every Attachment (and the MemoryStream each one wraps)
        // it holds — safe to leave undisposed here individually as long as `message` itself is
        // disposed (it is, via the enclosing `using`).
        foreach (var att in attachments)
            message.Attachments.Add(new Attachment(new MemoryStream(att.Content), att.FileName, att.ContentType));

        using var smtp = new SmtpClient(host, int.TryParse(_config["Smtp:Port"], out var port) ? port : 25)
        {
            EnableSsl = bool.TryParse(_config["Smtp:UseStartTls"], out var tls) && tls
        };
        await smtp.SendMailAsync(message);
    }

    public async Task<List<SvcCommLogEntry>> GetCommLogAsync(string? targetModule, int maxRows = 100)
    {
        var result = new List<SvcCommLogEntry>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand($@"
            SELECT * FROM (
                SELECT COMM_ID, TARGET_MODULE, CHANNEL, SUBJECT, BODY, TEMPLATE_NAME, RECIPIENT_SCOPE,
                       RECIPIENT_COUNT, SENT_BY, SENT_AT
                FROM RP_SVC_COMM_LOG
                WHERE :p_mod IS NULL OR TARGET_MODULE = :p_mod2
                ORDER BY SENT_AT DESC
            ) WHERE ROWNUM <= :p_max", cn) { BindByName = true };
        cmd.Parameters.Add("p_mod", (object?)targetModule ?? DBNull.Value);
        cmd.Parameters.Add("p_mod2", (object?)targetModule ?? DBNull.Value);
        cmd.Parameters.Add("p_max", maxRows);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new SvcCommLogEntry
            {
                CommId = r.GetInt64(0), TargetModule = r.GetString(1), Channel = r.GetString(2),
                Subject = r.IsDBNull(3) ? null : r.GetString(3),
                Body = r.IsDBNull(4) ? "" : r.GetOracleClob(4).Value,
                TemplateName = r.IsDBNull(5) ? null : r.GetString(5), RecipientScope = r.GetString(6),
                RecipientCount = Convert.ToInt32(r.GetDecimal(7)), SentBy = r.GetString(8), SentAt = r.GetDateTime(9)
            });
        }
        return result;
    }

    public async Task<List<SvcCommRecipientResult>> GetCommRecipientsAsync(long commId)
    {
        var result = new List<SvcCommRecipientResult>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "SELECT RECIPIENT_ID, RECIPIENT_NAME, RECIPIENT_ADDRESS, SEND_STATUS, ERROR_MESSAGE FROM RP_SVC_COMM_RECIPIENT WHERE COMM_ID = :p_id", cn);
        cmd.Parameters.Add("p_id", commId);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new SvcCommRecipientResult
            {
                RecipientId = r.GetString(0), RecipientName = r.IsDBNull(1) ? "" : r.GetString(1),
                RecipientAddress = r.GetString(2), SendStatus = r.GetString(3), ErrorMessage = r.IsDBNull(4) ? null : r.GetString(4)
            });
        }
        return result;
    }

    public async Task<List<SvcCommAttachmentInfo>> GetCommAttachmentsAsync(long commId)
    {
        var result = new List<SvcCommAttachmentInfo>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "SELECT ID, FILE_NAME, MIME, DBMS_LOB.GETLENGTH(CONTENT) FROM RP_SVC_COMM_ATTACHMENT WHERE COMM_ID = :p_id", cn);
        cmd.Parameters.Add("p_id", commId);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new SvcCommAttachmentInfo
            {
                Id = r.GetInt64(0), FileName = r.GetString(1),
                ContentType = r.IsDBNull(2) ? "" : r.GetString(2),
                SizeBytes = r.IsDBNull(3) ? 0 : Convert.ToInt64(r.GetDecimal(3))
            });
        }
        return result;
    }
}
