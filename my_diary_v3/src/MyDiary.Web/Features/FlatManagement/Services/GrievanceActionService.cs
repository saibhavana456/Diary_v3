using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using MyDiary.Core.Services;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class GrievanceActionService : IGrievanceActionService
{
    private const string HelpdeskEmail = "maintenancehelpdesk@unionbankofindia.bank.in";
    private static readonly string[] AllowedAttachmentExtensions = { ".jpg", ".jpeg", ".png", ".pdf", ".docx" };

    private readonly string _connString;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<GrievanceActionService> _logger;

    public GrievanceActionService(IConfiguration config, IWebHostEnvironment env, ILogger<GrievanceActionService> logger)
    {
        _config = config;
        _env = env;
        _logger = logger;
        _connString = config.GetConnectionString("ConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ConStr in appsettings.json");
    }

    public async Task<FlatAllocationInfo?> ResolveFlatFromRefNoAsync(string refNo)
    {
        const string sql = @"
            SELECT BUILDING, ROOM_NBR, EMPLID
            FROM FLATS.PS_UBI_RQ_APP_PROC
            WHERE EMPLID IN (SELECT CREATED_BY FROM FLATS.FLAT_GRIEVANCE WHERE REF_NO = :refNo)
            ORDER BY BEGIN_DT DESC
            FETCH FIRST 1 ROWS ONLY";

        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("refNo", OracleDbType.Varchar2).Value = refNo;
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new FlatAllocationInfo(
            EmplId: r.IsDBNull(2) ? null : r.GetString(2),
            Building: r.IsDBNull(0) ? null : r.GetString(0),
            RoomNbr: r.IsDBNull(1) ? null : r.GetString(1));
    }

    public async Task<FlatDetailsInfo?> GetFlatDetailsAsync(string emplId)
    {
        const string sql = @"
            SELECT FLAT_NO, CARPET_AREA, FLAT_TYPE, ADDRESS, BUILDING_NAME
            FROM VW_FLAT_DETAILS
            WHERE PF_NO = :PF_NO
            ORDER BY OCCUPIED_FROM DESC
            FETCH FIRST 1 ROWS ONLY";

        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("PF_NO", OracleDbType.Varchar2).Value = emplId;
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new FlatDetailsInfo(
            FlatNo: r.IsDBNull(0) ? null : r.GetString(0),
            Area: r.IsDBNull(1) ? null : r.GetValue(1).ToString(),
            Type: r.IsDBNull(2) ? null : r.GetString(2),
            Address: r.IsDBNull(3) ? null : r.GetString(3),
            Location: r.IsDBNull(4) ? null : r.GetString(4));
    }

    public async Task<GrievanceDetailsInfo?> GetGrievanceAsync(string refNo)
    {
        const string sql = @"
            SELECT
                FG.REF_NO, FG.FLAT_ID, FG.FLAT_NO, FG.SUBJECT, FG.CATEGORY, FG.DESCRIPTION,
                FG.ATTACHMENT_PATH, FG.STATUS, FG.CREATED_BY, FG.CREATED_NAME, FG.CREATED_ON,
                FGA.ACTION_ID, FG.COMPLETED_YN, FG.RATING, FG.FEEDBACK, FG.FEEDBACK_ATTACHMENT_PATH, FG.FEEDBACK_DATE
            FROM FLATS.FLAT_GRIEVANCE FG
            JOIN FLATS.FLAT_GRIEVANCE_ACTION FGA ON FG.REF_NO = FGA.REF_NO
            WHERE FG.REF_NO = :REF_NO
            ORDER BY FGA.ACTION_ID DESC
            FETCH FIRST 1 ROWS ONLY";

        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new GrievanceDetailsInfo(
            RefNo: r.GetString(0),
            FlatId: r.IsDBNull(1) ? null : r.GetString(1),
            FlatNo: r.IsDBNull(2) ? null : r.GetString(2),
            Subject: r.IsDBNull(3) ? null : r.GetString(3),
            Category: r.IsDBNull(4) ? null : r.GetString(4),
            Description: r.IsDBNull(5) ? null : r.GetString(5),
            AttachmentPath: r.IsDBNull(6) ? null : r.GetString(6),
            Status: r.IsDBNull(7) ? null : r.GetString(7),
            CreatedByCode: r.IsDBNull(8) ? null : r.GetString(8),
            CreatedByName: r.IsDBNull(9) ? null : r.GetString(9),
            CreatedOn: r.IsDBNull(10) ? null : r.GetValue(10).ToString(),
            ActionId: r.GetInt32(11),
            CompletedYn: r.IsDBNull(12) ? null : r.GetString(12),
            Rating: r.IsDBNull(13) ? null : r.GetValue(13).ToString(),
            Feedback: r.IsDBNull(14) ? null : r.GetString(14),
            FeedbackAttachmentPath: r.IsDBNull(15) ? null : r.GetString(15),
            FeedbackDate: SafeGetDateTime(r, 16));
    }

    public async Task<List<ProductRow>> GetReplacedItemsAsync(string refNo)
    {
        const string sql = @"
            SELECT PRODUCT_NAME, PRODUCT_QTY, PRODUCT_DESC
            FROM FLATS.GRIEVANCE_PRODUCT_DESC
            WHERE REF_NO = :REF_NO
            ORDER BY PRODUCT_NAME";

        var result = new List<ProductRow>();
        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new ProductRow(
                ProductName: r.IsDBNull(0) ? "" : r.GetString(0),
                Qty: r.IsDBNull(1) ? 0 : Convert.ToDecimal(r.GetValue(1)),
                Description: r.IsDBNull(2) ? null : r.GetString(2)));
        }
        return result;
    }

    public async Task<List<ActionHistoryRow>> GetActionHistoryAsync(string refNo)
    {
        const string sql = @"
            SELECT
                FGA.ACTION_ID, FGA.REF_NO, GAC.ACTION_TAKEN, GAC.ACTION_DETAILS,
                GAC.REMARKS, GAC.ATTACHMENT_PATH, GAC.ACTION_DATE
            FROM FLATS.GRIEVANCE_ACTION_DESC GAC
            JOIN FLATS.FLAT_GRIEVANCE_ACTION FGA ON FGA.ACTION_ID = GAC.ACTION_ID
            WHERE FGA.REF_NO = :REF_NO
            ORDER BY GAC.ACTION_DATE DESC";

        var result = new List<ActionHistoryRow>();
        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new ActionHistoryRow(
                ActionId: r.GetInt32(0),
                RefNo: r.GetString(1),
                ActionTaken: r.IsDBNull(2) ? null : r.GetString(2),
                ActionDetails: r.IsDBNull(3) ? null : r.GetString(3),
                Remarks: r.IsDBNull(4) ? null : r.GetString(4),
                AttachmentPath: r.IsDBNull(5) ? null : r.GetString(5),
                ActionDate: SafeGetDateTime(r, 6)));
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

            }
            return null;
        }
    }

    public async Task<List<VendorOption>> GetActiveVendorsAsync()
    {
        const string sql = "SELECT VENDOR_ID, VENDOR_NAME FROM FLATS.VENDOR_MASTER WHERE STATUS = 'Active' ORDER BY VENDOR_NAME";

        var result = new List<VendorOption>();
        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new VendorOption(r.GetValue(0).ToString()!, r.IsDBNull(1) ? "" : r.GetString(1)));
        }
        return result;
    }

    public async Task<VendorInfo?> GetVendorInfoAsync(string vendorId)
    {
        const string sql = @"
            SELECT VENDOR_CODE, VENDOR_NAME, VENDOR_EMAIL, VENDOR_MOBILE, SPOC_NAME, SPOC_EMAIL, SPOC_MOBILE
            FROM FLATS.VENDOR_MASTER
            WHERE VENDOR_ID = :VENDOR_ID
            FETCH FIRST 1 ROWS ONLY";

        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("VENDOR_ID", OracleDbType.Varchar2).Value = vendorId;
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new VendorInfo(
            VendorCode: r.IsDBNull(0) ? null : r.GetString(0),
            VendorName: r.IsDBNull(1) ? null : r.GetString(1),
            VendorEmail: r.IsDBNull(2) ? null : r.GetString(2),
            VendorMobile: r.IsDBNull(3) ? null : r.GetString(3),
            SpocName: r.IsDBNull(4) ? null : r.GetString(4),
            SpocEmail: r.IsDBNull(5) ? null : r.GetString(5),
            SpocMobile: r.IsDBNull(6) ? null : r.GetString(6));
    }

    public async Task<SaveActionResult> SaveActionAsync(SaveActionRequest req)
    {
        if (string.Equals(req.ActionTaken, "Work Completed", StringComparison.OrdinalIgnoreCase) && req.Products.Count == 0)
            return new SaveActionResult(false, "Add at least one product before completing work.");

        await using var cn = new OracleConnection(_connString);
        await cn.OpenAsync();
        var tx = (OracleTransaction)await cn.BeginTransactionAsync();
        try
        {
            if (string.Equals(req.ActionTaken, "Assigned to Vendor", StringComparison.OrdinalIgnoreCase)
                && await IsAlreadyAssignedToVendorAsync(cn, tx, req.RefNo))
            {
                await tx.RollbackAsync();
                return new SaveActionResult(false, "This Service Request has already been assigned to a vendor. You cannot assign it again.");
            }

            await using (var cmd = new OracleCommand(@"
                UPDATE FLATS.FLAT_GRIEVANCE_ACTION
                   SET ACTION_DATE = :ACTION_DATE,
                       ACTION_BY = :ACTION_BY,
                       ACTION_TAKEN = :ACTION_TAKEN,
                       QUOTATION_REQUEST_DATE = :Q_REQ_DT,
                       LAST_SUBMISSION_DATE = :LAST_SUB_DT,
                       EXPECTED_VENDOR_VISIT_DATE = :EXP_VISIT_DT,
                       VENDOR_VISIT_DATE = :VENDOR_VISIT_DT,
                       QUOTATION_DATE = :Q_DT,
                       QUOTATION_AMT = :Q_AMT,
                       QUOTATION_APPROVAL_DATE = :Q_APPR_DT,
                       QUOTATION_APPROVAL_AMT = :Q_APPR_AMT,
                       QUOTATION_APPROVAL_REF_NO = :Q_APPR_REF,
                       QUOTATION_APPROVAL_BY = :Q_APPR_BY,
                       WORK_STARTED_DATE = :WS_DT,
                       WORK_IN_PROGRESS_DATE = :WIP_DT,
                       WORK_COMPLETED_DATE = :WC_DT,
                       INVOICE_NO = :INV_NO,
                       INVOICE_DATE = :INV_DT,
                       INVOICE_AMT = :INV_AMT,
                       PAYMENT_DATE = :PAY_DT,
                       PAYMENT_AMT = :PAY_AMT
                 WHERE ACTION_ID = :ACTION_ID", cn) { Transaction = tx, BindByName = true })
            {
                cmd.Parameters.Add("ACTION_ID", OracleDbType.Int32).Value = req.ActionId;
                cmd.Parameters.Add("ACTION_DATE", OracleDbType.TimeStamp).Value = AppTime.Now;
                cmd.Parameters.Add("ACTION_BY", OracleDbType.Varchar2).Value = req.EnteredByCode;
                cmd.Parameters.Add("ACTION_TAKEN", OracleDbType.Varchar2).Value = req.ActionTaken;
                cmd.Parameters.Add("Q_REQ_DT", OracleDbType.TimeStamp).Value = (object?)req.QuotationRequestDate ?? DBNull.Value;
                cmd.Parameters.Add("LAST_SUB_DT", OracleDbType.TimeStamp).Value = (object?)req.LastSubmissionDate ?? DBNull.Value;
                cmd.Parameters.Add("EXP_VISIT_DT", OracleDbType.TimeStamp).Value = (object?)req.ExpectedVendorVisitDate ?? DBNull.Value;
                cmd.Parameters.Add("VENDOR_VISIT_DT", OracleDbType.TimeStamp).Value = (object?)req.VendorVisitDate ?? DBNull.Value;
                cmd.Parameters.Add("Q_DT", OracleDbType.TimeStamp).Value = (object?)req.QuotationDate ?? DBNull.Value;
                cmd.Parameters.Add("Q_AMT", OracleDbType.Decimal).Value = (object?)req.QuotationAmt ?? DBNull.Value;
                cmd.Parameters.Add("Q_APPR_DT", OracleDbType.TimeStamp).Value = (object?)req.QuotationApprovalDate ?? DBNull.Value;
                cmd.Parameters.Add("Q_APPR_AMT", OracleDbType.Decimal).Value = (object?)req.QuotationApprovalAmt ?? DBNull.Value;
                cmd.Parameters.Add("Q_APPR_REF", OracleDbType.Varchar2).Value = (object?)req.QuotationApprovalRefNo ?? DBNull.Value;
                cmd.Parameters.Add("Q_APPR_BY", OracleDbType.Varchar2).Value = (object?)req.QuotationApprovalBy ?? DBNull.Value;
                cmd.Parameters.Add("WS_DT", OracleDbType.TimeStamp).Value = (object?)req.WorkStartedDate ?? DBNull.Value;
                cmd.Parameters.Add("WIP_DT", OracleDbType.TimeStamp).Value = (object?)req.WorkInProgressDate ?? DBNull.Value;
                cmd.Parameters.Add("WC_DT", OracleDbType.TimeStamp).Value = (object?)req.WorkCompletedDate ?? DBNull.Value;
                cmd.Parameters.Add("INV_NO", OracleDbType.Varchar2).Value = (object?)req.InvoiceNo ?? DBNull.Value;
                cmd.Parameters.Add("INV_DT", OracleDbType.TimeStamp).Value = (object?)req.InvoiceDate ?? DBNull.Value;
                cmd.Parameters.Add("INV_AMT", OracleDbType.Decimal).Value = (object?)req.InvoiceAmt ?? DBNull.Value;
                cmd.Parameters.Add("PAY_DT", OracleDbType.TimeStamp).Value = (object?)req.PaymentDate ?? DBNull.Value;
                cmd.Parameters.Add("PAY_AMT", OracleDbType.Decimal).Value = (object?)req.PaymentAmt ?? DBNull.Value;

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) throw new InvalidOperationException("No action record found to update.");
            }

            if (string.Equals(req.ActionTaken, "Assigned to Vendor", StringComparison.OrdinalIgnoreCase))
            {
                await using var cmdVendor = new OracleCommand(
                    "UPDATE FLATS.FLAT_GRIEVANCE SET VENDOR_NAME = :VENDOR_NAME, VENDOR_MOBILE = :VENDOR_MOBILE WHERE REF_NO = :REF_NO", cn)
                { Transaction = tx, BindByName = true };
                cmdVendor.Parameters.Add("VENDOR_NAME", OracleDbType.Varchar2).Value = (object?)req.VendorName ?? DBNull.Value;
                cmdVendor.Parameters.Add("VENDOR_MOBILE", OracleDbType.Varchar2).Value = (object?)req.VendorMobile ?? DBNull.Value;
                cmdVendor.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = req.RefNo;
                await cmdVendor.ExecuteNonQueryAsync();
            }

            await using (var cmd = new OracleCommand(
                "UPDATE FLATS.FLAT_GRIEVANCE SET STATUS = :STATUS WHERE REF_NO = :REF_NO", cn)
            { Transaction = tx, BindByName = true })
            {
                cmd.Parameters.Add("STATUS", OracleDbType.Varchar2).Value = req.ActionTaken;
                cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = req.RefNo;
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new OracleCommand(@"
                INSERT INTO FLATS.GRIEVANCE_ACTION_DESC
                    (ACTION_ID, REF_NO, ACTION_TAKEN, ACTION_DETAILS, ATTACHMENT_PATH, ACTION_DATE, REMARKS)
                VALUES
                    (:ACTION_ID, :REF_NO, :ACTION_TAKEN, :DETAILS, :ATTACHMENT_PATH, SYSDATE, :REMARKS)", cn)
            { Transaction = tx, BindByName = true })
            {
                cmd.Parameters.Add("ACTION_ID", OracleDbType.Int32).Value = req.ActionId;
                cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = req.RefNo;
                cmd.Parameters.Add("ACTION_TAKEN", OracleDbType.Varchar2).Value = req.ActionTaken;
                cmd.Parameters.Add("DETAILS", OracleDbType.Clob).Value = BuildActionDetails(req);
                cmd.Parameters.Add("REMARKS", OracleDbType.Clob).Value =
                    string.IsNullOrWhiteSpace(req.Remarks) ? DBNull.Value : req.Remarks;
                cmd.Parameters.Add("ATTACHMENT_PATH", OracleDbType.NVarchar2).Value =
                    string.IsNullOrWhiteSpace(req.AttachmentRelPath) ? DBNull.Value : req.AttachmentRelPath;
                await cmd.ExecuteNonQueryAsync();
            }

            if (string.Equals(req.ActionTaken, "Work Completed", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var p in req.Products)
                {
                    await using var prodCmd = new OracleCommand(@"
                        INSERT INTO FLATS.GRIEVANCE_PRODUCT_DESC (REF_NO, PRODUCT_NAME, PRODUCT_DESC, PRODUCT_QTY, ACTION_DATE)
                        VALUES (:REF_NO, :PRODUCT_NAME, :PRODUCT_DESC, :PRODUCT_QTY, SYSDATE)", cn)
                    { Transaction = tx, BindByName = true };
                    prodCmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = req.RefNo;
                    prodCmd.Parameters.Add("PRODUCT_NAME", OracleDbType.Varchar2).Value = p.ProductName;
                    prodCmd.Parameters.Add("PRODUCT_DESC", OracleDbType.Clob).Value =
                        string.IsNullOrWhiteSpace(p.Description) ? DBNull.Value : p.Description;
                    prodCmd.Parameters.Add("PRODUCT_QTY", OracleDbType.Decimal).Value = p.Qty;
                    await prodCmd.ExecuteNonQueryAsync();
                }
            }

            if (string.Equals(req.ActionTaken, "Invoice Submitted", StringComparison.OrdinalIgnoreCase))
            {
                await using var cmdVf = new OracleCommand(@"
                    UPDATE FLATS.FLAT_GRIEVANCE
                       SET FEEDBACK_BY_VENDOR = :FB,
                           FEEDBACK_RATING_BY_VENDOR = :RT,
                           FEEDBACK_GIVEN_BY_VENDOR = :VENDOR_BY,
                           FEEDBACK_DATE_BY_VENDOR = SYSDATE,
                           FEEDBACK_ATTACHMENT_BY_VENDOR = :ATT
                     WHERE REF_NO = :REF_NO", cn)
                { Transaction = tx, BindByName = true };
                cmdVf.Parameters.Add("FB", OracleDbType.NVarchar2).Value =
                    string.IsNullOrWhiteSpace(req.VendorFeedback) ? DBNull.Value : req.VendorFeedback;
                cmdVf.Parameters.Add("RT", OracleDbType.Int32).Value = (object?)req.VendorFeedbackRating ?? DBNull.Value;
                cmdVf.Parameters.Add("VENDOR_BY", OracleDbType.Varchar2).Value = req.EnteredByCode;
                cmdVf.Parameters.Add("ATT", OracleDbType.NVarchar2).Value =
                    string.IsNullOrWhiteSpace(req.VendorFeedbackAttachmentRelPath) ? DBNull.Value : req.VendorFeedbackAttachmentRelPath;
                cmdVf.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = req.RefNo;
                await cmdVf.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return new SaveActionResult(true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "SaveActionAsync failed for {RefNo}", req.RefNo);
            return new SaveActionResult(false, UserFacingError.Generic);
        }
    }

    private static async Task<bool> IsAlreadyAssignedToVendorAsync(OracleConnection cn, OracleTransaction tx, string refNo)
    {
        await using var cmd = new OracleCommand(@"
            SELECT COUNT(1)
            FROM FLATS.FLAT_GRIEVANCE_ACTION
            WHERE REF_NO = :REF_NO
              AND UPPER(ACTION_TAKEN) = 'ASSIGNED TO VENDOR'", cn)
        { Transaction = tx, BindByName = true };
        cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<AssignmentEmailInfo?> GetAssignmentEmailInfoAsync(string createdByPfNo)
    {
        const string sql = @"
            SELECT RM.RH_NAME, RM.RH_EMAIL, SD.EMAIL_ID, SD.NAME
            FROM ORGANISATION.STAFF_DETAILS SD
            LEFT JOIN FLATS.REGION_MASTER RM ON RM.REGION_CODE = SD.REGION_CODE
            WHERE SD.PF_NO = :PFNO";

        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("PFNO", OracleDbType.Varchar2).Value = createdByPfNo;
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new AssignmentEmailInfo(
            RoHeadName: r.IsDBNull(0) ? null : r.GetString(0),
            RoHeadEmail: r.IsDBNull(1) ? null : r.GetString(1),
            UserEmail: r.IsDBNull(2) ? null : r.GetString(2),
            UserName: r.IsDBNull(3) ? null : r.GetString(3));
    }

    public async Task NotifyVendorAssignedAsync(string refNo, string enteredByCode, GrievanceDetailsInfo grievance, VendorInfo vendor, string? expectedVendorVisitDate)
    {
        try
        {
            var info = string.IsNullOrWhiteSpace(grievance.CreatedByCode)
                ? null
                : await GetAssignmentEmailInfoAsync(grievance.CreatedByCode);

            using var mail = new MailMessage { From = new MailAddress(HelpdeskEmail), Subject = "Complaint Assigned – Request to Acknowledge and Schedule Visit", IsBodyHtml = true, BodyEncoding = Encoding.UTF8 };
            AddIfValid(mail.To, vendor.VendorEmail, vendor.VendorName ?? "Vendor");
            AddIfValid(mail.To, HelpdeskEmail, "Maintenance Helpdesk");
            AddIfValid(mail.CC, info?.UserEmail, info?.UserName ?? "User");
            mail.Body = BuildVendorEmailBody(refNo, grievance, vendor, expectedVendorVisitDate, info);

            await AttachGrievanceFilesAsync(grievance.AttachmentPath, mail);

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
            await MarkEmailSentAsync(refNo, enteredByCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send vendor-assigned notification for {RefNo}", refNo);
        }
    }

    public async Task NotifyUserStatusUpdateAsync(string refNo, string enteredByCode, GrievanceDetailsInfo grievance, AllocatedFlatDetails? flat, string actionTaken)
    {
        try
        {
            var info = string.IsNullOrWhiteSpace(grievance.CreatedByCode)
                ? null
                : await GetAssignmentEmailInfoAsync(grievance.CreatedByCode);

            if (info is null || string.IsNullOrWhiteSpace(info.UserEmail))
            {
                _logger.LogWarning("NotifyUserStatusUpdateAsync: no email on file for creator of {RefNo}", refNo);
                return;
            }

            using var mail = new MailMessage { From = new MailAddress(HelpdeskEmail), Subject = "Service Request Update | Ref No: " + refNo, IsBodyHtml = true, BodyEncoding = Encoding.UTF8 };
            AddIfValid(mail.To, HelpdeskEmail, "Maintenance Helpdesk");
            AddIfValid(mail.CC, info.UserEmail, grievance.CreatedByName ?? info.UserName);
            AddIfValid(mail.ReplyToList, HelpdeskEmail, "Maintenance Helpdesk");
            mail.Body = BuildUserEmailBody(refNo, grievance, flat, actionTaken);

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send status-update notification for {RefNo}", refNo);
        }
    }

    private static string BuildUserEmailBody(string refNo, GrievanceDetailsInfo g, AllocatedFlatDetails? flat, string actionTaken)
    {
        var sb = new StringBuilder();
        sb.Append("<div style='background-color:#f4f4f4;padding:20px;font-family:\"Segoe UI\",Tahoma,Geneva,Verdana,sans-serif;color:#333;'>");
        sb.Append("<div style='max-width:600px;margin:0 auto;background:#fff;border-radius:8px;overflow:hidden;border:1px solid #e0e0e0;box-shadow:0 2px 4px rgba(0,0,0,0.1);'>");
        sb.Append("<div style='padding:30px;'>");
        sb.Append("<p style='font-size:16px;'>Dear Sir/Madam,</p>");
        sb.Append("<p style='line-height:1.5;'>This is to inform you that your complaint has been updated with the following action:</p>");
        sb.Append("<p style='font-size:15px;font-weight:bold;color:#003366;'>").Append(WebUtility.HtmlEncode(actionTaken)).Append("</p>");
        sb.Append("<table style='width:100%;border-collapse:collapse;margin-top:20px;border:1px solid #f0f0f0;'>");
        AppendRow(sb, "Reference No", refNo, true);
        AppendRow(sb, "Subject", g.Subject, false);
        AppendRow(sb, "Category", g.Category, false);
        AppendRow(sb, "Description", g.Description, false);
        AppendRow(sb, "Flat No", g.FlatNo, false);
        AppendRow(sb, "Building", flat?.BuildingName, false);
        AppendRow(sb, "Address", flat?.Address, false);
        AppendRow(sb, "Raised By", g.CreatedByName, false);
        AppendRow(sb, "Status", actionTaken, false);
        AppendRow(sb, "Status Date", AppTime.Now.ToString("dd/MM/yyyy"), false);
        sb.Append("</table>");
        sb.Append("<hr style='border:0;border-top:1px solid #eeeeee;margin:25px 0;'>");
        sb.Append("<p style='margin:0;font-weight:bold;color:#003366;'>Regards,</p>");
        sb.Append("<p style='margin:4px 0;font-size:14px;'>Team Maintenance<br/>Union Bank of India</p>");
        sb.Append("<p style='margin:4px 0;font-size:12px;color:#0055aa;'>").Append(WebUtility.HtmlEncode(HelpdeskEmail)).Append("</p>");
        sb.Append("</div></div></div>");
        return sb.ToString();
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

    private async Task AttachGrievanceFilesAsync(string? attachmentPathField, MailMessage mail)
    {
        if (string.IsNullOrWhiteSpace(attachmentPathField)) return;

        var parts = attachmentPathField.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
        const long maxSingle = 15L * 1024 * 1024;
        long total = 0;
        foreach (var raw in parts)
        {
            var value = raw.Trim();
            try
            {
                if (value.StartsWith("/vendor/attachments/", StringComparison.OrdinalIgnoreCase))
                {
                    var idPart = value[value.LastIndexOf('/')..].TrimStart('/');
                    if (!int.TryParse(idPart, out var attachmentId)) continue;

                    var stored = await GetAttachmentAsync(attachmentId);
                    if (stored is null || stored.Data.Length == 0) continue;
                    if (stored.Data.Length > maxSingle || total + stored.Data.Length > 25L * 1024 * 1024) continue;

                    mail.Attachments.Add(new Attachment(new MemoryStream(stored.Data), stored.FileName, stored.ContentType));
                    total += stored.Data.Length;
                }
                else
                {
                    var relative = value.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
                    var fullPath = Path.Combine(_env.WebRootPath, relative);
                    if (!File.Exists(fullPath)) continue;

                    var size = new FileInfo(fullPath).Length;
                    if (size <= 0 || size > maxSingle || total + size > 25L * 1024 * 1024) continue;

                    mail.Attachments.Add(new Attachment(fullPath));
                    total += size;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not attach file {Path}", raw);
            }
        }
    }

    public async Task<StoredAttachmentRef> SaveAttachmentAsync(string refNo, string category, string fileName, string contentType, byte[] data, string? uploadedBy)
    {
        const string sql = @"
            INSERT INTO FLATS.GRIEVANCE_ATTACHMENTS
                (REF_NO, CATEGORY, FILE_NAME, CONTENT_TYPE, FILE_SIZE, FILE_DATA, UPLOADED_BY, UPLOADED_ON)
            VALUES
                (:REF_NO, :CATEGORY, :FILE_NAME, :CONTENT_TYPE, :FILE_SIZE, :FILE_DATA, :UPLOADED_BY, SYSDATE)
            RETURNING ATTACHMENT_ID INTO :NEW_ID";

        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("REF_NO", OracleDbType.Varchar2).Value = refNo;
        cmd.Parameters.Add("CATEGORY", OracleDbType.Varchar2).Value = category;
        cmd.Parameters.Add("FILE_NAME", OracleDbType.Varchar2).Value = fileName;
        cmd.Parameters.Add("CONTENT_TYPE", OracleDbType.Varchar2).Value = contentType;
        cmd.Parameters.Add("FILE_SIZE", OracleDbType.Int32).Value = data.Length;
        cmd.Parameters.Add("FILE_DATA", OracleDbType.Blob).Value = data;
        cmd.Parameters.Add("UPLOADED_BY", OracleDbType.Varchar2).Value = (object?)uploadedBy ?? DBNull.Value;
        var idParam = new OracleParameter("NEW_ID", OracleDbType.Int32) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(idParam);

        await cn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();

        var newId = ((OracleDecimal)idParam.Value).ToInt32();
        return new StoredAttachmentRef(newId, $"/vendor/attachments/{newId}");
    }

    public async Task<StoredAttachment?> GetAttachmentAsync(int attachmentId)
    {
        const string sql = "SELECT FILE_DATA, CONTENT_TYPE, FILE_NAME FROM FLATS.GRIEVANCE_ATTACHMENTS WHERE ATTACHMENT_ID = :ID";

        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("ID", OracleDbType.Int32).Value = attachmentId;
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var data = r.IsDBNull(0) ? Array.Empty<byte>() : (byte[])r["FILE_DATA"];
        var contentType = r.IsDBNull(1) ? "application/octet-stream" : r.GetString(1);
        var fileName = r.IsDBNull(2) ? $"attachment_{attachmentId}" : r.GetString(2);
        return new StoredAttachment(data, contentType, fileName);
    }

    public async Task<bool> IsAttachmentAccessibleToVendorAsync(int attachmentId, string vendorMobile)
    {
        if (string.IsNullOrWhiteSpace(vendorMobile)) return false;

        const string sql = @"
            SELECT COUNT(*)
            FROM FLATS.GRIEVANCE_ATTACHMENTS ga
            JOIN FLATS.FLAT_GRIEVANCE g ON g.REF_NO = ga.REF_NO
            WHERE ga.ATTACHMENT_ID = :ID AND g.VENDOR_MOBILE = :MOBILE";

        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("ID", OracleDbType.Int32).Value = attachmentId;
        cmd.Parameters.Add("MOBILE", OracleDbType.Varchar2).Value = vendorMobile.Trim();
        await cn.OpenAsync();
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<bool> IsAttachmentAccessibleToStaffAsync(int attachmentId, string empCode)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return false;

        const string sql = @"
            SELECT COUNT(*)
            FROM FLATS.GRIEVANCE_ATTACHMENTS ga
            JOIN FLATS.FLAT_GRIEVANCE g ON g.REF_NO = ga.REF_NO
            WHERE ga.ATTACHMENT_ID = :ID AND g.CREATED_BY = :EMP";

        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("ID", OracleDbType.Int32).Value = attachmentId;
        cmd.Parameters.Add("EMP", OracleDbType.Varchar2).Value = empCode.Trim();
        await cn.OpenAsync();
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<AttachmentMigrationResult> MigrateFolderAttachmentsToDbAsync(string uploadedBy)
    {
        var results = new[]
        {
            await MigrateColumnAsync("FLATS.FLAT_GRIEVANCE", "REF_NO", "ATTACHMENT_PATH", "GRIEVANCE", uploadedBy),
            await MigrateColumnAsync("FLATS.FLAT_GRIEVANCE", "REF_NO", "FEEDBACK_ATTACHMENT_PATH", "FEEDBACK", uploadedBy),
            await MigrateColumnAsync("FLATS.FLAT_GRIEVANCE", "REF_NO", "FEEDBACK_ATTACHMENT_BY_VENDOR", "VENDOR_FEEDBACK", uploadedBy),
            await MigrateColumnAsync("FLATS.GRIEVANCE_ACTION_DESC", "SR_NO", "ATTACHMENT_PATH", "ACTION_ATTACHMENT", uploadedBy),
        };

        return new AttachmentMigrationResult(
            results.Sum(x => x.Migrated),
            results.Sum(x => x.Skipped),
            results.SelectMany(x => x.Errors).ToList());
    }

    private async Task<(int Migrated, int Skipped, List<string> Errors)> MigrateColumnAsync(
        string table, string keyColumn, string pathColumn, string category, string uploadedBy)
    {
        var migrated = 0;
        var skipped = 0;
        var errors = new List<string>();

        var rows = new List<(string Key, string? RefNo, string Path)>();
        await using (var cn = new OracleConnection(_connString))
        {
            await cn.OpenAsync();
            var selectSql = $@"
                SELECT {keyColumn}, REF_NO, {pathColumn}
                FROM {table}
                WHERE {pathColumn} IS NOT NULL
                  AND {pathColumn} NOT LIKE '/vendor/attachments/%'";
            await using var cmd = new OracleCommand(selectSql, cn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                rows.Add((r.GetValue(0).ToString()!, r.IsDBNull(1) ? null : r.GetString(1), r.GetString(2)));
            }
        }

        foreach (var row in rows)
        {
            try
            {
                var relative = row.Path.Trim().TrimStart('~').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(_env.WebRootPath, relative);
                if (!File.Exists(fullPath))
                {
                    skipped++;
                    errors.Add($"{table}.{pathColumn} ({row.Key}): file not found at {row.Path}");
                    continue;
                }

                var bytes = await File.ReadAllBytesAsync(fullPath);
                var fileName = Path.GetFileName(fullPath);
                var contentType = GuessContentType(fileName);
                var stored = await SaveAttachmentAsync(row.RefNo ?? "", category, fileName, contentType, bytes, uploadedBy);

                await using var cn2 = new OracleConnection(_connString);
                await cn2.OpenAsync();
                var updateSql = $"UPDATE {table} SET {pathColumn} = :NEWPATH WHERE {keyColumn} = :KEY";
                await using var updCmd = new OracleCommand(updateSql, cn2) { BindByName = true };
                updCmd.Parameters.Add("NEWPATH", OracleDbType.Varchar2).Value = stored.DownloadPath;
                if (keyColumn == "SR_NO")
                    updCmd.Parameters.Add("KEY", OracleDbType.Int32).Value = int.Parse(row.Key);
                else
                    updCmd.Parameters.Add("KEY", OracleDbType.Varchar2).Value = row.Key;
                await updCmd.ExecuteNonQueryAsync();

                migrated++;
            }
            catch (Exception ex)
            {
                skipped++;
                _logger.LogError(ex, "Attachment migration failed for {Table}.{PathColumn} key {Key}", table, pathColumn, row.Key);
                errors.Add($"{table}.{pathColumn} ({row.Key}): could not be migrated. See application logs for details.");
            }
        }

        return (migrated, skipped, errors);
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream",
        };
    }

    public async Task<GrievanceListResult> GetVendorGrievancesAsync(string vendorMobile, string? building, string? flatNo, int pageIndex, int pageSize)
    {
        var where = new StringBuilder(@"
            FROM FLATS.FLAT_GRIEVANCE g
            JOIN FLATS.VW_FLAT_DETAILS a ON g.CREATED_BY = a.PF_NO
            JOIN FLATS.GRIEVANCE_ACTION_DESC d ON d.REF_NO = g.REF_NO AND d.ACTION_TAKEN = 'Assigned to Vendor'
            WHERE g.VENDOR_MOBILE = :VENDOR_MOBILE");

        if (!string.IsNullOrWhiteSpace(building))
            where.Append(" AND a.BUILDING_NAME = :BUILDING");
        if (!string.IsNullOrWhiteSpace(flatNo))
            where.Append(" AND a.FLAT_NO LIKE :FLAT_NO");

        void BindFilters(OracleCommand c)
        {
            c.Parameters.Add("VENDOR_MOBILE", OracleDbType.Varchar2).Value = vendorMobile.Trim();
            if (!string.IsNullOrWhiteSpace(building))
                c.Parameters.Add("BUILDING", OracleDbType.Varchar2).Value = building.Trim();
            if (!string.IsNullOrWhiteSpace(flatNo))
                c.Parameters.Add("FLAT_NO", OracleDbType.Varchar2).Value = "%" + flatNo.Trim() + "%";
        }

        await using var cn = new OracleConnection(_connString);
        await cn.OpenAsync();

        int totalCount;
        await using (var countCmd = new OracleCommand("SELECT COUNT(*) " + where, cn) { BindByName = true })
        {
            BindFilters(countCmd);
            totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        }

        var rows = new List<GrievanceListRow>();
        var listSql = @"
            SELECT g.REF_NO, a.BUILDING_NAME, g.FLAT_NO, g.CATEGORY, g.STATUS, d.ACTION_DATE, a.ADDRESS "
            + where + @"
            ORDER BY g.STATUS_DATE DESC
            OFFSET :OFFSET_ROWS ROWS FETCH NEXT :PAGE_SIZE ROWS ONLY";

        await using (var listCmd = new OracleCommand(listSql, cn) { BindByName = true })
        {
            BindFilters(listCmd);
            listCmd.Parameters.Add("OFFSET_ROWS", OracleDbType.Int32).Value = Math.Max(0, pageIndex) * pageSize;
            listCmd.Parameters.Add("PAGE_SIZE", OracleDbType.Int32).Value = pageSize;

            await using var r = await listCmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                rows.Add(new GrievanceListRow(
                    RefNo: r.GetString(0),
                    BuildingName: r.IsDBNull(1) ? null : r.GetString(1),
                    FlatNo: r.IsDBNull(2) ? null : r.GetString(2),
                    Category: r.IsDBNull(3) ? null : r.GetString(3),
                    Status: r.IsDBNull(4) ? null : r.GetString(4),
                    ActionDate: r.IsDBNull(5) ? null : r.GetDateTime(5),
                    Address: r.IsDBNull(6) ? null : r.GetString(6)));
            }
        }

        return new GrievanceListResult(rows, totalCount);
    }

    public async Task<List<string>> GetVendorGrievanceBuildingsAsync(string vendorMobile)
    {
        const string sql = @"
            SELECT DISTINCT a.BUILDING_NAME
            FROM FLATS.FLAT_GRIEVANCE g
            JOIN FLATS.VW_FLAT_DETAILS a ON g.CREATED_BY = a.PF_NO
            WHERE g.VENDOR_MOBILE = :VENDOR_MOBILE
              AND a.BUILDING_NAME IS NOT NULL
            ORDER BY a.BUILDING_NAME ";

        var result = new List<string>();
        await using var cn = new OracleConnection(_connString);
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("VENDOR_MOBILE", OracleDbType.Varchar2).Value = vendorMobile.Trim();
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(r.GetString(0));
        }
        return result;
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

    private static string BuildVendorEmailBody(string refNo, GrievanceDetailsInfo g, VendorInfo v, string? expectedVisitDate, AssignmentEmailInfo? info)
    {
        var sb = new StringBuilder();
        sb.Append("<div style='background-color:#f4f4f4;padding:20px;font-family:\"Segoe UI\",Tahoma,Geneva,Verdana,sans-serif;color:#333;'>");
        sb.Append("<div style='max-width:600px;margin:0 auto;background:#fff;border-radius:8px;overflow:hidden;border:1px solid #e0e0e0;box-shadow:0 2px 4px rgba(0,0,0,0.1);'>");
        sb.Append("<div style='padding:30px;'>");
        sb.Append("<p style='font-size:16px;'>Dear Sir/Madam,</p>");
        sb.Append("<p style='line-height:1.5;'>The following complaint has been assigned to your end for necessary action.</p>");
        sb.Append("<p style='line-height:1.5;'><strong>Kindly acknowledge and schedule the site visit / quotation as per timelines.</strong></p>");
        sb.Append("<table style='width:100%;border-collapse:collapse;margin-top:20px;border:1px solid #f0f0f0;'>");
        AppendRow(sb, "Reference No", refNo, true);
        AppendRow(sb, "Subject", g.Subject, false);
        AppendRow(sb, "Category", g.Category, false);
        AppendRow(sb, "Description", g.Description, false);
        AppendRow(sb, "Flat No", g.FlatNo, false);
        AppendRow(sb, "Expected Vendor Visit Date", expectedVisitDate, false);
        sb.Append("</table>");
        sb.Append("<h4 style='margin-top:25px;color:#003366;'>Vendor Details</h4>");
        sb.Append("<table style='width:100%;border-collapse:collapse;margin-top:10px;border:1px solid #f0f0f0;'>");
        AppendRow(sb, "Vendor Name", v.VendorName, false);
        AppendRow(sb, "Vendor Email", v.VendorEmail, false);
        AppendRow(sb, "Vendor Mobile", v.VendorMobile, false);
        sb.Append("</table>");
        sb.Append("<h4 style='margin-top:25px;color:#003366;'>SPOC Details</h4>");
        sb.Append("<table style='width:100%;border-collapse:collapse;margin-top:10px;border:1px solid #f0f0f0;'>");
        AppendRow(sb, "SPOC Name", v.SpocName, false);
        AppendRow(sb, "SPOC Mobile", v.SpocMobile, false);
        AppendRow(sb, "SPOC Email", v.SpocEmail, false);
        sb.Append("</table>");
        sb.Append("<p style='line-height:1.5;margin-top:20px;'>You are requested to take necessary action and update the status accordingly.</p>");
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

    private static string BuildActionDetails(SaveActionRequest r)
    {
        string FmtDate(DateTime? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "-";
        string FmtMoney(decimal? m) => m.HasValue ? m.Value.ToString("N2", CultureInfo.InvariantCulture) : "-";

        var sb = new StringBuilder();
        switch (r.ActionTaken)
        {
            case "Call For Quotation":
                sb.AppendLine("Quotation Request Date: " + FmtDate(r.QuotationRequestDate));
                sb.AppendLine("Last Submission Date: " + FmtDate(r.LastSubmissionDate));
                break;
            case "Assigned to Vendor":
                sb.AppendLine("Selected Vendor: " + (r.VendorName ?? "-"));
                sb.AppendLine();
                sb.AppendLine("Contact Person Details:");
                sb.AppendLine("Name: " + (string.IsNullOrWhiteSpace(r.VendorSpocName) ? "-" : r.VendorSpocName));
                sb.AppendLine("Phone: " + (string.IsNullOrWhiteSpace(r.VendorSpocContact) ? "-" : r.VendorSpocContact));
                sb.AppendLine("Email: " + (string.IsNullOrWhiteSpace(r.VendorSpocEmail) ? "-" : r.VendorSpocEmail));
                sb.AppendLine();
                sb.AppendLine("Expected Vendor Visit Date: " + FmtDate(r.ExpectedVendorVisitDate));
                break;
            case "On site Vendor Visit":
                sb.AppendLine("Vendor Visit Date: " + FmtDate(r.VendorVisitDate));
                break;
            case "Submit Quotation":
                sb.AppendLine("Quotation Date: " + FmtDate(r.QuotationDate));
                sb.AppendLine("Quotation Amount: " + FmtMoney(r.QuotationAmt));
                break;
            case "Quotation Approval":
                sb.AppendLine("Quotation Approval Date: " + FmtDate(r.QuotationApprovalDate));
                sb.AppendLine("Quotation Approval Amount: " + FmtMoney(r.QuotationApprovalAmt));
                sb.AppendLine("Quotation Approval Ref No: " + (r.QuotationApprovalRefNo ?? "-"));
                sb.AppendLine("Quotation Approved By: " + (r.QuotationApprovalBy ?? "-"));
                break;
            case "Payment Completed":
                sb.AppendLine("Payment Date: " + FmtDate(r.PaymentDate));
                sb.AppendLine("Payment Amount: " + FmtMoney(r.PaymentAmt));
                break;
            case "Work Started":
                sb.AppendLine("Work Started Date: " + FmtDate(r.WorkStartedDate));
                break;
            case "Work in Progress":
                sb.AppendLine("Work In-Progress Date: " + FmtDate(r.WorkInProgressDate));
                break;
            case "Work Completed":
                sb.AppendLine("Work Completed Date: " + FmtDate(r.WorkCompletedDate));
                sb.AppendLine("Products: " + string.Join("; ", r.Products.Select(p => $"{p.ProductName} x{p.Qty}")));
                break;
            case "Invoice Submitted":
                sb.AppendLine("Invoice No: " + (r.InvoiceNo ?? "-"));
                sb.AppendLine("Invoice Date: " + FmtDate(r.InvoiceDate));
                sb.AppendLine("Invoice Amount: " + FmtMoney(r.InvoiceAmt));
                break;
            default:
                sb.AppendLine("No additional details captured for this action.");
                break;
        }
        return sb.ToString();
    }
}