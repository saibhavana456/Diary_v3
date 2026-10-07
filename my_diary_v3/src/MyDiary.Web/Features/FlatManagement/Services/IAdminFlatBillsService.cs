using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IAdminFlatBillsService
{
    Task<bool> CheckExistingBillAsync(string flatId, string billTypeCode, string billMonth);
    Task<AddBillResult> AddBillAsync(AddBillRequest req);
    Task<List<FlatBillRow>> GetBillHistoryAsync(string flatId);
}
