using MyDiary.Web.Features.OperationDashboard.Models;
using System.Collections.Generic;
using System.Data;
namespace MyDiary.Web.Features.OperationDashboard.Services
{
    public interface IOperationDashboardService
    {
        Task<SundryOSReportDto> GetSundryEntriesDataAsync(string solId);
        Task<SuspenseOSReportDto> GetSuspenseEntriesDataAsync(string solId);
        Task<LockerReportDto> GetLockerReportDataAsync(string solId);
        Task<AccountStatusReportDto> GetDefaultingAccountsDataAsync(string solId);
        Task<CashHoldingReportDto> GetCashHoldingDataAsync(string solId);
        Task<CKYCPendingReportDto> GetCKYCPendencyDataAsync(string solId);
        Task<ReKYCPendencyReportDto> GetReKYCPendencyDataAsync(string solId);
        Task<UCICAadhaarReportDto> GetUCICAadhaarPendencyDataAsync(string solId);
        Task<UCICPanReportDto> GetUCICPanPendencyDataAsync(string solId);
        Task<BeneficiaryOwnerReportDto> GetBeneficiaryOwnerDataAsync(string solId);
        Task<BOComplaintReportDto> GetCustomerComplaintsDataAsync(string solId);

        Task<DataSet> GetDashboardDataAsync(string solId);
        Task<IEnumerable<DropdownItemDto>> GetDropdownDataAsync(string type, string parentSolId = null);
    }
}