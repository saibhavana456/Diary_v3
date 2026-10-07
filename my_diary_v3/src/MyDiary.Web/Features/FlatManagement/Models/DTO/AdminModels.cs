namespace MyDiary.Web.Features.FlatManagement.Models.DTO;

public record AdminFlatRow(
    string? EmplId,
    string? UbiCenter,
    string? RoomNbr,
    DateTime? PurchasedDate,
    string? RoomType,
    string? UbiRegionOfc);

public record AdminGrievanceRow(
    string RefNo,
    string? FlatId,
    string? FlatNo,
    string? Location,
    string? Category,
    string? Status,
    DateTime? CreatedOn,
    string? CreatedByDisplay);

public record AdminGrievanceListResult(IReadOnlyList<AdminGrievanceRow> Rows, int TotalCount);
