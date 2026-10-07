using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class AdminUserDetailService : IAdminUserDetailService
{
    private readonly string _connString;
    private readonly ILogger<AdminUserDetailService> _logger;

    public AdminUserDetailService(IConfiguration config, ILogger<AdminUserDetailService> logger)
    {
        _connString = config.GetConnectionString("ConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ConStr in appsettings.json");
        _logger = logger;
    }

    public async Task<AdminUserFormData?> GetAdminByEmpIdAsync(string empId)
    {
        const string sql = @"
            SELECT EMP_ID, NAME, ADMINSCOPETYPE, ADMINSCOPECODE, ADMINSCOPENAME, JOB_DESC, CONTACT_NO,
                   EMAIL_ID, REGION_CODE, REGION_NAME, ZONE_CODE, ZONE_NAME, DEPT_DESC
            FROM VW_USER_MASTER
            WHERE EMP_ID = :EMP_ID
              AND USER_TYPE = 'admin'";

        try
        {
            await using var cn = new OracleConnection(_connString);
            await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
            cmd.Parameters.Add("EMP_ID", OracleDbType.Varchar2).Value = empId.Trim();
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new AdminUserFormData(
                PfNo: r.GetString(0),
                Name: r.IsDBNull(1) ? null : r.GetString(1),
                ScopeType: r.IsDBNull(2) ? null : r.GetString(2),
                ScopeCode: r.IsDBNull(3) ? null : r.GetString(3),
                ScopeName: r.IsDBNull(4) ? null : r.GetString(4),
                Designation: r.IsDBNull(5) ? null : r.GetString(5),
                Phone: r.IsDBNull(6) ? null : r.GetString(6),
                Email: r.IsDBNull(7) ? null : r.GetString(7),
                RoCode: r.IsDBNull(8) ? null : r.GetString(8),
                RoName: r.IsDBNull(9) ? null : r.GetString(9),
                ZoCode: r.IsDBNull(10) ? null : r.GetString(10),
                ZoName: r.IsDBNull(11) ? null : r.GetString(11),
                Department: r.IsDBNull(12) ? null : r.GetString(12));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAdminByEmpIdAsync failed for {EmpId}", empId);
            return null;
        }
    }

    public async Task<AdminUserFormData?> GetStaffByPfAsync(string pfNo)
    {
        const string sql = @"
            SELECT PF_NO, NAME, DEPT_ID, DEPT_DESC, REGION_CODE, REGION_NAME, ZONE_CODE, ZONE_NAME,
                   JOB_DESC, CONTACT_NO, EMAIL_ID
            FROM ORGANISATION.STAFF_DETAILS
            WHERE PF_NO = :PF";

        try
        {
            await using var cn = new OracleConnection(_connString);
            await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
            cmd.Parameters.Add("PF", OracleDbType.Varchar2).Value = pfNo.Trim();
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new AdminUserFormData(
                PfNo: r.GetString(0),
                Name: r.IsDBNull(1) ? null : r.GetString(1),
                Department: r.IsDBNull(3) ? null : r.GetString(3),
                RoCode: r.IsDBNull(4) ? null : r.GetString(4),
                RoName: r.IsDBNull(5) ? null : r.GetString(5),
                ZoCode: r.IsDBNull(6) ? null : r.GetString(6),
                ZoName: r.IsDBNull(7) ? null : r.GetString(7),
                Designation: r.IsDBNull(8) ? null : r.GetString(8),
                Phone: r.IsDBNull(9) ? null : r.GetString(9),
                Email: r.IsDBNull(10) ? null : r.GetString(10),
                ScopeType: null,
                ScopeCode: null,
                ScopeName: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStaffByPfAsync failed for {PfNo}", pfNo);
            return null;
        }
    }

    public async Task<AdminUserSaveResult> SaveAdminAsync(AdminUserSaveRequest req)
    {
        const string sql = @"
            MERGE INTO FLATS.USER_MASTER tgt
                USING (SELECT :EMP_ID AS EMP_ID FROM DUAL) src
                ON (tgt.EMP_ID = src.EMP_ID)
                WHEN MATCHED THEN
                UPDATE SET
                    tgt.EMP_NAME       = :EMP_NAME,
                    tgt.USER_TYPE      = 'admin',
                    tgt.STATUS         = :STATUS,
                    tgt.MODIFY_BY      = :ENTRY_BY,
                    tgt.MODIFY_DATE    = SYSDATE,
                    tgt.ADMINSCOPETYPE = :SCOPE,
                    tgt.ADMINSCOPECODE = :SCOPE_CODE,
                    tgt.ADMINSCOPENAME = :SCOPE_NAME,
                    tgt.PHONE          = :PHONE,
                    tgt.EMAIL          = :EMAIL,
                    tgt.DESIGNATION    = :DESIGNATION
                WHEN NOT MATCHED THEN
                INSERT (EMP_ID, EMP_NAME, USER_TYPE, STATUS, ENTRY_BY, ENTRY_DATE,
                        ADMINSCOPETYPE, ADMINSCOPECODE, ADMINSCOPENAME, PHONE, EMAIL, DESIGNATION)
                VALUES (:EMP_ID, :EMP_NAME, 'admin', :STATUS, :ENTRY_BY, SYSDATE,
                        :SCOPE, :SCOPE_CODE, :SCOPE_NAME, :PHONE, :EMAIL, :DESIGNATION)";

        try
        {
            await using var cn = new OracleConnection(_connString);
            await using var cmd = new OracleCommand(sql, cn) { BindByName = true };

            cmd.Parameters.Add("EMP_ID", OracleDbType.Varchar2).Value = req.EmpId.Trim();
            cmd.Parameters.Add("EMP_NAME", OracleDbType.Varchar2).Value = (object?)req.Name ?? DBNull.Value;
            cmd.Parameters.Add("STATUS", OracleDbType.Varchar2).Value = req.Status;
            cmd.Parameters.Add("ENTRY_BY", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(req.EnteredBy) ? DBNull.Value : req.EnteredBy;
            cmd.Parameters.Add("SCOPE", OracleDbType.Varchar2).Value = req.Scope;
            cmd.Parameters.Add("SCOPE_CODE", OracleDbType.Varchar2).Value = req.ScopeCode.Trim();
            cmd.Parameters.Add("SCOPE_NAME", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(req.ScopeName) ? DBNull.Value : req.ScopeName;
            cmd.Parameters.Add("PHONE", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(req.Phone) ? DBNull.Value : req.Phone;
            cmd.Parameters.Add("EMAIL", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(req.Email) ? DBNull.Value : req.Email;
            cmd.Parameters.Add("DESIGNATION", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(req.Designation) ? DBNull.Value : req.Designation;

            await cn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return new AdminUserSaveResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveAdminAsync failed for {EmpId}", req.EmpId);
            return new AdminUserSaveResult(false, UserFacingError.Generic);
        }
    }
}
