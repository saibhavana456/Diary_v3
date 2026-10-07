namespace MyDiary.Web.Features.FlatManagement.Models.DTO;

public record AssetItemOption(string AssetId, string AssetName);

public record CurrentAssetRow(
    string AssetReferenceNo,
    string? AssetName,
    string? NoOfAssets,
    string? PurchasePrice,
    string? AllocationDate);

public record AssetHistoryRow(
    string? AssetName,
    string? NoOfAssets,
    string? PurchasePrice,
    string? AllocationDate,
    string? Remark,
    string? OtherRemark);

public record AssetDetailInfo(
    string AssetReferenceNo,
    string? NoOfAssets,
    string? PurchasePrice,
    string? AllocationDate,
    string? Remark);

public record AssetAllocationRequest(
    string FlatId,
    string FlatNo,
    string AssetId,
    string AssetName,
    string CategoryId,
    string NoOfAssets,
    string PurchasePrice,
    string ProvidedDate,
    string? Remark,
    string EnteredBy);

public record AssetActionResult(bool Success, string? Error);

public record AssetUpdateRequest(
    string AssetReferenceNo,
    string NoOfAssets,
    string PurchasePrice,
    string AllocationDate);

public record SocietyOption(string SocietyId, string SocietyName);

public record FlatOption(string FlatId, string FlatNo);

public record AssetTransferRequest(
    string AssetReferenceNo,
    string SourceFlatNo,
    string SourceSocietyName,
    string SourceLocationName,
    string TargetFlatId,
    string TargetFlatNo,
    string TargetSocietyName,
    string TargetLocationName,
    string TransferDate,
    string? Remark);

public record AssetReplaceRequest(
    string OldAssetReferenceNo,
    string ReplaceDate,
    string? WriteOffDetails,
    string? Remark,
    string FlatId,
    string FlatNo,
    string NewAssetId,
    string NewAssetName,
    string NewCategoryId,
    string NewNoOfAssets,
    string NewPurchasePrice,
    string EnteredBy);

public record AssetSaleRequest(
    string AssetReferenceNo,
    string SaleDate,
    string SaleTo,
    string? NoOfYears,
    string DepreciationCost,
    string SalePrice,
    string? Remark);

public record AssetDiscardRequest(
    string AssetReferenceNo,
    string DiscardDate,
    string? OfficeLetterNo,
    string? Remark);
