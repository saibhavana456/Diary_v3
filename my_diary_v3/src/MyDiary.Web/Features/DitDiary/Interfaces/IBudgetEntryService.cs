// MyDiary.Web.Features.DitDiary/Interfaces/IBudgetEntryService.cs
using MyDiary.Web.Features.DitDiary.Models.Entities;
using MyDiary.Web.Features.DitDiary.Models.DTO;
namespace MyDiary.Web.Features.DitDiary.Interfaces
{
    /// <summary>
    /// Monthly budget ENTRY (the + button forms) — stores into the new
    /// detailed tables and mirrors a summary row into BUDGET_UTILIZATION
    /// so the existing list and dashboard keep working.
    /// </summary>
    public interface IBudgetEntryService
    {
        Task<List<Department>> GetDepartmentsAsync();
        Task<List<Section>> GetCapitalSectionsByDeptAsync(long? deptId);
        Task<List<BudgetRow>> GetCapitalRowsAsync(int fyId, long? deptId, string? sectionName, string month);
        Task<List<BudgetRow>> GetRevenueRowsAsync(int fyId, long? deptId, string? sectionName, string month);
        Task<List<FinancialYear>> GetFinancialYearsAsync();
        Task<FinancialYear?> GetCurrentFyAsync();
        Task<List<ExpenditureHead>> GetHeadsAsync();

        Task<List<Section>> GetSectionsAsync(string appliesTo);            // 'CAPITAL' / 'REVENUE'
        Task<List<ProjectsMaster>> GetProjectsForSectionAsync(string? sectionName);

        Task<BudgetAllocation?> GetCapitalAllocationAsync(int projectId, int fyId);
        Task<BudgetAllocation?> GetRevenueAllocationAsync(int projectId, int fyId);

        Task<decimal> GetCapitalCumulativeAsync(int projectId, int fyId);
        Task<decimal> GetRevenueCumulativeAsync(int projectId, int fyId);
        Task<decimal> GetCapitalFyUtilizedAsync(int fyId); // added this line (B1)
        Task<decimal> GetRevenueFyUtilizedAsync(int fyId); // added this line (B1)

        Task<CapitalUtilization?> GetCapitalEntryAsync(int projectId, int fyId, string month);
        Task<List<RevenueUtilization>> GetRevenueEntriesAsync(int projectId, int fyId, string month);

        Task<bool> IsMonthSubmittedAsync(string view, int projectId, int fyId, string month);
        Task<int> GetProjectStartMonthIndexAsync(int projectId);

        /// <returns>null on success, otherwise the blocking error message (BRD rule)</returns>
        Task<string?> SubmitCapitalAsync(CapitalUtilization entry, string fyLabel, string sectionName, BudgetAllocation alloc);

        /// <summary>Revenue rule: over-budget WARNS but still saves (caller shows the popup first).</summary>
        Task SubmitRevenueAsync(List<RevenueUtilization> rows, string fyLabel, string sectionName, BudgetAllocation alloc, bool overBudget, decimal exceededBy);
        Task<string?> GetSubmissionOwnerAsync(string view, int? projectId, long? sectionId, int fyId, string month);
        Task<BudgetDashboardData> GetDashboardAsync(string view, int fyId, long? deptId, string? sectionName, string month, List<long>? scopeSectionIds);
    }
}