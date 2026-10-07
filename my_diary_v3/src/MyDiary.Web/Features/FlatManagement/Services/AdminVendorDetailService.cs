using System.Text.RegularExpressions;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class AdminVendorDetailService : IAdminVendorDetailService
{
    private readonly string _connString;
    private readonly ILogger<AdminVendorDetailService> _logger;

    public AdminVendorDetailService(IConfiguration config, ILogger<AdminVendorDetailService> logger)
    {
        _connString = config.GetConnectionString("ConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ConStr in appsettings.json");
        _logger = logger;
    }

    public async Task<AdminVendorFormData?> GetVendorByIdAsync(string vendorId)
    {
        if (!int.TryParse(vendorId, out var vid)) return null;

        const string sql = @"
            SELECT VENDOR_NAME, VENDOR_MOBILE, VENDOR_ADDRESS, VENDOR_EMAIL, VENDOR_GST,
                   SPOC_NAME, SPOC_MOBILE, SPOC_EMAIL, STATUS
            FROM FLATS.VENDOR_MASTER
            WHERE VENDOR_ID = :VID";

        try
        {
            await using var cn = new OracleConnection(_connString);
            await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
            cmd.Parameters.Add("VID", OracleDbType.Int32).Value = vid;
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new AdminVendorFormData(
                VendorId: vendorId,
                VendorName: r.IsDBNull(0) ? null : r.GetString(0),
                VendorMobile: r.IsDBNull(1) ? null : r.GetString(1),
                VendorAddress: r.IsDBNull(2) ? null : r.GetString(2),
                VendorEmail: r.IsDBNull(3) ? null : r.GetString(3),
                VendorGst: r.IsDBNull(4) ? null : r.GetString(4),
                SpocName: r.IsDBNull(5) ? null : r.GetString(5),
                SpocMobile: r.IsDBNull(6) ? null : r.GetString(6),
                SpocEmail: r.IsDBNull(7) ? null : r.GetString(7),
                Status: r.IsDBNull(8) ? null : r.GetString(8));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetVendorByIdAsync failed for {VendorId}", vendorId);
            return null;
        }
    }

    public async Task<AdminVendorSaveResult> SaveVendorAsync(AdminVendorSaveRequest req)
    {
        var isEdit = !string.IsNullOrWhiteSpace(req.VendorId);
        var vMobDigits = Regex.Replace(req.VendorMobile, @"\D", "");
        var vGstin = (req.VendorGst ?? "").Trim().ToUpperInvariant();

        try
        {
            await using var cn = new OracleConnection(_connString);
            await cn.OpenAsync();

            int? vid = null;
            if (isEdit)
            {
                if (!int.TryParse(req.VendorId, out var parsed))
                    return new AdminVendorSaveResult(false, "Invalid vendor id.");
                vid = parsed;
            }

            const string dupSql = @"
                SELECT 1
                FROM FLATS.VENDOR_MASTER
                WHERE
                (
                    REPLACE(REPLACE(REPLACE(VENDOR_MOBILE, ' ', ''), '-', ''), '+', '')
                        LIKE '%' || :vMobDigits || '%'
                    OR (:vGSTIN IS NOT NULL AND VENDOR_GST = :vGSTIN)
                )
                AND (:VID IS NULL OR VENDOR_ID <> :VID)
                FETCH FIRST 1 ROWS ONLY";

            await using (var chk = new OracleCommand(dupSql, cn) { BindByName = true })
            {
                chk.Parameters.Add("vMobDigits", OracleDbType.NVarchar2, 200).Value = vMobDigits;
                chk.Parameters.Add("vGSTIN", OracleDbType.NVarchar2, 200).Value = string.IsNullOrWhiteSpace(vGstin) ? DBNull.Value : vGstin;
                chk.Parameters.Add("VID", OracleDbType.Int32).Value = vid.HasValue ? vid.Value : DBNull.Value;

                if (await chk.ExecuteScalarAsync() is not null)
                    return new AdminVendorSaveResult(false, "Vendor with same Mobile/GST already exists.");
            }

            const string mergeSql = @"
                MERGE INTO FLATS.VENDOR_MASTER t
                    USING (SELECT :VID AS VENDOR_ID, :VENDOR_CODE AS VENDOR_CODE FROM DUAL) s
                    ON (t.VENDOR_ID = s.VENDOR_ID)
                    WHEN MATCHED THEN
                    UPDATE SET
                        t.VENDOR_NAME    = :vName,
                        t.VENDOR_ADDRESS = :vAddr,
                        t.VENDOR_MOBILE  = :vMob,
                        t.VENDOR_GST     = :vGSTIN,
                        t.VENDOR_EMAIL   = :vEmailid,
                        t.SPOC_NAME      = :sName,
                        t.SPOC_MOBILE    = :sMob,
                        t.SPOC_EMAIL     = :sEmailid,
                        t.STATUS         = :vStatus,
                        t.MODIFIED_BY    = :P_BY,
                        t.MODIFIED_ON    = SYSDATE
                    WHEN NOT MATCHED THEN
                    INSERT (VENDOR_NAME, VENDOR_CODE, VENDOR_ADDRESS, VENDOR_MOBILE, VENDOR_GST, VENDOR_EMAIL,
                            SPOC_NAME, SPOC_MOBILE, SPOC_EMAIL, STATUS, CREATED_BY, CREATED_ON, MODIFIED_BY, MODIFIED_ON)
                    VALUES (:vName, :VENDOR_CODE, :vAddr, :vMob, :vGSTIN, :vEmailid,
                            :sName, :sMob, :sEmailid, :vStatus, :P_BY, SYSDATE, :P_BY, SYSDATE)";

            await using (var cmd = new OracleCommand(mergeSql, cn) { BindByName = true })
            {
                var vendorCode = isEdit ? null : GenerateVendorCode(req.VendorName, req.VendorMobile);

                cmd.Parameters.Add("VID", OracleDbType.Int32).Value = vid.HasValue ? vid.Value : DBNull.Value;
                cmd.Parameters.Add("VENDOR_CODE", OracleDbType.NVarchar2, 200).Value = (object?)vendorCode ?? DBNull.Value;
                cmd.Parameters.Add("vName", OracleDbType.NVarchar2, 200).Value = req.VendorName;
                cmd.Parameters.Add("vAddr", OracleDbType.NVarchar2, 500).Value = string.IsNullOrEmpty(req.VendorAddress) ? DBNull.Value : req.VendorAddress;
                cmd.Parameters.Add("vMob", OracleDbType.NVarchar2, 200).Value = req.VendorMobile;
                cmd.Parameters.Add("vGSTIN", OracleDbType.NVarchar2, 200).Value = string.IsNullOrEmpty(vGstin) ? DBNull.Value : vGstin;
                cmd.Parameters.Add("vEmailid", OracleDbType.NVarchar2, 200).Value = req.VendorEmail;
                cmd.Parameters.Add("sName", OracleDbType.NVarchar2, 200).Value = req.SpocName;
                cmd.Parameters.Add("sMob", OracleDbType.NVarchar2, 200).Value = req.SpocMobile;
                cmd.Parameters.Add("sEmailid", OracleDbType.NVarchar2, 200).Value = req.SpocEmail;
                cmd.Parameters.Add("vStatus", OracleDbType.NVarchar2, 200).Value = req.Status;
                cmd.Parameters.Add("P_BY", OracleDbType.NVarchar2, 200).Value = string.IsNullOrEmpty(req.EnteredBy) ? DBNull.Value : req.EnteredBy;

                await cmd.ExecuteNonQueryAsync();
            }

            return new AdminVendorSaveResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveVendorAsync failed for {VendorId}", req.VendorId);
            return new AdminVendorSaveResult(false, UserFacingError.Generic);
        }
    }

    private static string GenerateVendorCode(string vendorName, string mobile)
    {
        var letters = new string((vendorName ?? string.Empty)
            .Trim()
            .Where(char.IsLetter)
            .Select(char.ToUpperInvariant)
            .ToArray());

        letters = letters.Length >= 4 ? letters.Substring(0, 4) : letters.PadRight(4, 'X');

        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        var last4 = digits.Length >= 4 ? digits.Substring(digits.Length - 4) : digits.PadLeft(4, '0');

        return letters + last4;
    }
}
