using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IAdminAssetAllocationService
{
    Task<decimal?> GetTotalAssetValueAsync(string flatId);
    Task<List<AssetItemOption>> GetItemsAsync(string categoryId);

    Task<List<CurrentAssetRow>> GetCurrentAssetsAsync(string flatId);
    Task<AssetActionResult> UpdateAssetAsync(AssetUpdateRequest req);
    Task<AssetActionResult> DeleteAssetAsync(string assetReferenceNo);

    Task<List<AssetHistoryRow>> GetAssetHistoryAsync(string flatId);

    Task<AssetActionResult> AllocateAssetAsync(AssetAllocationRequest req);

    Task<List<AssetItemOption>> GetActiveAssetsForFlatAsync(string flatId);
    Task<AssetDetailInfo?> GetAssetDetailAsync(string assetReferenceNo);

    Task<List<SocietyOption>> GetActiveSocietiesAsync();
    Task<List<SocietyOption>> GetSocietiesFilteredAsync(string societyId);
    Task<List<FlatOption>> GetFlatsBySocietyAsync(string societyId);

    Task<AssetActionResult> TransferAssetAsync(AssetTransferRequest req);
    Task<AssetActionResult> ReplaceAssetAsync(AssetReplaceRequest req);
    Task<AssetActionResult> SaleAssetAsync(AssetSaleRequest req);
    Task<AssetActionResult> DiscardAssetAsync(AssetDiscardRequest req);
}
