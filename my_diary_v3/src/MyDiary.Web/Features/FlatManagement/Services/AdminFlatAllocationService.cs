using MyDiary.Core.Services;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Microsoft.Data.SqlClient;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class AdminFlatAllocationService : IAdminFlatAllocationService
{
    private readonly string _connString;
    private readonly ILogger<AdminFlatAllocationService> _logger;

    public AdminFlatAllocationService(IConfiguration config, ILogger<AdminFlatAllocationService> logger)
    {
        _connString = config.GetConnectionString("SSMSConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:SSMSConStr in appsettings.json");
        _logger = logger;
    }

    public async Task<FlatAllocationFlatInfo?> GetFlatInfoAsync(string flatId)
    {
        const string sql = @"
            SELECT Flat_No, Flat_Area, Flat_BHK_Details, Society_Id, Society_Name, Society_Address,
                   Society_Location_Id, Society_Location_Name, Flat_Family_Details,
                   Flat_Occupancy_Status, Flat_Occupant_Count, ManagedBy_Code, ManagedBy_Name
            FROM Flat_Details_Master
            WHERE Flat_Id = @FlatId";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            var occupantCountRaw = r.IsDBNull(10) ? "" : r.GetValue(10).ToString();
            int.TryParse(occupantCountRaw, out var occupantCount);

            return new FlatAllocationFlatInfo(
                FlatId: flatId,
                FlatNo: r.IsDBNull(0) ? null : r.GetString(0),
                FlatArea: r.IsDBNull(1) ? null : r.GetString(1),
                BhkDetails: r.IsDBNull(2) ? null : r.GetString(2),
                SocietyId: r.IsDBNull(3) ? null : r.GetString(3),
                SocietyName: r.IsDBNull(4) ? null : r.GetString(4),
                Address: r.IsDBNull(5) ? null : r.GetString(5),
                LocationId: r.IsDBNull(6) ? null : r.GetString(6),
                LocationName: r.IsDBNull(7) ? null : r.GetString(7),
                FamilyDetails: r.IsDBNull(8) ? null : r.GetString(8),
                OccupancyStatus: r.IsDBNull(9) ? null : r.GetString(9),
                OccupantCount: occupantCount,
                ManagedByCode: r.IsDBNull(11) ? null : r.GetString(11),
                ManagedByName: r.IsDBNull(12) ? null : r.GetString(12));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFlatInfoAsync failed for {FlatId}", flatId);
            return null;
        }
    }

    public async Task<List<FlatOccupantRow>> GetCurrentOccupantsAsync(string flatId)
    {
        const string sql = @"
            SELECT Employee_PF, Employee_Name, Employee_Designation, Employee_Department,
                   Extra1, Extra2, Flat_Occupy_Date
            FROM Flat_Allocation
            WHERE Flat_Id = @FlatId AND Status = 'Active'
            ORDER BY Employee_PF";

        var result = new List<FlatOccupantRow>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new FlatOccupantRow(
                    EmployeePf: r.GetString(0),
                    EmployeeName: r.IsDBNull(1) ? null : r.GetString(1),
                    EmployeeDesignation: r.IsDBNull(2) ? null : r.GetString(2),
                    EmployeeDepartment: r.IsDBNull(3) ? null : r.GetString(3),
                    Email: r.IsDBNull(4) ? null : r.GetString(4),
                    Phone: r.IsDBNull(5) ? null : r.GetString(5),
                    OccupyDate: r.IsDBNull(6) ? null : r.GetString(6)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCurrentOccupantsAsync failed for {FlatId}", flatId);
        }
        return result;
    }

    public async Task<FlatAllocationResult> UpdateOccupantAsync(FlatOccupantEditRequest req)
    {
        const string sql = @"
            UPDATE Flat_Allocation
               SET Employee_Name = @Name,
                   Employee_Designation = @Designation,
                   Employee_Department = @Department,
                   Extra1 = @Email,
                   Extra2 = @Phone
             WHERE Employee_PF = @Pf AND Flat_Id = @FlatId
               AND Occupy_Status = 'Alloted' AND Status = 'Active'";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Name", req.EmployeeName);
            cmd.Parameters.AddWithValue("@Designation", req.EmployeeDesignation);
            cmd.Parameters.AddWithValue("@Department", (object?)req.EmployeeDepartment ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", (object?)req.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Phone", (object?)req.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pf", req.EmployeePf);
            cmd.Parameters.AddWithValue("@FlatId", req.FlatId);
            await cn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return new FlatAllocationResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateOccupantAsync failed for {Pf}", req.EmployeePf);
            return new FlatAllocationResult(false, UserFacingError.Generic);
        }
    }

    public async Task<StaffLookupInfo?> GetStaffByPfAsync(string pfNo)
    {
        const string sql = @"
            SELECT EMPLID, NAME, DEPTID, DESCR1, REGION_CODE, REGION_NAME, DIVISION_CODE, DIVISION_NAME,
                   EMP_SCALE_DESCR, POSTING_DATE, EXPECTED_END_DATE, PHONE, EMAIL
            FROM [Organisations].[dbo].[StaffDetails]
            WHERE EMPLID = @Pf";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Pf", pfNo.Trim());
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new StaffLookupInfo(
                PfNo: r.GetString(0),
                Name: r.IsDBNull(1) ? null : r.GetString(1),
                Department: r.IsDBNull(3) ? null : r.GetString(3),
                RoCode: r.IsDBNull(4) ? null : r.GetString(4),
                RoName: r.IsDBNull(5) ? null : r.GetString(5),
                ZoCode: r.IsDBNull(6) ? null : r.GetString(6),
                ZoName: r.IsDBNull(7) ? null : r.GetString(7),
                Designation: r.IsDBNull(8) ? null : r.GetString(8),
                PostingDate: SafeFormatDate(r, 9),
                RetirementDate: SafeFormatDate(r, 10),
                Phone: r.IsDBNull(11) ? null : r.GetString(11),
                Email: r.IsDBNull(12) ? null : r.GetString(12));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStaffByPfAsync failed for {Pf}", pfNo);
            return null;
        }
    }

    public async Task<string?> CheckExistingAllocationAsync(string pfNo)
    {
        const string sql = @"
            SELECT Flat_No, Society_Name, Society_Location_Name
            FROM Flat_Allocation
            WHERE Employee_PF = @Pf AND Status = 'Active'";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Pf", pfNo);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            var flatNo = r.IsDBNull(0) ? "" : r.GetString(0);
            var societyName = r.IsDBNull(1) ? "" : r.GetString(1);
            var locationName = r.IsDBNull(2) ? "" : r.GetString(2);
            return $"Flat No.:{flatNo}, {societyName}, {locationName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckExistingAllocationAsync failed for {Pf}", pfNo);
            return null;
        }
    }

    public async Task<FlatAllocationResult> AllocateAsync(FlatAllocationRequest req)
    {
        try
        {
            await using var cn = new SqlConnection(_connString);
            await cn.OpenAsync();

            var flat = await GetFlatInfoInternalAsync(cn, req.FlatId);
            if (flat is null)
                return new FlatAllocationResult(false, "Flat Details with this Flat No. doesn't exist..");

            var activeCount = await GetActiveOccupantCountAsync(cn, req.FlatId);
            var isFamily = string.Equals(flat.FamilyDetails, "Family", StringComparison.OrdinalIgnoreCase);
            var isBachelor = string.Equals(flat.FamilyDetails, "Bachelor", StringComparison.OrdinalIgnoreCase);
            var allowed = (isFamily && activeCount == 0) || isBachelor;
            if (!allowed)
            {
                return new FlatAllocationResult(false,
                    "You can not allocate a family Flat to more than one Occupants. " +
                    "Deallocate the current occupant OR change flat category to Bachelor");
            }

            var existing = await CheckExistingAllocationInternalAsync(cn, req.EmployeePf);
            if (existing is not null)
                return new FlatAllocationResult(false, $"The user already have a Occupied Flat {existing} Kindly Deallocated the Previous then Allocate the New..");

            var refNo = AppTime.Now.ToString("yyyyMMddHHmmssfff");
            var occupyDate = ParseNullableDate(req.OccupyDate);
            var occupyDateIso = occupyDate?.ToString("yyyy-MM-dd") ?? req.OccupyDate;
            var occupyDateDt = occupyDate ?? new DateTime(1900, 1, 1);
            var createdOnIso = AppTime.Now.ToString("yyyy-MM-dd");

            const string sql = @"
                INSERT INTO Flat_Allocation
                    (ReferenceNo, Employee_PF, Employee_Name, Employee_Designation, Employee_Department,
                     Flat_Id, Flat_No, Flat_Name, Flat_Area, Flat_BHK_Details, Flat_Family_Details,
                     Society_Id, Society_Name, Society_Address, Society_Location_Id, Society_Location_Name,
                     Flat_Request_Status, Flat_Occupy_Date, Flat_Occupy_Date_Datetime,
                     Allotement_Letter_No, Transfer_Letter_No, Flat_Keys_Given, Vehicle_Details,
                     Emp_RO_Code, Emp_RO_Name, Emp_ZO_Code, Emp_ZO_Name, Emp_Posting_Date, Emp_Retirement_Date,
                     ManagedBy_Code, ManagedBy_Name,
                     Flat_Exit_Date, Flat_Exit_Date_Datetime, Flat_transferred_to, Flat_Keys_Back, Flat_Keys_BackTo,
                     Occupy_Status, Status, RemarkAllocate, RemarkDeallocate, Created_By, Created_On,
                     Extra1, Extra2, Extra3, Extra4, Extra5, Extra6, Extra7, Extra8, Extra9, Extra10,
                     Extra11, Extra12, Extra13, Extra14, Extra15)
                VALUES
                    (@RefNo, @Pf, @Name, @Designation, @Department,
                     @FlatId, @FlatNo, @FlatName, @FlatArea, @Bhk, @Family,
                     @SocietyId, @SocietyName, @SocietyAddress, @LocationId, @LocationName,
                     'Allocate', @OccupyDate, @OccupyDateDt,
                     @AllotmentLetterNo, @TransferLetterNo, @KeysGiven, @VehicleDetails,
                     @RoCode, @RoName, @ZoCode, @ZoName, @PostingDate, @RetirementDate,
                     @ManagedByCode, @ManagedByName,
                     NULL, NULL, NULL, NULL, NULL,
                     'Alloted', 'Active', @Remark, NULL, @CreatedBy, @CreatedOn,
                     @Email, @Phone, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
                     NULL, NULL, NULL, NULL, NULL)";

            await using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@RefNo", refNo);
                cmd.Parameters.AddWithValue("@Pf", req.EmployeePf);
                cmd.Parameters.AddWithValue("@Name", req.EmployeeName);
                cmd.Parameters.AddWithValue("@Designation", req.EmployeeDesignation);
                cmd.Parameters.AddWithValue("@Department", (object?)req.EmployeeDepartment ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FlatId", req.FlatId);
                cmd.Parameters.AddWithValue("@FlatNo", (object?)flat.FlatNo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FlatName", (object?)flat.LocationName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FlatArea", (object?)flat.FlatArea ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Bhk", (object?)flat.BhkDetails ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Family", (object?)flat.FamilyDetails ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SocietyId", (object?)flat.SocietyId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SocietyName", (object?)flat.SocietyName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SocietyAddress", (object?)flat.Address ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LocationId", (object?)flat.LocationId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LocationName", (object?)flat.LocationName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OccupyDate", occupyDateIso);
                cmd.Parameters.AddWithValue("@OccupyDateDt", occupyDateDt);
                cmd.Parameters.AddWithValue("@AllotmentLetterNo", (object?)req.AllotmentLetterNo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TransferLetterNo", (object?)req.TransferLetterNo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KeysGiven", req.KeysGiven);
                cmd.Parameters.AddWithValue("@VehicleDetails", (object?)req.VehicleDetails ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RoCode", (object?)req.RoCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RoName", (object?)req.RoName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ZoCode", (object?)req.ZoCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ZoName", (object?)req.ZoName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PostingDate", (object?)req.PostingDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RetirementDate", (object?)req.RetirementDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ManagedByCode", (object?)flat.ManagedByCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ManagedByName", (object?)flat.ManagedByName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Remark", (object?)req.Remark ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", (object?)req.EnteredBy ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedOn", createdOnIso);
                cmd.Parameters.AddWithValue("@Email", (object?)req.Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Phone", (object?)req.Phone ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }

            await RecomputeOccupancyAsync(cn, req.FlatId);

            return new FlatAllocationResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AllocateAsync failed for {Pf} / {FlatId}", req.EmployeePf, req.FlatId);
            return new FlatAllocationResult(false, UserFacingError.Generic);
        }
    }

    public async Task<List<string>> GetAllocatablePfListAsync(string flatId)
    {
        const string sql = @"
            SELECT DISTINCT Employee_PF
            FROM Flat_Allocation
            WHERE Occupy_Status = 'Alloted' AND Status = 'Active' AND Flat_Id = @FlatId
            ORDER BY Employee_PF";

        var result = new List<string>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (!r.IsDBNull(0)) result.Add(r.GetString(0));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAllocatablePfListAsync failed for {FlatId}", flatId);
        }
        return result;
    }

    public async Task<DeallocationCandidate?> GetOccupantByPfAsync(string pfNo)
    {
        const string sql = @"
            SELECT Employee_PF, Employee_Name, Employee_Designation, Flat_Occupy_Date, Extra1, Extra2
            FROM Flat_Allocation
            WHERE Employee_PF = @Pf";

        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Pf", pfNo);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new DeallocationCandidate(
                EmployeePf: r.GetString(0),
                EmployeeName: r.IsDBNull(1) ? null : r.GetString(1),
                EmployeeDesignation: r.IsDBNull(2) ? null : r.GetString(2),
                OccupyDate: r.IsDBNull(3) ? null : r.GetString(3),
                Email: r.IsDBNull(4) ? null : r.GetString(4),
                Phone: r.IsDBNull(5) ? null : r.GetString(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOccupantByPfAsync failed for {Pf}", pfNo);
            return null;
        }
    }

    public async Task<FlatAllocationResult> DeallocateAsync(FlatDeallocationRequest req)
    {
        try
        {
            await using var cn = new SqlConnection(_connString);
            await cn.OpenAsync();

            var exitDate = ParseNullableDate(req.ExitDate);
            var exitDateDt = exitDate ?? new DateTime(1900, 1, 1);
            var modifiedOnIso = AppTime.Now.ToString("yyyy-MM-dd");

            const string sql = @"
                UPDATE Flat_Allocation
                   SET Flat_Exit_Date = @ExitDate,
                       Flat_Exit_Date_Datetime = @ExitDateDt,
                       Flat_transferred_to = NULL,
                       Flat_Keys_Back = @KeysBack,
                       Flat_Keys_BackTo = @KeysBackTo,
                       Occupy_Status = 'Vacated',
                       Status = 'Deactive',
                       RemarkDeallocate = @Remark,
                       Modified_By = @ModifiedBy,
                       Modified_On = @ModifiedOn
                 WHERE Employee_PF = @Pf AND Flat_Id = @FlatId
                   AND Occupy_Status = 'Alloted' AND Status = 'Active'";

            await using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@ExitDate", req.ExitDate);
                cmd.Parameters.AddWithValue("@ExitDateDt", exitDateDt);
                cmd.Parameters.AddWithValue("@KeysBack", req.KeysBack);
                cmd.Parameters.AddWithValue("@KeysBackTo", (object?)req.KeysBackTo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Remark", (object?)req.Remark ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ModifiedBy", (object?)req.EnteredBy ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ModifiedOn", modifiedOnIso);
                cmd.Parameters.AddWithValue("@Pf", req.EmployeePf);
                cmd.Parameters.AddWithValue("@FlatId", req.FlatId);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return new FlatAllocationResult(false, "No active allocation found for this PF on this flat.");
            }

            await RecomputeOccupancyAsync(cn, req.FlatId);

            return new FlatAllocationResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeallocateAsync failed for {Pf} / {FlatId}", req.EmployeePf, req.FlatId);
            return new FlatAllocationResult(false, UserFacingError.Generic);
        }
    }

    public async Task<List<PreviousOccupantRow>> GetPreviousOccupantsAsync(string flatId)
    {
        const string sql = @"
            SELECT Employee_PF, Employee_Name, Employee_Designation, Employee_Department,
                   Flat_Occupy_Date, Flat_Exit_Date, Occupy_Status, Extra1, Extra2
            FROM Flat_Allocation
            WHERE Flat_Id = @FlatId AND Occupy_Status = 'Vacated' AND Status = 'Deactive'
            ORDER BY Flat_Exit_Date DESC";

        var result = new List<PreviousOccupantRow>();
        try
        {
            await using var cn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@FlatId", flatId);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new PreviousOccupantRow(
                    EmployeePf: r.IsDBNull(0) ? null : r.GetString(0),
                    EmployeeName: r.IsDBNull(1) ? null : r.GetString(1),
                    EmployeeDesignation: r.IsDBNull(2) ? null : r.GetString(2),
                    EmployeeDepartment: r.IsDBNull(3) ? null : r.GetString(3),
                    OccupyDate: r.IsDBNull(4) ? null : r.GetString(4),
                    ExitDate: r.IsDBNull(5) ? null : r.GetString(5),
                    OccupyStatus: r.IsDBNull(6) ? null : r.GetString(6),
                    Email: r.IsDBNull(7) ? null : r.GetString(7),
                    Phone: r.IsDBNull(8) ? null : r.GetString(8)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPreviousOccupantsAsync failed for {FlatId}", flatId);
        }
        return result;
    }

    private static async Task<FlatAllocationFlatInfo?> GetFlatInfoInternalAsync(SqlConnection cn, string flatId)
    {
        const string sql = @"
            SELECT Flat_No, Flat_Area, Flat_BHK_Details, Society_Id, Society_Name, Society_Address,
                   Society_Location_Id, Society_Location_Name, Flat_Family_Details,
                   Flat_Occupancy_Status, Flat_Occupant_Count, ManagedBy_Code, ManagedBy_Name
            FROM Flat_Details_Master
            WHERE Flat_Id = @FlatId";

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@FlatId", flatId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var occupantCountRaw = r.IsDBNull(10) ? "" : r.GetValue(10).ToString();
        int.TryParse(occupantCountRaw, out var occupantCount);

        return new FlatAllocationFlatInfo(
            FlatId: flatId,
            FlatNo: r.IsDBNull(0) ? null : r.GetString(0),
            FlatArea: r.IsDBNull(1) ? null : r.GetString(1),
            BhkDetails: r.IsDBNull(2) ? null : r.GetString(2),
            SocietyId: r.IsDBNull(3) ? null : r.GetString(3),
            SocietyName: r.IsDBNull(4) ? null : r.GetString(4),
            Address: r.IsDBNull(5) ? null : r.GetString(5),
            LocationId: r.IsDBNull(6) ? null : r.GetString(6),
            LocationName: r.IsDBNull(7) ? null : r.GetString(7),
            FamilyDetails: r.IsDBNull(8) ? null : r.GetString(8),
            OccupancyStatus: r.IsDBNull(9) ? null : r.GetString(9),
            OccupantCount: occupantCount,
            ManagedByCode: r.IsDBNull(11) ? null : r.GetString(11),
            ManagedByName: r.IsDBNull(12) ? null : r.GetString(12));
    }

    private static async Task<int> GetActiveOccupantCountAsync(SqlConnection cn, string flatId)
    {
        const string sql = "SELECT COUNT(*) FROM Flat_Allocation WHERE Flat_Id = @FlatId AND Occupy_Status = 'Alloted' AND Status = 'Active'";
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@FlatId", flatId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<string?> CheckExistingAllocationInternalAsync(SqlConnection cn, string pfNo)
    {
        const string sql = "SELECT Flat_No, Society_Name, Society_Location_Name FROM Flat_Allocation WHERE Employee_PF = @Pf AND Status = 'Active'";
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Pf", pfNo);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var flatNo = r.IsDBNull(0) ? "" : r.GetString(0);
        var societyName = r.IsDBNull(1) ? "" : r.GetString(1);
        var locationName = r.IsDBNull(2) ? "" : r.GetString(2);
        return $"Flat No.:{flatNo}, {societyName}, {locationName}";
    }

    private static async Task RecomputeOccupancyAsync(SqlConnection cn, string flatId)
    {
        var count = await GetActiveOccupantCountAsync(cn, flatId);
        var status = count == 0 ? "Vacant" : "Alloted";

        const string sql = "UPDATE Flat_Details_Master SET Flat_Occupant_Count = @Count, Flat_Occupancy_Status = @Status WHERE Flat_Id = @FlatId";
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Count", count.ToString());
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@FlatId", flatId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static DateTime? ParseNullableDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParseExact(s.Trim(), "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.TryParse(s, out var fallback) ? fallback : null;
    }

    private static string? SafeFormatDate(SqlDataReader r, int ordinal)
    {
        if (r.IsDBNull(ordinal)) return null;
        try
        {
            var value = r.GetValue(ordinal);
            if (value is DateTime dt)
                return dt <= new DateTime(1901, 1, 1) ? null : dt.ToString("dd/MM/yyyy");
            return DateTime.TryParse(value.ToString(), out var parsed)
                ? (parsed <= new DateTime(1901, 1, 1) ? null : parsed.ToString("dd/MM/yyyy"))
                : null;
        }
        catch
        {
            return null;
        }
    }
}
