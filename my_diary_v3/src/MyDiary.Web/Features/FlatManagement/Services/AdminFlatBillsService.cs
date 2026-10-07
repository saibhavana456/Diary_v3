using MyDiary.Core.Services;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Microsoft.Data.SqlClient;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class AdminFlatBillsService : IAdminFlatBillsService
{
    private readonly string _connString;
    private readonly ILogger<AdminFlatBillsService> _logger;

    public AdminFlatBillsService(IConfiguration config, ILogger<AdminFlatBillsService> logger)
    {
        _connString = config.GetConnectionString("SSMSConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:SSMSConStr in appsettings.json");
        _logger = logger;
    }

    public async Task<bool> CheckExistingBillAsync(string flatId, string billTypeCode, string billMonth)
    {
        const string sql = @"
            SELECT TOP 1 1
            FROM Bill_Management
            WHERE Bill_Type_Code = @BillTypeCode AND Bill_Month = @BillMonth
              AND Status = 'Active' AND Flat_Id = @FlatId";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@BillTypeCode", billTypeCode);
            cmd.Parameters.AddWithValue("@BillMonth", billMonth);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result is not null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckExistingBillAsync failed for {FlatId}", flatId);
            return true;
        }
    }
    public async Task<AddBillResult> AddBillAsync(AddBillRequest req)
    {
        var referenceNo = AppTime.Now.ToString("yyyyMMddHHmmssfff");
        var billAddDate = AppTime.Now.ToString("dd/MM/yyyy");

        const string sql = @"
            INSERT INTO Bill_Management
                (ReferenceNo, Bill_Type_Code, Bill_Type_Name, Provider_Name, Bill_Month, Bill_Month_to_Show,
                 Bill_Generation_Date, Bill_Cycle_End_Date, Bill_No, Bill_Amount, Paid_Amount, Payment_Date,
                 Remark, Flat_Id, Flat_No, Created_By, Bill_Add_Date, Status)
            VALUES
                (@ReferenceNo, @BillTypeCode, @BillTypeName, @ProviderName, @BillMonth, @BillMonthToShow,
                 @BillGenerationDate, @BillCycleEndDate, @BillNo, @BillAmount, @PaidAmount, @PaymentDate,
                 @Remark, @FlatId, @FlatNo, @CreatedBy, @BillAddDate, 'Active')";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@ReferenceNo", referenceNo);
            cmd.Parameters.AddWithValue("@BillTypeCode", req.BillTypeCode);
            cmd.Parameters.AddWithValue("@BillTypeName", req.BillTypeName);
            cmd.Parameters.AddWithValue("@ProviderName", req.ProviderName);
            cmd.Parameters.AddWithValue("@BillMonth", req.BillMonth);
            cmd.Parameters.AddWithValue("@BillMonthToShow", req.BillMonthToShow);
            cmd.Parameters.AddWithValue("@BillGenerationDate", req.BillGenerationDate);
            cmd.Parameters.AddWithValue("@BillCycleEndDate", (object?)req.BillCycleEndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BillNo", (object?)req.BillNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BillAmount", req.BillAmount);
            cmd.Parameters.AddWithValue("@PaidAmount", req.PaidAmount);
            cmd.Parameters.AddWithValue("@PaymentDate", (object?)req.PaymentDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Remark", (object?)req.Remark ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FlatId", req.FlatId);
            cmd.Parameters.AddWithValue("@FlatNo", (object?)req.FlatNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", req.EnteredBy);
            cmd.Parameters.AddWithValue("@BillAddDate", billAddDate);

            await cn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return new AddBillResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddBillAsync failed for {FlatId}", req.FlatId);
            return new AddBillResult(false, "Some Error Occured. Please try later...");
        }
    }

    public async Task<List<FlatBillRow>> GetBillHistoryAsync(string flatId)
    {
        const string sql = @"
            SELECT Bill_Type_Name, Provider_Name, Bill_Month_to_Show, Bill_Amount, Bill_Cycle_End_Date,
                   Paid_Amount, Bill_Generation_Date, Payment_Date, Remark, Bill_Add_Date
            FROM Bill_Management
            WHERE Flat_Id = @FlatId AND Status = 'Active'
            ORDER BY Bill_Month DESC";

        var result = new List<FlatBillRow>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new FlatBillRow(
                    BillTypeName: r.IsDBNull(0) ? null : r.GetString(0),
                    ProviderName: r.IsDBNull(1) ? null : r.GetString(1),
                    BillMonthToShow: r.IsDBNull(2) ? null : r.GetString(2),
                    BillAmount: r.IsDBNull(3) ? null : r.GetValue(3).ToString(),
                    BillCycleEndDate: r.IsDBNull(4) ? null : r.GetString(4),
                    PaidAmount: r.IsDBNull(5) ? null : r.GetValue(5).ToString(),
                    BillGenerationDate: r.IsDBNull(6) ? null : r.GetString(6),
                    PaymentDate: r.IsDBNull(7) ? null : r.GetString(7),
                    Remark: r.IsDBNull(8) ? null : r.GetString(8),
                    BillAddDate: r.IsDBNull(9) ? null : r.GetString(9)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetBillHistoryAsync failed for {FlatId}", flatId);
        }
        return result;
    }
}
