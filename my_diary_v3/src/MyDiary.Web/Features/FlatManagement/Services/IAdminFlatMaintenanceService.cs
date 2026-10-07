using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IAdminFlatMaintenanceService
{
    Task<AddMaintenanceResult> AddMaintenanceAsync(AddMaintenanceRequest req);
    Task<List<FlatMaintenanceRow>> GetMaintenanceHistoryAsync(string flatId);
    Task<bool> DeleteMaintenanceAsync(DeleteMaintenanceRequest req);
}
