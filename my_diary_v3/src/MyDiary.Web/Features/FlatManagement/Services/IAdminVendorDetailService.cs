using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IAdminVendorDetailService
{
    Task<AdminVendorFormData?> GetVendorByIdAsync(string vendorId);
    Task<AdminVendorSaveResult> SaveVendorAsync(AdminVendorSaveRequest req);
}
