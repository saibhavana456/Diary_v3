namespace MyDiary.Web.Features.FlatManagement.Models.DTO;

public record FlatBillRow(
    string? BillTypeName,
    string? ProviderName,
    string? BillMonthToShow,
    string? BillAmount,
    string? BillCycleEndDate,
    string? PaidAmount,
    string? BillGenerationDate,
    string? PaymentDate,
    string? Remark,
    string? BillAddDate);

public record AddBillRequest(
    string FlatId,
    string? FlatNo,
    string BillTypeCode,
    string BillTypeName,
    string ProviderName,
    string BillMonth,
    string BillMonthToShow,
    string BillGenerationDate,
    string? BillCycleEndDate,
    string? BillNo,
    string BillAmount,
    string PaidAmount,
    string? PaymentDate,
    string? Remark,
    string EnteredBy);

public record AddBillResult(bool Success, string? Error);
