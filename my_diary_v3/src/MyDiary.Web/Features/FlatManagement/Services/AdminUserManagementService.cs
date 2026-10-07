using System.Text;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class AdminUserManagementService : IAdminUserManagementService
{
    private readonly string _connString;
    private readonly ILogger<AdminUserManagementService> _logger;

    public AdminUserManagementService(IConfiguration config, ILogger<AdminUserManagementService> logger)
    {
        _connString = config.GetConnectionString("ConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ConStr in appsettings.json");
        _logger = logger;
    }

    public async Task<AdminUserListResult> GetUsersAsync(string? empPfOrName, string? location, int pageIndex, int pageSize)
    {
        var result = new List<AdminUserRow>();
        try
        {
            await using var cn = new OracleConnection(_connString);
            await cn.OpenAsync();

            var where = new StringBuilder(" FROM VW_USER_MASTER WHERE 1 = 1");

            var pf = (empPfOrName ?? string.Empty).Trim();
            var loc = (location ?? string.Empty).Trim();

            if (!string.IsNullOrEmpty(pf)) where.Append(" AND (EMP_ID = :EMP_ID OR UPPER(NAME) LIKE UPPER(:EMP_NAME))");
            if (!string.IsNullOrEmpty(loc)) where.Append(" AND UPPER(LOCATION_DESC) LIKE UPPER(:LOCATION)");

            void BindFilters(OracleCommand c)
            {
                if (!string.IsNullOrEmpty(pf))
                {
                    c.Parameters.Add("EMP_ID", OracleDbType.Varchar2).Value = pf;
                    c.Parameters.Add("EMP_NAME", OracleDbType.Varchar2).Value = "%" + pf + "%";
                }
                if (!string.IsNullOrEmpty(loc))
                    c.Parameters.Add("LOCATION", OracleDbType.Varchar2).Value = "%" + loc + "%";
            }

            int totalCount;
            await using (var countCmd = new OracleCommand("SELECT COUNT(*)" + where, cn) { BindByName = true })
            {
                BindFilters(countCmd);
                totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            var listSql = @"
                SELECT EMP_ID, NAME, USER_TYPE, SCALE_DESC, JOB_DESC, LOCATION_DESC, CONTACT_NO, EMAIL_ID"
                + where + @"
                ORDER BY SCALE_DESC, EMP_ID
                OFFSET :OFFSET_ROWS ROWS FETCH NEXT :PAGE_SIZE ROWS ONLY";

            await using (var listCmd = new OracleCommand(listSql, cn) { BindByName = true })
            {
                BindFilters(listCmd);
                listCmd.Parameters.Add("OFFSET_ROWS", OracleDbType.Int32).Value = Math.Max(0, pageIndex) * pageSize;
                listCmd.Parameters.Add("PAGE_SIZE", OracleDbType.Int32).Value = pageSize;

                await using var r = await listCmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    result.Add(new AdminUserRow(
                        EmpId: r.GetString(0),
                        Name: r.IsDBNull(1) ? null : r.GetString(1),
                        UserType: r.IsDBNull(2) ? null : r.GetString(2),
                        ScaleDesc: r.IsDBNull(3) ? null : r.GetString(3),
                        JobDesc: r.IsDBNull(4) ? null : r.GetString(4),
                        LocationDesc: r.IsDBNull(5) ? null : r.GetString(5),
                        ContactNo: r.IsDBNull(6) ? null : r.GetString(6),
                        EmailId: r.IsDBNull(7) ? null : r.GetString(7)));
                }
            }

            return new AdminUserListResult(result, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetUsersAsync failed");
            return new AdminUserListResult(result, 0);
        }
    }

    public async Task<AdminVendorListResult> GetVendorsAsync(string? vendorCodeOrName, string? location, int pageIndex, int pageSize)
    {
        var result = new List<AdminVendorRow>();
        try
        {
            await using var cn = new OracleConnection(_connString);
            await cn.OpenAsync();

            var where = new StringBuilder(" FROM VENDOR_MASTER WHERE 1 = 1");

            var vc = (vendorCodeOrName ?? string.Empty).Trim();
            var loc = (location ?? string.Empty).Trim();

            if (!string.IsNullOrEmpty(vc)) where.Append(" AND (VENDOR_CODE = :VCODE OR UPPER(VENDOR_NAME) LIKE UPPER(:VNAME))");
            if (!string.IsNullOrEmpty(loc)) where.Append(" AND UPPER(VENDOR_ADDRESS) LIKE UPPER(:ADDR)");

            void BindFilters(OracleCommand c)
            {
                if (!string.IsNullOrEmpty(vc))
                {
                    c.Parameters.Add("VCODE", OracleDbType.Varchar2).Value = vc;
                    c.Parameters.Add("VNAME", OracleDbType.Varchar2).Value = "%" + vc + "%";
                }
                if (!string.IsNullOrEmpty(loc))
                    c.Parameters.Add("ADDR", OracleDbType.Varchar2).Value = "%" + loc + "%";
            }

            int totalCount;
            await using (var countCmd = new OracleCommand("SELECT COUNT(*)" + where, cn) { BindByName = true })
            {
                BindFilters(countCmd);
                totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            var listSql = @"
                SELECT VENDOR_ID, VENDOR_CODE, VENDOR_NAME, VENDOR_ADDRESS, VENDOR_MOBILE, VENDOR_EMAIL, STATUS, CREATED_ON"
                + where + @"
                ORDER BY CREATED_ON DESC
                OFFSET :OFFSET_ROWS ROWS FETCH NEXT :PAGE_SIZE ROWS ONLY";

            await using (var listCmd = new OracleCommand(listSql, cn) { BindByName = true })
            {
                BindFilters(listCmd);
                listCmd.Parameters.Add("OFFSET_ROWS", OracleDbType.Int32).Value = Math.Max(0, pageIndex) * pageSize;
                listCmd.Parameters.Add("PAGE_SIZE", OracleDbType.Int32).Value = pageSize;

                await using var r = await listCmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    result.Add(new AdminVendorRow(
                        VendorId: r.GetString(0),
                        VendorCode: r.IsDBNull(1) ? null : r.GetString(1),
                        VendorName: r.IsDBNull(2) ? null : r.GetString(2),
                        VendorAddress: r.IsDBNull(3) ? null : r.GetString(3),
                        VendorMobile: r.IsDBNull(4) ? null : r.GetString(4),
                        VendorEmail: r.IsDBNull(5) ? null : r.GetString(5),
                        Status: r.IsDBNull(6) ? null : r.GetString(6),
                        CreatedOn: SafeGetDateTime(r, 7)));
                }
            }

            return new AdminVendorListResult(result, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetVendorsAsync failed");
            return new AdminVendorListResult(result, 0);
        }
    }

    private static DateTime? SafeGetDateTime(OracleDataReader r, int ordinal)
    {
        if (r.IsDBNull(ordinal)) return null;
        try
        {
            return r.GetDateTime(ordinal);
        }
        catch
        {
            try
            {
                var raw = r.GetValue(ordinal);
                if (raw is DateTime dt) return dt;
                if (raw is not null && DateTime.TryParse(raw.ToString(), out var parsed)) return parsed;
            }
            catch
            {
                
            }
            return null;
        }
    }
}
