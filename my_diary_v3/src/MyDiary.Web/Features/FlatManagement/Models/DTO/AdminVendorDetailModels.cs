namespace MyDiary.Web.Features.FlatManagement.Models.DTO;


public record AdminVendorFormData(
    string? VendorId,
    string? VendorName,
    string? VendorMobile,
    string? VendorAddress,
    string? VendorEmail,
    string? VendorGst,
    string? SpocName,
    string? SpocMobile,
    string? SpocEmail,
    string? Status);

public record AdminVendorSaveRequest(
    string? VendorId,
    string VendorName,
    string VendorMobile,
    string? VendorAddress,
    string? VendorGst,
    string VendorEmail,
    string SpocName,
    string SpocMobile,
    string SpocEmail,
    string Status,
    string EnteredBy);

public record AdminVendorSaveResult(bool Success, string? Error);
