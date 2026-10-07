using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IAdminFlatMasterService
{
    Task<List<FlatLocationOption>> GetLocationsAsync();
    Task<List<FlatSocietyOption>> GetSocietiesAsync(string locationId);
    Task<List<JurisdictionOption>> GetJurisdictionOptionsAsync();
    Task<OwnedUsageDefaults?> GetOwnedUsageDefaultsAsync(string societyId);
    Task<bool> FlatExistsAsync(string flatNo, string societyId, string locationId);
    Task<AddFlatResult> AddFlatAsync(AddFlatRequest req);

    Task<FlatMasterListResult> GetFlatsAsync(string adminPfNo, string? search, int pageIndex, int pageSize);

    // Edit_Flat.aspx: separate lookup source (Society_Details_Master) from Add_Flat's dropdowns.
    Task<List<FlatLocationOption>> GetEditLocationsAsync();
    Task<List<FlatSocietyOption>> GetEditSocietiesAsync(string locationId);
    Task<EditFlatDetails?> GetFlatForEditAsync(string flatId);
    Task<bool> FlatExistsExcludingAsync(string flatNo, string societyId, string locationId, string excludeFlatId);
    Task<AddFlatResult> UpdateFlatAsync(EditFlatRequest req);
}
