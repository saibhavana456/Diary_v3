using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IAdminGrievanceService
{
    Task<List<string>> GetLocationsAsync();
    Task<List<string>> GetFlatTypesAsync(string? location);

    Task<AdminGrievanceListResult> GetGrievancesAsync(
        string adminPfNo, string? location, string? flatType, string? empPf, string? flatNo, int pageIndex, int pageSize);
}
