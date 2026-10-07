namespace MyDiary.Web.Features.FlatManagement.Models.DTO;
public record GrievanceFlatContext(
    string? FlatNo,
    string? BuildingName,
    string? Address,
    string? FlatType,
    string? CarpetArea,
    string? UserMail);

public record SubmitGrievanceRequest(
    string PfNo,
    string CreatedByName,
    string Category,
    string Subject,
    string Description,
    string? AttachmentFileName,
    string? AttachmentContentType,
    byte[]? AttachmentData);

public record SubmitGrievanceResult(bool Success, string? RefNo, string? Error);

public record TrackGrievanceRow(
    string RefNo,
    string? FlatId,
    string? Category,
    string? Subject,
    string? Status,
    DateTime? CreatedOn,
    string? CreatedBy);

public record SubmitFeedbackRequest(
    string RefNo,
    string PfNo,
    bool WorkCompleted,
    int? Rating,
    string Feedback,
    string? AttachmentFileName,
    string? AttachmentContentType,
    byte[]? AttachmentData);

public record SubmitFeedbackResult(bool Success, string? Error);