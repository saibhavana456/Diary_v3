using MyDiary.Web.Features.Reports.Models;

namespace MyDiary.Web.Features.Reports.Services
{
    public interface IDealerReportService
    {
        Task<List<StateMasterModel>> GetStatesAsync();

        Task<PagedDealerResult> GetDealersAsync(
            string stateCode,
            int pageNumber,
            int pageSize,
            string searchString = null,
            string sortColumn = "name",
            string sortDirection = "ASC");

        Task<byte[]> ExportDealersToExcelAsync(string stateCode, string searchString = null, string sortColumn = "name", string sortDirection = "ASC");
    }
}