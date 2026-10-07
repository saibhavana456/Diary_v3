using System.Text;
using MyDiary.Core.Services;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Microsoft.Data.SqlClient;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class AdminFlatMasterService : IAdminFlatMasterService
{
    private readonly string _connString;
    private readonly ILogger<AdminFlatMasterService> _logger;

    public AdminFlatMasterService(IConfiguration config, ILogger<AdminFlatMasterService> logger)
    {
        _connString = config.GetConnectionString("SSMSConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:SSMSConStr in appsettings.json");
        _logger = logger;
    }

    public async Task<List<FlatLocationOption>> GetLocationsAsync()
    {
        const string sql = @"
            SELECT DISTINCT Society_Location_Id, Society_Location_Name
            FROM Flat_Allocation
            WHERE Status = 'Active'
            ORDER BY Society_Location_Name";

        var result = new List<FlatLocationOption>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (r.IsDBNull(0)) continue;
                result.Add(new FlatLocationOption(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLocationsAsync failed");
        }
        return result;
    }

    public async Task<List<FlatSocietyOption>> GetSocietiesAsync(string locationId)
    {
        const string sql = @"
            SELECT DISTINCT Society_Id, Society_Name
            FROM Flat_Allocation
            WHERE Status = 'Active' AND Society_Location_Id = @LocId
            ORDER BY Society_Name";

        var result = new List<FlatSocietyOption>();
        var loc = (locationId ?? string.Empty).Trim();
        if (loc.Length == 0) return result;

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@LocId", loc);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (r.IsDBNull(0)) continue;
                result.Add(new FlatSocietyOption(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSocietiesAsync failed for location {LocationId}", loc);
        }
        return result;
    }

    public async Task<List<JurisdictionOption>> GetJurisdictionOptionsAsync()
    {
        const string sql = @"
            SELECT '000000' AS code, 'CO - Corporate Office' AS display_name, 1 AS sort_param
            UNION ALL
            SELECT CAST([zone_code] AS NVARCHAR(20)), 'ZO - ' + [zone_name_eng], 2
            FROM [Organisations].[dbo].[zone_master]
            UNION ALL
            SELECT CAST([region_code] AS NVARCHAR(20)), 'RO - ' + [region_name], 3
            FROM [Organisations].[dbo].[region_master]
            ORDER BY sort_param, display_name";

        var result = new List<JurisdictionOption>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (r.IsDBNull(0)) continue;
                result.Add(new JurisdictionOption(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetJurisdictionOptionsAsync failed");
        }
        return result;
    }

    public async Task<OwnedUsageDefaults?> GetOwnedUsageDefaultsAsync(string societyId)
    {
        const string sql = "SELECT Owned_Status, Usability_Status FROM Society_Details_Master WHERE Society_Id = @Id";

        var id = (societyId ?? string.Empty).Trim();
        if (id.Length == 0) return null;

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new OwnedUsageDefaults(
                OwnedStatus: r.IsDBNull(0) ? null : r.GetString(0),
                UsabilityStatus: r.IsDBNull(1) ? null : r.GetString(1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOwnedUsageDefaultsAsync failed for society {SocietyId}", id);
            return null;
        }
    }

    public async Task<bool> FlatExistsAsync(string flatNo, string societyId, string locationId)
    {
        const string sql = @"
            SELECT 1 FROM Flat_Details_Master
            WHERE Flat_No = @FlatNo AND Society_Id = @SocietyId AND Society_Location_Id = @LocationId";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatNo", flatNo ?? "");
            cmd.Parameters.AddWithValue("@SocietyId", societyId ?? "");
            cmd.Parameters.AddWithValue("@LocationId", locationId ?? "");
            await cn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result is not null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FlatExistsAsync failed for {FlatNo}", flatNo);
            return false;
        }
    }

    public async Task<AddFlatResult> AddFlatAsync(AddFlatRequest req)
    {
        var flatId = AppTime.Now.ToString("yyMMddHHmmssfff") + Random.Shared.Next(100, 999);
        var createdOn = AppTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt");

        const string sql = @"
            INSERT INTO Flat_Details_Master
                (ReferenceNo, Flat_Id, Flat_No, Flat_Name, Flat_Floor_No, Flat_Area, Flat_BHK_Details,
                 Flat_Family_Details, Society_Id, Society_Name, Society_Address, Society_Location_Id,
                 Society_Location_Name, Flat_Owned, Flat_Usability, Flat_Start_Date, Owner_Name,
                 Owner_Address, Owner_Contact, Flat_Occupancy_Status, Flat_Occupant_Count, Created_By,
                 Created_On, Status, Extra1, Extra2, Extra3, Extra4, Extra5, Extra6, Extra7, Extra8,
                 Extra9, Extra10, ManagedBy_Code, ManagedBy_Name)
            VALUES
                (@ReferenceNo, @FlatId, @FlatNo, @FlatName, @FlatFloorNo, @FlatArea, @BhkDetails,
                 @FamilyDetails, @SocietyId, @SocietyName, @SocietyAddress, @LocationId,
                 @LocationName, @OwnedStatus, @Usability, @PurchaseDate, @OwnerName,
                 @OwnerAddress, @OwnerContact, @OccupancyStatus, @OccupantCount, @CreatedBy,
                 @CreatedOn, @Status, @Extra1, @Extra2, @Extra3, @Extra4, @Extra5, @Extra6, @Extra7, @Extra8,
                 @Extra9, @Extra10, @ManagedByCode, @ManagedByName)";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);

            cmd.Parameters.AddWithValue("@ReferenceNo", AppTime.Now.ToString("yyyyMMddHHmmssfff"));
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            cmd.Parameters.AddWithValue("@FlatNo", req.FlatNo);
            cmd.Parameters.AddWithValue("@FlatName", req.LocationName);
            cmd.Parameters.AddWithValue("@FlatFloorNo", "");
            cmd.Parameters.AddWithValue("@FlatArea", req.FlatArea);
            cmd.Parameters.AddWithValue("@BhkDetails", req.BhkDetails);
            cmd.Parameters.AddWithValue("@FamilyDetails", req.FamilyDetails);
            cmd.Parameters.AddWithValue("@SocietyId", req.SocietyId);
            cmd.Parameters.AddWithValue("@SocietyName", req.SocietyName);
            cmd.Parameters.AddWithValue("@SocietyAddress", req.SocietyAddress);
            cmd.Parameters.AddWithValue("@LocationId", req.LocationId);
            cmd.Parameters.AddWithValue("@LocationName", req.LocationName);
            cmd.Parameters.AddWithValue("@OwnedStatus", req.OwnedStatus);
            cmd.Parameters.AddWithValue("@Usability", req.Usability);
            cmd.Parameters.AddWithValue("@PurchaseDate", req.PurchaseDate);
            cmd.Parameters.AddWithValue("@OwnerName", "");
            cmd.Parameters.AddWithValue("@OwnerAddress", "");
            cmd.Parameters.AddWithValue("@OwnerContact", "");
            cmd.Parameters.AddWithValue("@OccupancyStatus", "Vacant");
            cmd.Parameters.AddWithValue("@OccupantCount", "0");
            cmd.Parameters.AddWithValue("@CreatedBy", req.CreatedBy);
            cmd.Parameters.AddWithValue("@CreatedOn", createdOn);
            cmd.Parameters.AddWithValue("@Status", "Active");
            cmd.Parameters.AddWithValue("@Extra1", DBNull.Value);
            cmd.Parameters.AddWithValue("@Extra2", DBNull.Value);
            cmd.Parameters.AddWithValue("@Extra3", DBNull.Value);
            cmd.Parameters.AddWithValue("@Extra4", DBNull.Value);
            cmd.Parameters.AddWithValue("@Extra5", DBNull.Value);
            cmd.Parameters.AddWithValue("@Extra6", DBNull.Value);
            cmd.Parameters.AddWithValue("@Extra7", DBNull.Value);
            cmd.Parameters.AddWithValue("@Extra8", DBNull.Value);
            cmd.Parameters.AddWithValue("@Extra9", DBNull.Value);
            cmd.Parameters.AddWithValue("@Extra10", DBNull.Value);
            cmd.Parameters.AddWithValue("@ManagedByCode", req.ManagedByCode);
            cmd.Parameters.AddWithValue("@ManagedByName", req.ManagedByName);

            await cn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return new AddFlatResult(true, null, flatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddFlatAsync failed for FlatNo {FlatNo}", req.FlatNo);
            return new AddFlatResult(false, UserFacingError.Generic, null);
        }
    }

    public async Task<FlatMasterListResult> GetFlatsAsync(string adminPfNo, string? search, int pageIndex, int pageSize)
    {
        var result = new List<FlatMasterRow>();
        var pfNo = (adminPfNo ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(pfNo)) return new FlatMasterListResult(result, 0);

        try
        {
            await using var cn = new SqlConnection(_connString);
            await cn.OpenAsync();

            var (scopeType, scopeCode) = await LoadScopeAsync(cn, pfNo);

            var where = new StringBuilder(" FROM Flat_Details_Master WHERE Status = 'Active'");

            var q = (search ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(q) && !string.Equals(q, "All", StringComparison.OrdinalIgnoreCase))
                where.Append(" AND (Society_Location_Name LIKE @Q OR Society_Name LIKE @Q OR Flat_No LIKE @Q)");

            if (scopeType == "RO" && !string.IsNullOrEmpty(scopeCode))
                where.Append(" AND ManagedBy_Code = @RO");
            else if (scopeType == "ZO" && !string.IsNullOrEmpty(scopeCode))
                where.Append(@" AND (ManagedBy_Code = @ZO OR ManagedBy_Code IN (
                            SELECT region_code FROM [Organisations].[dbo].[region_master] WHERE zone_code = @ZO))");

            void BindFilters(SqlCommand c)
            {
                if (!string.IsNullOrEmpty(q) && !string.Equals(q, "All", StringComparison.OrdinalIgnoreCase))
                    c.Parameters.AddWithValue("@Q", "%" + q + "%");
                if (scopeType == "RO" && !string.IsNullOrEmpty(scopeCode))
                    c.Parameters.AddWithValue("@RO", scopeCode);
                else if (scopeType == "ZO" && !string.IsNullOrEmpty(scopeCode))
                    c.Parameters.AddWithValue("@ZO", scopeCode);
            }

            int totalCount;
            await using (var countCmd = new SqlCommand("SELECT COUNT(*)" + where, cn))
            {
                BindFilters(countCmd);
                totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            var listSql = @"
                SELECT Flat_Id, Society_Location_Name, Society_Name, Flat_No, Flat_Family_Details,
                       Flat_BHK_Details, Flat_Start_Date, Flat_Occupancy_Status, ManagedBy_Name "
                + where + @"
                ORDER BY Society_Location_Name
                OFFSET @OffsetRows ROWS FETCH NEXT @PageSize ROWS ONLY";

            await using (var listCmd = new SqlCommand(listSql, cn))
            {
                BindFilters(listCmd);
                listCmd.Parameters.AddWithValue("@OffsetRows", Math.Max(0, pageIndex) * pageSize);
                listCmd.Parameters.AddWithValue("@PageSize", pageSize);

                await using var r = await listCmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    result.Add(new FlatMasterRow(
                        FlatId: r.GetString(0),
                        LocationName: r.IsDBNull(1) ? null : r.GetString(1),
                        SocietyName: r.IsDBNull(2) ? null : r.GetString(2),
                        FlatNo: r.IsDBNull(3) ? null : r.GetString(3),
                        FamilyDetails: r.IsDBNull(4) ? null : r.GetString(4),
                        BhkDetails: r.IsDBNull(5) ? null : r.GetString(5),
                        PurchaseDate: r.IsDBNull(6) ? null : r.GetString(6),
                        OccupancyStatus: r.IsDBNull(7) ? null : r.GetString(7),
                        ManagedByName: r.IsDBNull(8) ? null : r.GetString(8)));
                }
            }

            return new FlatMasterListResult(result, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFlatsAsync failed for admin {PfNo}", pfNo);
            return new FlatMasterListResult(result, 0);
        }
    }

    public async Task<List<FlatLocationOption>> GetEditLocationsAsync()
    {
        const string sql = @"
            SELECT DISTINCT Society_Location_Id, Society_Location_Name
            FROM Society_Details_Master
            WHERE Status = 'Active'
            ORDER BY Society_Location_Name";

        var result = new List<FlatLocationOption>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (r.IsDBNull(0)) continue;
                result.Add(new FlatLocationOption(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetEditLocationsAsync failed");
        }
        return result;
    }

    public async Task<List<FlatSocietyOption>> GetEditSocietiesAsync(string locationId)
    {
        const string sql = @"
            SELECT DISTINCT Society_Id, Society_Name
            FROM Society_Details_Master
            WHERE Status = 'Active'
            ORDER BY Society_Name";

        var result = new List<FlatSocietyOption>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (r.IsDBNull(0)) continue;
                result.Add(new FlatSocietyOption(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"GetEditSocietiesAsync failed for location {locationId}");
        }
        return result;
    }

    public async Task<EditFlatDetails?> GetFlatForEditAsync(string flatId)
    {
        const string sql = @"
            SELECT Flat_No, Flat_BHK_Details, Flat_Family_Details, Flat_Area, Society_Id, Society_Location_Id,
                   Society_Address, Flat_Occupant_Count, Flat_Owned, Flat_Usability, Flat_Start_Date,
                   ManagedBy_Code, ManagedBy_Name
            FROM Flat_Details_Master
            WHERE Flat_Id = @FlatId";

        var id = (flatId ?? string.Empty).Trim();
        if (id.Length == 0) return null;

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", id);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            int.TryParse(r.IsDBNull(7) ? "0" : r.GetValue(7).ToString(), out var occupantCount);

            return new EditFlatDetails(
                FlatId: id,
                FlatNo: r.IsDBNull(0) ? null : r.GetString(0),
                BhkDetails: r.IsDBNull(1) ? null : r.GetString(1),
                FamilyDetails: r.IsDBNull(2) ? null : r.GetString(2),
                FlatArea: r.IsDBNull(3) ? null : r.GetString(3),
                SocietyId: r.IsDBNull(4) ? null : r.GetString(4),
                LocationId: r.IsDBNull(5) ? null : r.GetString(5),
                Address: r.IsDBNull(6) ? null : r.GetString(6),
                OccupantCount: occupantCount,
                OwnedStatus: r.IsDBNull(8) ? null : r.GetString(8),
                Usability: r.IsDBNull(9) ? null : r.GetString(9),
                PurchaseDate: r.IsDBNull(10) ? null : r.GetString(10),
                ManagedByCode: r.IsDBNull(11) ? null : r.GetString(11),
                ManagedByName: r.IsDBNull(12) ? null : r.GetString(12));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFlatForEditAsync failed for {FlatId}", id);
            return null;
        }
    }
    public async Task<bool> FlatExistsExcludingAsync(string flatNo, string societyId, string locationId, string excludeFlatId)
    {
        const string sql = @"
            SELECT 1 FROM Flat_Details_Master
            WHERE Flat_No = @FlatNo AND Society_Id = @SocietyId AND Society_Location_Id = @LocationId
              AND Flat_Id <> @ExcludeFlatId";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatNo", flatNo ?? "");
            cmd.Parameters.AddWithValue("@SocietyId", societyId ?? "");
            cmd.Parameters.AddWithValue("@LocationId", locationId ?? "");
            cmd.Parameters.AddWithValue("@ExcludeFlatId", excludeFlatId ?? "");
            await cn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result is not null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FlatExistsExcludingAsync failed for {FlatNo}", flatNo);
            return false;
        }
    }

    public async Task<AddFlatResult> UpdateFlatAsync(EditFlatRequest req)
    {
        const string sql = @"
            UPDATE Flat_Details_Master
               SET Flat_No = @FlatNo,
                   Flat_Area = @FlatArea,
                   Flat_BHK_Details = @BhkDetails,
                   Flat_Family_Details = @FamilyDetails,
                   Society_Id = @SocietyId,
                   Society_Name = @SocietyName,
                   Society_Address = @SocietyAddress,
                   Society_Location_Id = @LocationId,
                   Society_Location_Name = @LocationName,
                   Flat_Owned = @OwnedStatus,
                   Flat_Usability = @Usability,
                   Flat_Start_Date = @PurchaseDate,
                   ManagedBy_Code = @ManagedByCode,
                   ManagedBy_Name = @ManagedByName
             WHERE Flat_Id = @FlatId";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatNo", req.FlatNo);
            cmd.Parameters.AddWithValue("@FlatArea", req.FlatArea);
            cmd.Parameters.AddWithValue("@BhkDetails", req.BhkDetails);
            cmd.Parameters.AddWithValue("@FamilyDetails", req.FamilyDetails);
            cmd.Parameters.AddWithValue("@SocietyId", req.SocietyId);
            cmd.Parameters.AddWithValue("@SocietyName", req.SocietyName);
            cmd.Parameters.AddWithValue("@SocietyAddress", req.SocietyAddress);
            cmd.Parameters.AddWithValue("@LocationId", req.LocationId);
            cmd.Parameters.AddWithValue("@LocationName", req.LocationName);
            cmd.Parameters.AddWithValue("@OwnedStatus", req.OwnedStatus);
            cmd.Parameters.AddWithValue("@Usability", req.Usability);
            cmd.Parameters.AddWithValue("@PurchaseDate", req.PurchaseDate);
            cmd.Parameters.AddWithValue("@ManagedByCode", req.ManagedByCode);
            cmd.Parameters.AddWithValue("@ManagedByName", req.ManagedByName);
            cmd.Parameters.AddWithValue("@FlatId", req.FlatId);

            await cn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0)
                return new AddFlatResult(false, "Flat Details with this Flat No. doesn't exist..", null);

            return new AddFlatResult(true, null, req.FlatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateFlatAsync failed for {FlatId}", req.FlatId);
            return new AddFlatResult(false, UserFacingError.Generic, null);
        }
    }

    private async Task<(string ScopeType, string ScopeCode)> LoadScopeAsync(SqlConnection cn, string pfNo)
    {
        const string sql = @"
            SELECT TOP 1 AdminScopeType, AdminScopeCode
            FROM USER_MASTER
            WHERE EMP_ID = @Pf AND USER_TYPE = 'admin' AND Status = 'Active'
            ORDER BY TRANS_ID DESC";

        try
        {
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Pf", pfNo);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ("", "");

            var scopeType = r.IsDBNull(0) ? "" : r.GetString(0).Trim().ToUpperInvariant();
            var scopeCode = r.IsDBNull(1) ? "" : r.GetString(1).Trim();
            return (scopeType, scopeCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoadScopeAsync (SSMS) failed for admin {PfNo}; treating as unscoped", pfNo);
            return ("", "");
        }
    }
}
