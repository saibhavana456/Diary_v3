using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IAdminUserDetailService
{
    Task<AdminUserFormData?> GetAdminByEmpIdAsync(string empId);
    Task<AdminUserFormData?> GetStaffByPfAsync(string pfNo);
    Task<AdminUserSaveResult> SaveAdminAsync(AdminUserSaveRequest req);
}
