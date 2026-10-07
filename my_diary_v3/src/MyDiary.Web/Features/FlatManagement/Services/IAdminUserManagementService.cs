using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IAdminUserManagementService
{
    Task<AdminUserListResult> GetUsersAsync(string? empPfOrName, string? location, int pageIndex, int pageSize);
    Task<AdminVendorListResult> GetVendorsAsync(string? vendorCodeOrName, string? location, int pageIndex, int pageSize);
}
