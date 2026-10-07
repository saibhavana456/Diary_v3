using MyDiary.Web.Features.Focus360.Models;
using System.Threading.Tasks;

namespace MyDiary.Web.Features.Focus360.Services;

public interface IGapService
{
    Task<List<GapBranchListItem>> GetBranchesAsync(string? search = null);

    Task<List<GapZoneListItem>> GetZonesAsync(string? search = null);

    Task<List<GapRegionListItem>> GetRegionsAsync(string? search = null);

    /// <summary>Get regions filtered by zone solid code (for cascading dropdowns).</summary>
    Task<List<GapRegionListItem>> GetRegionsByZoneAsync(string zoneSolid);

    /// <summary>Get branches filtered by region solid code (for cascading dropdowns).</summary>
    Task<List<GapBranchListItem>> GetBranchesByRegionAsync(string regionSolid);

    Task<List<GapBranchSummary?>> GetBranchSummaryAsync(string branchId, DateTime? snapshotDate = null);

	Task<List<GapStaffDetail>> GetStaffDetailsAsync(string branchId, DateTime? snapshotDate = null, bool isCOUser = false);
    Task<(int TotalStaff, decimal PerEmployeeBusiness)> GetStaffTotalsAsync(string branchId, DateTime? snapshotDate = null, bool isCOUser = false);

    /// <summary>All KPI rows from BUSINESS_360_DATA × BUSINESS_360_PARAMETER_MASTER for the branch.</summary>
    Task<List<GapPerformanceRow>> GetAllDataAsync(string branchId, DateTime? asOnDate = null);

    /// <summary>Category colour coding from F360_CATEGORY_COLOR.</summary>
    Task<List<F360CategoryColor>> GetCategoryColorsAsync();

    Task<GapReportViewModel> BuildReportAsync(string branchId, DateTime? snapshotDate = null, bool isCOUser = false);
}
