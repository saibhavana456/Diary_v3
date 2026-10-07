using System.Data;
using System.Net;
using System.Net.Mail;
using System.Text;
using DocumentFormat.OpenXml.Vml.Office;
using MyDiary.Core.Services;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace MyDiary.Web.Features.FlatManagement.Services;
public class UserGrievanceService : IUserGrievanceService
{
    private const string HelpdeskEmail = "maintenancehelpdesk@unionbankofindia.bank.in";
    private const string RaisedActionTaken = "ServiceRequest Raised";

    private readonly string _connString;
    private readonly IConfiguration _config;
    private readonly IGrievanceActionService _grievanceService;
    private readonly ILogger<UserGrievanceService> _logger;

    public UserGrievanceService(
        IConfiguration config,
        IGrievanceActionService grievanceService,
        ILogger<UserGrievanceService> logger)
    {
        _config = config;
        _grievanceService = grievanceService;
        _logger = logger;
        _connString = config.GetConnectionString("ConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ConStr in appsettings.json");
    }

    public async Task<SubmitGrievanceResult> SubmitGrievanceAsync(SubmitGrievanceRequest req)
    {
        var pfNo = (req.PfNo ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(pfNo))
            return new SubmitGrievanceResult(false, null, "You are not authorized to raise a request.");

        if (string.IsNullOrWhiteSpace(req.Category))
            return new SubmitGrievanceResult(false, null, "Select a request type.");
        if (string.IsNullOrWhiteSpace(req.Subject))
            return new SubmitGrievanceResult(false, null, "Enter a subject.");
        if (string.IsNullOrWhiteSpace(req.Description))
            return new SubmitGrievanceResult(false, null, "Enter a description.");

        var flat = await GetFlatContextAsync(pfNo);
        if (flat is null || (string.IsNullOrWhiteSpace(flat.FlatNo) && string.IsNullOrWhiteSpace(flat.BuildingName)))
            return new SubmitGrievanceResult(false, null, "You are not allotted to any flat.");

        var refNo = AppTime.Now.ToString("yyMMddHHmmssfff") + Random.Shared.Next(100, 999);

        string? attachmentPath = null;
        if (req.AttachmentData is { Length: > 0 } && !string.IsNullOrWhiteSpace(req.AttachmentFileName))
        {
            try
            {
                var stored = await _grievanceService.SaveAttachmentAsync(
                    refNo, "GRIEVANCE", req.AttachmentFileName,
                    string.IsNullOrWhiteSpace(req.AttachmentContentType) ? "application/octet-stream" : req.AttachmentContentType,
                    req.AttachmentData, pfNo);
                attachmentPath = stored.DownloadPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store grievance attachment for {PfNo}", pfNo);
                return new SubmitGrievanceResult(false, null, "Could not save the attached file. Please try again.");
            }
        }

        try
        {
            await using var cn = new OracleConnection(_connString);
            await cn.OpenAsync();
            var tx = (OracleTransaction)await cn.BeginTransactionAsync();
            try
            {
                await using (var cmd = new OracleCommand(@"
                    INSERT INTO FLATS.FLAT_GRIEVANCE
                        (REF_NO, FLAT_ID, FLAT_NO, SUBJECT, CATEGORY, DESCRIPTION, ATTACHMENT_PATH, STATUS,
                         CREATED_BY, CREATED_NAME, CREATED_ON, STATUS_DATE)
                    VALUES
                        (:REF_NO, :FLAT_ID, :FLAT_NO, :SUBJECT, :CATEGORY, :DESCRIPTION, :ATTACHMENT_PATH, :STATUS,
                         :CREATED_BY, :CREATED_NAME, SYSDATE, SYSDATE)", cn)
                { Transaction = tx, BindByName = true })
                {
                    cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
                    cmd.Parameters.Add("FLAT_ID", OracleDbType.Varchar2).Value = (object?)flat.FlatNo ?? DBNull.Value;
                    cmd.Parameters.Add("FLAT_NO", OracleDbType.Varchar2).Value = (object?)flat.FlatNo ?? DBNull.Value;
                    cmd.Parameters.Add("SUBJECT", OracleDbType.Varchar2).Value = req.Subject.Trim();
                    cmd.Parameters.Add("CATEGORY", OracleDbType.Varchar2).Value = req.Category.Trim();
                    cmd.Parameters.Add("DESCRIPTION", OracleDbType.Clob).Value = req.Description.Trim();
                    cmd.Parameters.Add("ATTACHMENT_PATH", OracleDbType.NVarchar2).Value = (object?)attachmentPath ?? DBNull.Value;
                    cmd.Parameters.Add("STATUS", OracleDbType.Varchar2).Value = "Open";
                    cmd.Parameters.Add("CREATED_BY", OracleDbType.Varchar2).Value = pfNo;
                    cmd.Parameters.Add("CREATED_NAME", OracleDbType.Varchar2).Value = req.CreatedByName ?? pfNo;
                    await cmd.ExecuteNonQueryAsync();
                }

                int actionId;
                await using (var cmd = new OracleCommand(@"
                    INSERT INTO FLATS.FLAT_GRIEVANCE_ACTION
                        (REF_NO, ACTION_DATE, ACTION_BY, ACTION_TAKEN, REMARKS, ATTACHMENT_PATH)
                    VALUES
                        (:REF_NO, SYSDATE, :ACTION_BY, :ACTION_TAKEN, :REMARKS, :ATTACHMENT_PATH)
                    RETURNING ACTION_ID INTO :ACTION_ID", cn)
                { Transaction = tx, BindByName = true })
                {
                    cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
                    cmd.Parameters.Add("ACTION_BY", OracleDbType.Varchar2).Value = pfNo;
                    cmd.Parameters.Add("ACTION_TAKEN", OracleDbType.Varchar2).Value = RaisedActionTaken;
                    cmd.Parameters.Add("REMARKS", OracleDbType.Clob).Value = req.Description.Trim();
                    cmd.Parameters.Add("ATTACHMENT_PATH", OracleDbType.NVarchar2).Value = (object?)attachmentPath ?? DBNull.Value;

                    var outId = new OracleParameter("ACTION_ID", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(outId);
                    await cmd.ExecuteNonQueryAsync();
                    actionId = ((OracleDecimal)outId.Value).ToInt32();
                }

                await using (var cmd = new OracleCommand(@"
                    INSERT INTO FLATS.GRIEVANCE_ACTION_DESC
                        (ACTION_ID, REF_NO, ACTION_TAKEN, ACTION_DETAILS, ATTACHMENT_PATH, ACTION_DATE, REMARKS)
                    VALUES
                        (:ACTION_ID, :REF_NO, :ACTION_TAKEN, :DETAILS, :ATTACHMENT_PATH, SYSDATE, :REMARKS)", cn)
                { Transaction = tx, BindByName = true })
                {
                    cmd.Parameters.Add("ACTION_ID", OracleDbType.Int32).Value = actionId;
                    cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
                    cmd.Parameters.Add("ACTION_TAKEN", OracleDbType.Varchar2).Value = RaisedActionTaken;
                    cmd.Parameters.Add("DETAILS", OracleDbType.Clob).Value = req.Description.Trim();
                    cmd.Parameters.Add("ATTACHMENT_PATH", OracleDbType.NVarchar2).Value = (object?)attachmentPath ?? DBNull.Value;
                    cmd.Parameters.Add("REMARKS", OracleDbType.Clob).Value = req.Description.Trim();
                    await cmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitGrievanceAsync failed for {PfNo}", pfNo);
            return new SubmitGrievanceResult(false, null, "Error while saving your request. Please try again later.");
        }

        await SendGrievanceRaisedEmailAsync(refNo, req, flat);

        return new SubmitGrievanceResult(true, refNo, null);
    }

    public async Task<List<TrackGrievanceRow>> GetMyGrievancesAsync(string pfNo)
    {
        var username = (pfNo ?? string.Empty).Trim();
        var result = new List<TrackGrievanceRow>();
        if (string.IsNullOrEmpty(username)) return result;

        const string sql = @"
            SELECT REF_NO, FLAT_ID, CATEGORY, SUBJECT, STATUS, CREATED_ON, CREATED_BY
            FROM FLATS.FLAT_GRIEVANCE
            WHERE UPPER(TRIM(CREATED_BY)) = UPPER(TRIM(:U))
            ORDER BY REF_NO DESC";

        try
        {
            await using var cn = new OracleConnection(_connString);
            await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
            cmd.Parameters.Add("U", OracleDbType.Varchar2).Value = username;
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new TrackGrievanceRow(
                    RefNo: r.GetString(0),
                    FlatId: r.IsDBNull(1) ? null : r.GetString(1),
                    Category: r.IsDBNull(2) ? null : r.GetString(2),
                    Subject: r.IsDBNull(3) ? null : r.GetString(3),
                    Status: r.IsDBNull(4) ? null : r.GetString(4),
                    CreatedOn: SafeGetDateTime(r, 5),
                    CreatedBy: r.IsDBNull(6) ? null : r.GetString(6)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyGrievancesAsync failed for {PfNo}", username);
        }

        return result;
    }

    private static DateTime? SafeGetDateTime(OracleDataReader r, int ordinal)
    {
        if (r.IsDBNull(ordinal)) return null;
        try
        {
            return r.GetDateTime(ordinal);
        }
        catch
        {
            try
            {
                var raw = r.GetValue(ordinal);
                if (raw is DateTime dt) return dt;
                if (raw is not null && DateTime.TryParse(raw.ToString(), out var parsed)) return parsed;
            }
            catch
            {
                // fall through to null below
            }
            return null;
        }
    }

    public async Task<SubmitFeedbackResult> SubmitFeedbackAsync(SubmitFeedbackRequest req)
    {
        var pfNo = (req.PfNo ?? string.Empty).Trim();
        var refNo = (req.RefNo ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(pfNo) || string.IsNullOrEmpty(refNo))
            return new SubmitFeedbackResult(false, "Invalid request.");

        if (req.WorkCompleted && req.Rating is null)
            return new SubmitFeedbackResult(false, "Please provide a rating.");
        if (string.IsNullOrWhiteSpace(req.Feedback))
            return new SubmitFeedbackResult(false, "Please enter your feedback.");

        var grievance = await _grievanceService.GetGrievanceAsync(refNo);
        if (grievance is null || !string.Equals(grievance.CreatedByCode, pfNo, StringComparison.OrdinalIgnoreCase))
            return new SubmitFeedbackResult(false, "Service request not found.");

        var alreadySubmitted = grievance.FeedbackDate is not null
            || (!string.IsNullOrWhiteSpace(grievance.CompletedYn) && !string.Equals(grievance.CompletedYn, "N", StringComparison.OrdinalIgnoreCase));
        if (alreadySubmitted)
            return new SubmitFeedbackResult(false, "Feedback has already been submitted for this request.");

        if (!string.Equals(grievance.Status, "Work Completed", StringComparison.OrdinalIgnoreCase))
            return new SubmitFeedbackResult(false, "Feedback can only be submitted after work has been completed.");

        string? attachmentPath = null;
        if (req.AttachmentData is { Length: > 0 } && !string.IsNullOrWhiteSpace(req.AttachmentFileName))
        {
            try
            {
                var stored = await _grievanceService.SaveAttachmentAsync(
                    refNo, "FEEDBACK", req.AttachmentFileName,
                    string.IsNullOrWhiteSpace(req.AttachmentContentType) ? "application/octet-stream" : req.AttachmentContentType,
                    req.AttachmentData, pfNo);
                attachmentPath = stored.DownloadPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store feedback attachment for {RefNo}", refNo);
                return new SubmitFeedbackResult(false, "Could not save the attached file. Please try again.");
            }
        }

        var completedYn = req.WorkCompleted ? "Y" : "N";
        var ratingText = req.WorkCompleted ? req.Rating?.ToString() : null;
        const string actionTaken = "Feedback Submitted";
        var remarks = $"Completed={completedYn}, Rating={(string.IsNullOrEmpty(ratingText) ? "NA" : ratingText)}";

        try
        {
            await using var cn = new OracleConnection(_connString);
            await cn.OpenAsync();
            var tx = (OracleTransaction)await cn.BeginTransactionAsync();
            try
            {
                await using (var cmd = new OracleCommand(@"
                    UPDATE FLATS.FLAT_GRIEVANCE
                    SET COMPLETED_YN = :COMPLETED_YN,
                        RATING = :RATING,
                        FEEDBACK = :FEEDBACK,
                        FEEDBACK_ATTACHMENT_PATH = :FILE_PATH,
                        FEEDBACK_BY = :FEEDBACK_BY,
                        FEEDBACK_DATE = SYSDATE,
                        STATUS = CASE WHEN :COMPLETED_YN = 'Y' THEN 'Closed' ELSE STATUS END,
                        STATUS_DATE = CASE WHEN :COMPLETED_YN = 'Y' THEN SYSDATE ELSE STATUS_DATE END
                    WHERE REF_NO = :REF_NO", cn)
                { Transaction = tx, BindByName = true })
                {
                    cmd.Parameters.Add("COMPLETED_YN", OracleDbType.Varchar2).Value = completedYn;
                    cmd.Parameters.Add("RATING", OracleDbType.Varchar2).Value = (object?)ratingText ?? DBNull.Value;
                    cmd.Parameters.Add("FEEDBACK", OracleDbType.Clob).Value = req.Feedback.Trim();
                    cmd.Parameters.Add("FILE_PATH", OracleDbType.NVarchar2).Value = (object?)attachmentPath ?? DBNull.Value;
                    cmd.Parameters.Add("FEEDBACK_BY", OracleDbType.Varchar2).Value = pfNo;
                    cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
                    await cmd.ExecuteNonQueryAsync();
                }

                int actionId;
                await using (var cmd = new OracleCommand(@"
                    INSERT INTO FLATS.FLAT_GRIEVANCE_ACTION
                        (REF_NO, ACTION_DATE, ACTION_BY, ACTION_TAKEN, REMARKS, ATTACHMENT_PATH)
                    VALUES
                        (:REF_NO, SYSDATE, :ACTION_BY, :ACTION_TAKEN, :REMARKS, :ATTACHMENT_PATH)
                    RETURNING ACTION_ID INTO :ACTION_ID", cn)
                { Transaction = tx, BindByName = true })
                {
                    cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
                    cmd.Parameters.Add("ACTION_BY", OracleDbType.Varchar2).Value = pfNo;
                    cmd.Parameters.Add("ACTION_TAKEN", OracleDbType.Varchar2).Value = actionTaken;
                    cmd.Parameters.Add("REMARKS", OracleDbType.Clob).Value = remarks;
                    cmd.Parameters.Add("ATTACHMENT_PATH", OracleDbType.NVarchar2).Value = (object?)attachmentPath ?? DBNull.Value;

                    var outId = new OracleParameter("ACTION_ID", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(outId);
                    await cmd.ExecuteNonQueryAsync();
                    actionId = ((OracleDecimal)outId.Value).ToInt32();
                }

                await using (var cmd = new OracleCommand(@"
                    INSERT INTO FLATS.GRIEVANCE_ACTION_DESC
                        (ACTION_ID, REF_NO, ACTION_TAKEN, ACTION_DETAILS, ATTACHMENT_PATH, ACTION_DATE, REMARKS)
                    VALUES
                        (:ACTION_ID, :REF_NO, :ACTION_TAKEN, :DETAILS, :ATTACHMENT_PATH, SYSDATE, :REMARKS)", cn)
                { Transaction = tx, BindByName = true })
                {
                    cmd.Parameters.Add("ACTION_ID", OracleDbType.Int32).Value = actionId;
                    cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
                    cmd.Parameters.Add("ACTION_TAKEN", OracleDbType.Varchar2).Value = actionTaken;
                    cmd.Parameters.Add("DETAILS", OracleDbType.Clob).Value = req.Feedback.Trim();
                    cmd.Parameters.Add("ATTACHMENT_PATH", OracleDbType.NVarchar2).Value = (object?)attachmentPath ?? DBNull.Value;
                    cmd.Parameters.Add("REMARKS", OracleDbType.Clob).Value = remarks;
                    await cmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitFeedbackAsync failed for {RefNo}", refNo);
            return new SubmitFeedbackResult(false, "Failed to save feedback. Please try again later.");
        }

        return new SubmitFeedbackResult(true, null);
    }

    private async Task<GrievanceFlatContext?> GetFlatContextAsync(string pfNo)
    {
        const string sql = @"
            SELECT VWFD.FLAT_NO, VWFD.BUILDING_NAME, VWFD.ADDRESS, VWFD.FLAT_TYPE, VWFD.CARPET_AREA, SD.EMAIL_ID
            FROM VW_FLAT_DETAILS VWFD
            JOIN ORGANISATION.STAFF_DETAILS SD ON UPPER(TRIM(SD.PF_NO)) = UPPER(TRIM(VWFD.PF_NO))
            WHERE UPPER(TRIM(VWFD.PF_NO)) = UPPER(TRIM(:U))
            ORDER BY VWFD.OCCUPIED_FROM DESC
            FETCH FIRST 1 ROWS ONLY";

        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("U", OracleDbType.Varchar2).Value = pfNo;
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new GrievanceFlatContext(
            FlatNo: r.IsDBNull(0) ? null : r.GetString(0),
            BuildingName: r.IsDBNull(1) ? null : r.GetString(1),
            Address: r.IsDBNull(2) ? null : r.GetString(2),
            FlatType: r.IsDBNull(3) ? null : r.GetString(3),
            CarpetArea: r.IsDBNull(4) ? null : r.GetValue(4).ToString(),
            UserMail: r.IsDBNull(5) ? null : r.GetString(5));
    }

    private async Task SendGrievanceRaisedEmailAsync(string refNo, SubmitGrievanceRequest req, GrievanceFlatContext flat)
    {
        if (string.IsNullOrWhiteSpace(flat.UserMail))
        {
            _logger.LogWarning("Skipping grievance-raised email for {RefNo}: no email on file for {PfNo}", refNo, req.PfNo);
            return;
        }

        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(HelpdeskEmail),
                Subject = $"New ServiceRequest Raised | Ref No: {refNo}",
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
            };
            AddIfValid(mail.To, HelpdeskEmail, "Maintenance Helpdesk");
            AddIfValid(mail.CC, flat.UserMail, req.CreatedByName);
            AddIfValid(mail.ReplyToList, HelpdeskEmail, "Maintenance Helpdesk");
            mail.Body = BuildGrievanceRaisedEmailBody(refNo, req, flat);

            var smtp = _config.GetSection("Smtp");
            using var client = new SmtpClient(smtp["Host"], int.TryParse(smtp["Port"], out var port) ? port : 25)
            {
                EnableSsl = bool.TryParse(smtp["EnableSsl"], out var ssl) && ssl,
                UseDefaultCredentials = bool.TryParse(smtp["UseDefaultCredentials"], out var useDefault) && useDefault,
                Timeout = 120000,
            };
            if (!client.UseDefaultCredentials)
                client.Credentials = new NetworkCredential(smtp["User"], smtp["Credentials"]);
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            await client.SendMailAsync(mail);

            await MarkEmailSentAsync(refNo, req.PfNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send grievance-raised notification for {RefNo}", refNo);
        }
    }

    private async Task MarkEmailSentAsync(string refNo, string sentBy)
    {
        const string sql = @"
            MERGE INTO FLATS.GRIEVANCE_EMAIL_AUDIT tgt
            USING (SELECT :REF_NO AS REF_NO FROM DUAL) src
               ON (tgt.REF_NO = src.REF_NO)
             WHEN MATCHED THEN
                 UPDATE SET tgt.ASSIGNEDTOVENDOR_EMAILSENT = 1, tgt.SENT_ON = SYSTIMESTAMP, tgt.SENT_BY = :SENT_BY
             WHEN NOT MATCHED THEN
                 INSERT (REF_NO, ASSIGNEDTOVENDOR_EMAILSENT, SENT_ON, SENT_BY)
                 VALUES (:REF_NO, 1, SYSTIMESTAMP, :SENT_BY)";

        try
        {
            await using var cn = new OracleConnection(_connString);
            await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
            cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2, 30).Value = refNo;
            cmd.Parameters.Add("SENT_BY", OracleDbType.Varchar2, 50).Value = (object?)sentBy ?? DBNull.Value;
            await cn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarkEmailSentAsync failed for {RefNo}", refNo);
        }
    }

    private static string BuildGrievanceRaisedEmailBody(string refNo, SubmitGrievanceRequest req, GrievanceFlatContext flat)
    {
        var sb = new StringBuilder();
        sb.Append("<div style='background-color:#f4f4f4;padding:20px;font-family:\"Segoe UI\",Tahoma,Geneva,Verdana,sans-serif;color:#333;'>");
        sb.Append("<div style='max-width:600px;margin:0 auto;background:#fff;border-radius:8px;overflow:hidden;border:1px solid #e0e0e0;box-shadow:0 2px 4px rgba(0,0,0,0.1);'>");
        sb.Append("<div style='padding:30px;'>");
        sb.Append("<p style='font-size:16px;'>Dear Sir/Madam,</p>");
        sb.Append("<p style='line-height:1.5;'>This is to inform you that your complaint has been raised with the following details:</p>");
        sb.Append("<table style='width:100%;border-collapse:collapse;margin-top:20px;border:1px solid #f0f0f0;'>");
        AppendRow(sb, "Reference No", refNo, true);
        AppendRow(sb, "Subject", req.Subject, false);
        AppendRow(sb, "Category", req.Category, false);
        AppendRow(sb, "Description", req.Description, false);
        AppendRow(sb, "Flat No", flat.FlatNo, false);
        AppendRow(sb, "Building", flat.BuildingName, false);
        AppendRow(sb, "Raised By", req.CreatedByName, false);
        AppendRow(sb, "Address", flat.Address, false);
        AppendRow(sb, "Status", RaisedActionTaken, false);
        AppendRow(sb, "Status Date", AppTime.Now.ToString("dd/MM/yyyy"), false);
        sb.Append("</table>");
        sb.Append("<hr style='border:0;border-top:1px solid #eeeeee;margin:25px 0;'>");
        sb.Append("<p style='margin:0;font-weight:bold;color:#003366;'>Regards,</p>");
        sb.Append("<p style='margin:4px 0;font-size:14px;'>Team Maintenance<br/>Union Bank of India</p>");
        sb.Append("<p style='margin:4px 0;font-size:12px;color:#0055aa;'>").Append(WebUtility.HtmlEncode(HelpdeskEmail)).Append("</p>");
        sb.Append("</div></div></div>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, string? value, bool highlight)
    {
        var bg = highlight ? "#f9f9f9" : "#ffffff";
        sb.Append($"<tr style='background-color:{bg};'>");
        sb.Append("<td style='padding:12px;border-bottom:1px solid #f0f0f0;width:30%;color:#777;font-weight:bold;font-size:13px;text-transform:uppercase;'>").Append(label).Append("</td>");
        sb.Append("<td style='padding:12px;border-bottom:1px solid #f0f0f0;font-size:14px;'>").Append(WebUtility.HtmlEncode(value ?? "N/A")).Append("</td>");
        sb.Append("</tr>");
    }

    private static void AddIfValid(MailAddressCollection col, string? email, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        try
        {
            var addr = string.IsNullOrWhiteSpace(displayName) ? new MailAddress(email.Trim()) : new MailAddress(email.Trim(), displayName.Trim());
            foreach (MailAddress existing in col)
                if (string.Equals(existing.Address, addr.Address, StringComparison.OrdinalIgnoreCase)) return;
            col.Add(addr);
        }
        catch (FormatException) { }
    }
}
