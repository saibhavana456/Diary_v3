using MyDiary.Core.Services;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Microsoft.Data.SqlClient;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class AdminAssetAllocationService : IAdminAssetAllocationService
{
    private readonly string _connString;
    private readonly ILogger<AdminAssetAllocationService> _logger;

    public AdminAssetAllocationService(IConfiguration config, ILogger<AdminAssetAllocationService> logger)
    {
        _connString = config.GetConnectionString("SSMSConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:SSMSConStr in appsettings.json");
        _logger = logger;
    }

    public async Task<decimal?> GetTotalAssetValueAsync(string flatId)
    {
        const string sql = "SELECT SUM(CONVERT(int, Asset_Purchase_Price)) FROM Asset_Allocation WHERE Flat_Id = @FlatId AND Asset_Status = 'Active'";
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result is null or DBNull ? 0m : Convert.ToDecimal(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTotalAssetValueAsync failed for {FlatId}", flatId);
            return null;
        }
    }

    public async Task<List<AssetItemOption>> GetItemsAsync(string categoryId)
    {
        const string sql = "SELECT Asset_Id, Asset_Name FROM Asset_Master WHERE Asset_Category_Id = @Cat ORDER BY Asset_Name";
        var result = new List<AssetItemOption>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Cat", categoryId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (!r.IsDBNull(0)) result.Add(new AssetItemOption(r.GetValue(0).ToString()!, r.IsDBNull(1) ? "" : r.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetItemsAsync failed for category {Category}", categoryId);
        }
        return result;
    }

    public async Task<List<CurrentAssetRow>> GetCurrentAssetsAsync(string flatId)
    {
        const string sql = @"
            SELECT Asset_Reference_No, Asset_Name, No_of_Assets, Asset_Purchase_Price, Asset_Allocation_Date
            FROM Asset_Allocation
            WHERE Flat_Id = @FlatId AND Asset_Status = 'Active'
            ORDER BY Asset_Allocation_Date DESC";
        var result = new List<CurrentAssetRow>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new CurrentAssetRow(
                    AssetReferenceNo: r.GetValue(0).ToString()!,
                    AssetName: r.IsDBNull(1) ? null : r.GetString(1),
                    NoOfAssets: r.IsDBNull(2) ? null : r.GetValue(2).ToString(),
                    PurchasePrice: r.IsDBNull(3) ? null : r.GetValue(3).ToString(),
                    AllocationDate: r.IsDBNull(4) ? null : r.GetValue(4).ToString()));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCurrentAssetsAsync failed for {FlatId}", flatId);
        }
        return result;
    }

    public async Task<AssetActionResult> UpdateAssetAsync(AssetUpdateRequest req)
    {
        const string sql = @"
            UPDATE Asset_Allocation
               SET No_of_Assets = @N, Asset_Purchase_Price = @P, Asset_Allocation_Date = @D
             WHERE Asset_Reference_No = @Ref AND Asset_Status = 'Active'";
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@N", req.NoOfAssets);
            cmd.Parameters.AddWithValue("@P", req.PurchasePrice);
            cmd.Parameters.AddWithValue("@D", req.AllocationDate);
            cmd.Parameters.AddWithValue("@Ref", req.AssetReferenceNo);
            await cn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return new AssetActionResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAssetAsync failed for {Ref}", req.AssetReferenceNo);
            return new AssetActionResult(false, UserFacingError.Generic);
        }
    }

    public async Task<AssetActionResult> DeleteAssetAsync(string assetReferenceNo)
    {
        const string sql = "DELETE FROM Asset_Allocation WHERE Asset_Reference_No = @Ref";
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Ref", assetReferenceNo);
            await cn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return new AssetActionResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteAssetAsync failed for {Ref}", assetReferenceNo);
            return new AssetActionResult(false, UserFacingError.Generic);
        }
    }

    public async Task<List<AssetHistoryRow>> GetAssetHistoryAsync(string flatId)
    {
        const string sql = @"
            SELECT Asset_Name, No_of_Assets, Asset_Purchase_Price, Asset_Allocation_Date, Remark, Asset_Other_Remark
            FROM Asset_Allocation_Previous
            WHERE Flat_Id = @FlatId";
        var result = new List<AssetHistoryRow>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new AssetHistoryRow(
                    AssetName: r.IsDBNull(0) ? null : r.GetString(0),
                    NoOfAssets: r.IsDBNull(1) ? null : r.GetValue(1).ToString(),
                    PurchasePrice: r.IsDBNull(2) ? null : r.GetValue(2).ToString(),
                    AllocationDate: r.IsDBNull(3) ? null : r.GetValue(3).ToString(),
                    Remark: r.IsDBNull(4) ? null : r.GetString(4),
                    OtherRemark: r.IsDBNull(5) ? null : r.GetString(5)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAssetHistoryAsync failed for {FlatId}", flatId);
        }
        return result;
    }

    public async Task<AssetActionResult> AllocateAssetAsync(AssetAllocationRequest req)
    {
        try
        {
            await using var cn = new SqlConnection(_connString);
            await cn.OpenAsync();
            await InsertAssetRowAsync(cn, req.FlatId, req.AssetId, req.AssetName, req.CategoryId,
                req.NoOfAssets, req.PurchasePrice, req.ProvidedDate, req.Remark);
            return new AssetActionResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AllocateAssetAsync failed for {FlatId}", req.FlatId);
            return new AssetActionResult(false, "Some Error Occured. Please try later...");
        }
    }

    private static async Task InsertAssetRowAsync(SqlConnection cn, string flatId, string assetId, string assetName,
        string categoryId, string noOfAssets, string purchasePrice, string allocationDate, string? remark)
    {
        const string sql = @"
            INSERT INTO Asset_Allocation
                (Asset_Reference_No, Asset_Id, Asset_Category_Id, Asset_Name, No_of_Assets,
                 Asset_Purchase_Price, Asset_Allocation_Date, Flat_Id, Asset_Status, Remark, Asset_Other_Remark)
            VALUES
                (@Ref, @AssetId, @Category, @Name, @NoOfAssets, @Price, @Date, @FlatId, 'Active', @Remark, @Remark)";

        var refNo = AppTime.Now.ToString("yyyyMMddHHmmssfff");
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Ref", refNo);
        cmd.Parameters.AddWithValue("@AssetId", assetId);
        cmd.Parameters.AddWithValue("@Category", categoryId);
        cmd.Parameters.AddWithValue("@Name", assetName);
        cmd.Parameters.AddWithValue("@NoOfAssets", noOfAssets);
        cmd.Parameters.AddWithValue("@Price", purchasePrice);
        cmd.Parameters.AddWithValue("@Date", allocationDate);
        cmd.Parameters.AddWithValue("@FlatId", flatId);
        cmd.Parameters.AddWithValue("@Remark", (object?)remark ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AssetItemOption>> GetActiveAssetsForFlatAsync(string flatId)
    {
        const string sql = "SELECT Asset_Reference_No, Asset_Name FROM Asset_Allocation WHERE Flat_Id = @FlatId AND Asset_Status = 'Active' ORDER BY Asset_Name";
        var result = new List<AssetItemOption>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new AssetItemOption(r.GetValue(0).ToString()!, r.IsDBNull(1) ? "" : r.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetActiveAssetsForFlatAsync failed for {FlatId}", flatId);
        }
        return result;
    }

    public async Task<AssetDetailInfo?> GetAssetDetailAsync(string assetReferenceNo)
    {
        const string sql = "SELECT No_of_Assets, Asset_Purchase_Price, Asset_Allocation_Date, Remark FROM Asset_Allocation WHERE Asset_Reference_No = @Ref";
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Ref", assetReferenceNo);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new AssetDetailInfo(
                AssetReferenceNo: assetReferenceNo,
                NoOfAssets: r.IsDBNull(0) ? null : r.GetValue(0).ToString(),
                PurchasePrice: r.IsDBNull(1) ? null : r.GetValue(1).ToString(),
                AllocationDate: r.IsDBNull(2) ? null : r.GetValue(2).ToString(),
                Remark: r.IsDBNull(3) ? null : r.GetString(3));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAssetDetailAsync failed for {Ref}", assetReferenceNo);
            return null;
        }
    }

    public async Task<List<SocietyOption>> GetActiveSocietiesAsync()
    {
        const string sql = "SELECT DISTINCT Society_Id, Society_Name FROM Society_Details_Master WHERE Status = 'Active' ORDER BY Society_Name";
        var result = new List<SocietyOption>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new SocietyOption(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetActiveSocietiesAsync failed");
        }
        return result;
    }

    public async Task<List<SocietyOption>> GetSocietiesFilteredAsync(string societyId)
    {
        const string sql = "SELECT Society_Id, Society_Name FROM Society_Details_Master WHERE Status = 'Active' AND Society_Id = @SocietyId";
        var result = new List<SocietyOption>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@SocietyId", societyId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new SocietyOption(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSocietiesFilteredAsync failed for {SocietyId}", societyId);
        }
        return result;
    }

    public async Task<List<FlatOption>> GetFlatsBySocietyAsync(string societyId)
    {
        const string sql = "SELECT Flat_Id, Flat_No FROM Flat_Details_Master WHERE Society_Id = @SocietyId ORDER BY Flat_No";
        var result = new List<FlatOption>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@SocietyId", societyId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new FlatOption(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFlatsBySocietyAsync failed for {SocietyId}", societyId);
        }
        return result;
    }

    public async Task<AssetActionResult> TransferAssetAsync(AssetTransferRequest req)
    {
        try
        {
            await using var cn = new SqlConnection(_connString);
            await cn.OpenAsync();

            var detail = await GetAssetDetailInternalAsync(cn, req.AssetReferenceNo);
            if (detail is null)
                return new AssetActionResult(false, "Asset not found.");

            var remarkTo = $"Transferred to: ({req.TargetFlatNo}, {req.TargetSocietyName}, {req.TargetLocationName}) On: ({req.TransferDate})";
            var remarkFrom = $"Transferred from: ({req.SourceFlatNo}, {req.SourceSocietyName}, {req.SourceLocationName}) On: ({req.TransferDate})";

            await InsertHistoryRowDirectAsync(cn, detail, req.Remark ?? remarkFrom, remarkTo, sourceFlatId: null);

            const string updateSql = "UPDATE Asset_Allocation SET Flat_Id = @TargetFlatId WHERE Asset_Reference_No = @Ref AND Asset_Status = 'Active'";
            await using (var cmd = new SqlCommand(updateSql, cn))
            {
                cmd.Parameters.AddWithValue("@TargetFlatId", req.TargetFlatId);
                cmd.Parameters.AddWithValue("@Ref", req.AssetReferenceNo);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return new AssetActionResult(false, "Asset is no longer active.");
            }

            return new AssetActionResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TransferAssetAsync failed for {Ref}", req.AssetReferenceNo);
            return new AssetActionResult(false, "Some Error Occured. Please try later...");
        }
    }

    public async Task<AssetActionResult> ReplaceAssetAsync(AssetReplaceRequest req)
    {
        try
        {
            await using var cn = new SqlConnection(_connString);
            await cn.OpenAsync();

            var detail = await GetAssetDetailInternalAsync(cn, req.OldAssetReferenceNo);
            if (detail is null)
                return new AssetActionResult(false, "Asset not found.");

            var remark = string.IsNullOrWhiteSpace(req.WriteOffDetails) ? null : $"Write Off Details:{req.WriteOffDetails}";
            var otherRemark = $"Replaced with New Furniture on: {req.ReplaceDate} Remark:{req.Remark}";

            await InsertHistoryRowDirectAsync(cn, detail, remark, otherRemark, sourceFlatId: req.FlatId);

            const string deactivateSql = "UPDATE Asset_Allocation SET Asset_Status = 'Deactive' WHERE Asset_Reference_No = @Ref AND Asset_Status = 'Active'";
            await using (var cmd = new SqlCommand(deactivateSql, cn))
            {
                cmd.Parameters.AddWithValue("@Ref", req.OldAssetReferenceNo);
                await cmd.ExecuteNonQueryAsync();
            }

            await InsertAssetRowAsync(cn, req.FlatId, req.NewAssetId, req.NewAssetName, req.NewCategoryId,
                req.NewNoOfAssets, req.NewPurchasePrice, req.ReplaceDate, req.Remark);

            return new AssetActionResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReplaceAssetAsync failed for {Ref}", req.OldAssetReferenceNo);
            return new AssetActionResult(false, "Some Error Occured. Please try later...");
        }
    }

    public async Task<AssetActionResult> SaleAssetAsync(AssetSaleRequest req)
    {
        try
        {
            await using var cn = new SqlConnection(_connString);
            await cn.OpenAsync();

            var detail = await GetAssetDetailInternalAsync(cn, req.AssetReferenceNo);
            if (detail is null)
                return new AssetActionResult(false, "Asset not found.");

            var otherRemark = $"Sold to {req.SaleTo} in Rs.{req.SalePrice} on: {req.SaleDate}";
            await InsertHistoryRowDirectAsync(cn, detail, req.Remark, otherRemark, sourceFlatId: null, refFallback: req.AssetReferenceNo);

            const string sql = "UPDATE Asset_Allocation SET Asset_Status = 'Deactive' WHERE Asset_Reference_No = @Ref AND Asset_Status = 'Active'";
            await using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@Ref", req.AssetReferenceNo);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return new AssetActionResult(false, "Asset is no longer active.");
            }

            return new AssetActionResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaleAssetAsync failed for {Ref}", req.AssetReferenceNo);
            return new AssetActionResult(false, "Some Error Occured. Please try later...");
        }
    }

    public async Task<AssetActionResult> DiscardAssetAsync(AssetDiscardRequest req)
    {
        try
        {
            await using var cn = new SqlConnection(_connString);
            await cn.OpenAsync();

            var detail = await GetAssetDetailInternalAsync(cn, req.AssetReferenceNo);
            if (detail is null)
                return new AssetActionResult(false, "Asset not found.");

            var remark = string.Join(", ", new[] { req.OfficeLetterNo, req.Remark }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var otherRemark = $"Discarded on: {req.DiscardDate}{req.Remark}";

            await InsertHistoryRowDirectAsync(cn, detail, remark, otherRemark, sourceFlatId: null, refFallback: req.AssetReferenceNo);

            const string sql = "UPDATE Asset_Allocation SET Asset_Status = 'Deactive' WHERE Asset_Reference_No = @Ref AND Asset_Status = 'Active'";
            await using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@Ref", req.AssetReferenceNo);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return new AssetActionResult(false, "Asset is no longer active.");
            }

            return new AssetActionResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DiscardAssetAsync failed for {Ref}", req.AssetReferenceNo);
            return new AssetActionResult(false, "Some Error Occured. Please try later...");
        }
    }

    private static async Task<AssetDetailFull?> GetAssetDetailInternalAsync(SqlConnection cn, string assetReferenceNo)
    {
        const string sql = "SELECT Asset_Name, No_of_Assets, Asset_Purchase_Price, Asset_Allocation_Date, Flat_Id FROM Asset_Allocation WHERE Asset_Reference_No = @Ref";
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Ref", assetReferenceNo);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new AssetDetailFull(
            AssetName: r.IsDBNull(0) ? null : r.GetString(0),
            NoOfAssets: r.IsDBNull(1) ? null : r.GetValue(1).ToString(),
            PurchasePrice: r.IsDBNull(2) ? null : r.GetValue(2).ToString(),
            AllocationDate: r.IsDBNull(3) ? null : r.GetValue(3).ToString(),
            FlatId: r.IsDBNull(4) ? null : r.GetString(4));
    }

    private record AssetDetailFull(string? AssetName, string? NoOfAssets, string? PurchasePrice, string? AllocationDate, string? FlatId);

    private static async Task InsertHistoryRowDirectAsync(SqlConnection cn, AssetDetailFull detail, string? remark, string? otherRemark, string? sourceFlatId, string? refFallback = null)
    {
        const string sql = @"
            INSERT INTO Asset_Allocation_Previous
                (Asset_Name, No_of_Assets, Asset_Purchase_Price, Asset_Allocation_Date, Remark, Asset_Other_Remark, Flat_Id)
            VALUES
                (@Name, @NoOfAssets, @Price, @Date, @Remark, @OtherRemark, @FlatId)";

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Name", (object?)detail.AssetName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@NoOfAssets", (object?)detail.NoOfAssets ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Price", (object?)detail.PurchasePrice ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Date", (object?)detail.AllocationDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Remark", (object?)remark ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OtherRemark", (object?)otherRemark ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FlatId", (object?)(sourceFlatId ?? detail.FlatId) ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }
}
