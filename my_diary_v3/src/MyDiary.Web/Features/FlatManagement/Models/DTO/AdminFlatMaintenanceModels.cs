namespace MyDiary.Web.Features.FlatManagement.Models.DTO;

public record FlatMaintenanceRow(
    string? MaintenanceTypeName,
    string? VendorName,
    string? MaintenanceDate,
    string? BillNo,
    string? BillDate,
    string? BillAmount,
    string? Remark);

public record AddMaintenanceRequest(
    string FlatId,
    string? FlatNo,
    string MaintenanceTypeName,
    string? MaintenanceDate,
    string VendorName,
    string BillDate,
    string BillNo,
    string BillAmount,
    string? Remark,
    string EnteredBy);

public record AddMaintenanceResult(bool Success, string? Error);

public record DeleteMaintenanceRequest(
    string FlatId,
    string? MaintenanceTypeName,
    string? VendorName,
    string? MaintenanceDate,
    string? BillNo,
    string? BillDate,
    string? BillAmount,
    string? Remark);
