using MyDiary.Web.Features.RatesCharges.Models;

namespace MyDiary.Web.Features.RatesCharges.Services
{
    public interface IRatesChargesService
    {
        Task<List<DepositCategory>> GetCategoriesAsync();
        Task<List<DepositRate>> GetRatesByCategoryAsync(int categoryId);
        Task<List<MultiCurrencySection>> GetMultiCurrencyRatesAsync(int categoryId);

        /// <summary>✅ NEW — fetches the free-form HTML content blocks for categories whose ContentMode is "HTML".</summary>
        Task<List<CategoryContentBlock>> GetCategoryContentAsync(int categoryId);

        Task<List<MclrRate>> GetMclrRatesAsync();
        Task<List<BaseRateBplr>> GetBaseRateBplrAsync();
        Task<List<RatesChargesLink>> GetLinksBySectionAsync(string sectionCode);

        Task<List<MultiCurrencySection>> GetBulkDepositRatesAsync();
        Task<DateTime> GetBulkEffectiveDateAsync();
    }
}
