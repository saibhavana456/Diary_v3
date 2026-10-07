namespace MyDiary.Web.Features.FlatManagement.Models.DTO;

public record FlatAllocationInfo(string? EmplId, string? Building, string? RoomNbr);

public record FlatDetailsInfo(string? FlatNo, string? Type, string? Area, string? Address, string? Location);

public record GrievanceDetailsInfo(
    string RefNo,
    int ActionId,
    string? FlatNo,
    string? FlatId,
    string? Category,
    string? Subject,
    string? Status,
    string? Description,
    string? AttachmentPath,
    string? CreatedByCode,
    string? CreatedByName,
    string? CreatedOn,
    // User-submitted post-completion feedback (FLATS.FLAT_GRIEVANCE.COMPLETED_YN/RATING/
    // FEEDBACK/FEEDBACK_ATTACHMENT_PATH/FEEDBACK_DATE) -- ported for TrackGrievance_View.aspx.
    string? CompletedYn = null,
    string? Rating = null,
    string? Feedback = null,
    string? FeedbackAttachmentPath = null,
    DateTime? FeedbackDate = null);

public record VendorOption(string VendorId, string VendorName);

public record VendorInfo(
    string? VendorCode,
    string? VendorName,
    string? VendorEmail,
    string? VendorMobile,
    string? SpocName,
    string? SpocEmail,
    string? SpocMobile);

public record ProductRow(string ProductName, decimal Qty, string? Description);

public record SaveActionRequest(
    int ActionId,
    string RefNo,
    string ActionTaken,
    string Remarks,
    string EnteredByCode,
    string? AttachmentRelPath,
    DateTime? ExpectedVendorVisitDate,
    DateTime? VendorVisitDate,
    DateTime? QuotationDate,
    decimal? QuotationAmt,
    DateTime? QuotationApprovalDate,
    decimal? QuotationApprovalAmt,
    string? QuotationApprovalRefNo,
    string? QuotationApprovalBy,
    DateTime? WorkStartedDate,
    DateTime? WorkInProgressDate,
    DateTime? WorkCompletedDate,
    string? InvoiceNo,
    DateTime? InvoiceDate,
    decimal? InvoiceAmt,
    DateTime? PaymentDate,
    decimal? PaymentAmt,
    IReadOnlyList<ProductRow> Products,
    string? VendorFeedback,
    int? VendorFeedbackRating,
    string? VendorFeedbackAttachmentRelPath,
    // Admin-only fields (AdminGrievanceAction.aspx). Optional so the existing vendor-portal
    // callers of this same request/service don't need to change.
    DateTime? QuotationRequestDate = null,
    DateTime? LastSubmissionDate = null,
    string? VendorId = null,
    string? VendorName = null,
    string? VendorMobile = null,
    string? VendorSpocName = null,
    string? VendorSpocEmail = null,
    string? VendorSpocContact = null);

public record SaveActionResult(bool Success, string? Error);

public record AssignmentEmailInfo(string? RoHeadName, string? RoHeadEmail, string? UserEmail, string? UserName);

public record StoredAttachmentRef(int AttachmentId, string DownloadPath);

public record StoredAttachment(byte[] Data, string ContentType, string FileName);

public record AttachmentMigrationResult(int Migrated, int Skipped, IReadOnlyList<string> Errors);

public record GrievanceListRow(
    string RefNo,
    string? BuildingName,
    string? FlatNo,
    string? Category,
    string? Status,
    DateTime? ActionDate,
    string? Address);

public record GrievanceListResult(IReadOnlyList<GrievanceListRow> Rows, int TotalCount);

// Ports TrackGrievance_View.aspx.cs's BindActionHistory (FLAT_GRIEVANCE_ACTION joined to
// GRIEVANCE_ACTION_DESC by ACTION_ID).
public record ActionHistoryRow(
    int ActionId,
    string RefNo,
    string? ActionTaken,
    string? ActionDetails,
    string? Remarks,
    string? AttachmentPath,
    DateTime? ActionDate);
