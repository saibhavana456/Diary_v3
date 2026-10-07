using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IAdminFlatService
{
    Task<List<AdminFlatRow>> GetAllFlatsAsync(string adminPfNo, string? search);
}
