using MyDiary.Core.Services;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Microsoft.Data.SqlClient;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class AdminFlatMaintenanceService : IAdminFlatMaintenanceService
{
    private readonly string _connString;
    private readonly ILogger<AdminFlatMaintenanceService> _logger;

    public AdminFlatMaintenanceService(IConfiguration config, ILogger<AdminFlatMaintenanceService> logger)
    {
        _connString = config.GetConnectionString("SSMSConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:SSMSConStr in appsettings.json");
        _logger = logger;
    }

    public async Task<AddMaintenanceResult> AddMaintenanceAsync(AddMaintenanceRequest req)
    {
        var referenceNo = AppTime.Now.ToString("yyyyMMddHHmmssfff");
        var addDate = AppTime.Now.ToString("dd/MM/yyyy");

        const string sql = @"
            INSERT INTO Maintenance_Management
                (ReferenceNo, Maintenance_Type_Code, Maintenance_Type_Name, Maintenance_Date, Vendor_Name,
                 Bill_Date, Bill_No, Bill_Amount, Remark, Flat_Id, Flat_No, Created_By, Maintenance_Add_Date, Status)
            VALUES
                (@ReferenceNo, @MaintenanceTypeName, @MaintenanceTypeName, @MaintenanceDate, @VendorName,
                 @BillDate, @BillNo, @BillAmount, @Remark, @FlatId, @FlatNo, @CreatedBy, @AddDate, 'Active')";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@ReferenceNo", referenceNo);
            cmd.Parameters.AddWithValue("@MaintenanceTypeName", req.MaintenanceTypeName);
            cmd.Parameters.AddWithValue("@MaintenanceDate", (object?)req.MaintenanceDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@VendorName", req.VendorName);
            cmd.Parameters.AddWithValue("@BillDate", req.BillDate);
            cmd.Parameters.AddWithValue("@BillNo", req.BillNo);
            cmd.Parameters.AddWithValue("@BillAmount", req.BillAmount);
            cmd.Parameters.AddWithValue("@Remark", (object?)req.Remark ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FlatId", req.FlatId);
            cmd.Parameters.AddWithValue("@FlatNo", (object?)req.FlatNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", req.EnteredBy);
            cmd.Parameters.AddWithValue("@AddDate", addDate);

            await cn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return new AddMaintenanceResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddMaintenanceAsync failed for {FlatId}", req.FlatId);
            return new AddMaintenanceResult(false, "Some Error Occured. Please try later...");
        }
    }
    public async Task<List<FlatMaintenanceRow>> GetMaintenanceHistoryAsync(string flatId)
    {
        const string sql = @"
            SELECT Maintenance_Type_Name, Vendor_Name, Maintenance_Date, Bill_No, Bill_Date, Bill_Amount, Remark
            FROM Maintenance_Management
            WHERE Flat_Id = @FlatId AND Status = 'Active'
            ORDER BY Bill_Date DESC";

        var result = new List<FlatMaintenanceRow>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new FlatMaintenanceRow(
                    MaintenanceTypeName: r.IsDBNull(0) ? null : r.GetString(0),
                    VendorName: r.IsDBNull(1) ? null : r.GetString(1),
                    MaintenanceDate: r.IsDBNull(2) ? null : r.GetString(2),
                    BillNo: r.IsDBNull(3) ? null : r.GetString(3),
                    BillDate: r.IsDBNull(4) ? null : r.GetString(4),
                    BillAmount: r.IsDBNull(5) ? null : r.GetValue(5).ToString(),
                    Remark: r.IsDBNull(6) ? null : r.GetString(6)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMaintenanceHistoryAsync failed for {FlatId}", flatId);
        }
        return result;
    }

    public async Task<bool> DeleteMaintenanceAsync(DeleteMaintenanceRequest req)
    {
        const string sql = @"
            DELETE FROM Maintenance_Management
            WHERE Flat_Id = @FlatId AND Status = 'Active'
              AND Maintenance_Type_Name = @MaintenanceTypeName
              AND Vendor_Name = @VendorName
              AND (Maintenance_Date = @MaintenanceDate OR (Maintenance_Date IS NULL AND @MaintenanceDate IS NULL))
              AND Bill_No = @BillNo
              AND Bill_Date = @BillDate
              AND Bill_Amount = @BillAmount
              AND (Remark = @Remark OR (Remark IS NULL AND @Remark IS NULL))";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", req.FlatId);
            cmd.Parameters.AddWithValue("@MaintenanceTypeName", (object?)req.MaintenanceTypeName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@VendorName", (object?)req.VendorName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MaintenanceDate", (object?)req.MaintenanceDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BillNo", (object?)req.BillNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BillDate", (object?)req.BillDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BillAmount", (object?)req.BillAmount ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Remark", (object?)req.Remark ?? DBNull.Value);

            await cn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteMaintenanceAsync failed for {FlatId}", req.FlatId);
            return false;
        }
    }
}
