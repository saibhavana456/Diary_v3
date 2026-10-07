namespace MyDiary.Web.Features.FlatManagement.Models.DTO;


public record AdminUserRow(
    string EmpId,
    string? Name,
    string? UserType,
    string? ScaleDesc,
    string? JobDesc,
    string? LocationDesc,
    string? ContactNo,
    string? EmailId);

public record AdminUserListResult(IReadOnlyList<AdminUserRow> Rows, int TotalCount);

public record AdminVendorRow(
    string VendorId,
    string? VendorCode,
    string? VendorName,
    string? VendorAddress,
    string? VendorMobile,
    string? VendorEmail,
    string? Status,
    DateTime? CreatedOn);

public record AdminVendorListResult(IReadOnlyList<AdminVendorRow> Rows, int TotalCount);
