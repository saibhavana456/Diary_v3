using MyDiary.Web.Features.BirthdayRetirement.Models;

namespace MyDiary.Web.Features.BirthdayRetirement.Services
{
    public interface IBirthdayRetirementService
    {
        Task<IEnumerable<StaffEventDto>> GetStaffEventsAsync(string zone, string region, string branch, string eventType);

        // New methods for cascading dropdowns
        Task<IEnumerable<LocationDropdownDto>> GetZonesAsync();
        Task<IEnumerable<LocationDropdownDto>> GetRegionsAsync(string zoneCode);
        Task<IEnumerable<LocationDropdownDto>> GetBranchesAsync(string regionCode);
    }
}