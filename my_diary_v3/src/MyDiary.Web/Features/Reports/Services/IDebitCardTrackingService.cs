using MyDiary.Web.Features.Reports.Models;

namespace MyDiary.Web.Features.Reports.Services
{
    public interface IDebitCardTrackingService
    {
        Task<DebitCardTrackingResult> GetTrackingDataAsync(string accountNumber = null);
    }
}