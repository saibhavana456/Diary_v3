using Dapper;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using System.Data;
using System.Net;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class MasterRepo : IMasterRepo
{
    private readonly AdoUnitOfWork _uow;
    private readonly string _connString;
    public MasterRepo(AdoUnitOfWork uow, IConfiguration config)
    {
        _uow = uow;
        _connString = config.GetConnectionString("SQLServerConnection")
           ?? throw new InvalidOperationException("Missing 'SQLServerConnection' connection string.");
    }   

    // ── Request Types ─────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<RequestType>> ListRequestTypesAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID, CODE, NAME, IS_ACTIVE, ENTRY_MODE FROM RP_M_REQUEST_TYPE WHERE IS_ACTIVE=1 ORDER BY NAME", _uow.OracleTx);
        return await cmd.QueryAsync(MapRequestType, ct);
    }

    public async Task<IReadOnlyList<RequestType>> ListRequestTypesAllAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID, CODE, NAME, IS_ACTIVE, ENTRY_MODE FROM RP_M_REQUEST_TYPE ORDER BY NAME", _uow.OracleTx);
        return await cmd.QueryAsync(MapRequestType, ct);
    }

    public async Task<long> InsertRequestTypeAsync(RequestType m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_M_REQUEST_TYPE (ID,CODE,NAME,IS_ACTIVE,ENTRY_MODE)
              VALUES (RP_SEQ_GLOBAL.NEXTVAL,:p_code,:p_name,:p_active,:p_mode)
              RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_code", m.Code);
        cmd.Parameters.AddIn("p_name", m.Name);
        cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        cmd.Parameters.AddIn("p_mode", string.IsNullOrWhiteSpace(m.EntryMode) ? "BOTH" : m.EntryMode);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task UpdateRequestTypeAsync(RequestType m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_M_REQUEST_TYPE SET CODE=:p_code,NAME=:p_name,IS_ACTIVE=:p_active,ENTRY_MODE=:p_mode,UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_code", m.Code);
        cmd.Parameters.AddIn("p_name", m.Name);
        cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        cmd.Parameters.AddIn("p_mode", string.IsNullOrWhiteSpace(m.EntryMode) ? "BOTH" : m.EntryMode);
        cmd.Parameters.AddIn("p_id", m.Id);
        await cmd.ExecAsync(ct);
    }

    public async Task SetRequestTypeActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_M_REQUEST_TYPE SET IS_ACTIVE=:p_a,UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", isActive ? 1 : 0);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static RequestType MapRequestType(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), Code = Str(r, "CODE"), Name = Str(r, "NAME"), IsActive = Bool(r, "IS_ACTIVE"),
        EntryMode = string.IsNullOrWhiteSpace(StrN(r, "ENTRY_MODE")) ? "BOTH" : Str(r, "ENTRY_MODE")
    };

    // ── Unit Types ────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<UnitType>> ListUnitTypesAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,CODE,NAME,IS_ACTIVE FROM RP_M_UNIT_TYPE WHERE IS_ACTIVE=1 ORDER BY NAME", _uow.OracleTx);
        return await cmd.QueryAsync(MapUnitType, ct);
    }

    public async Task<IReadOnlyList<UnitType>> ListUnitTypesAllAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,CODE,NAME,IS_ACTIVE FROM RP_M_UNIT_TYPE ORDER BY NAME", _uow.OracleTx);
        return await cmd.QueryAsync(MapUnitType, ct);
    }

    public async Task<long> InsertUnitTypeAsync(UnitType m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_M_UNIT_TYPE(ID,CODE,NAME,IS_ACTIVE) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_code,:p_name,:p_active) RETURNING ID INTO :p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_code", m.Code);
        cmd.Parameters.AddIn("p_name", m.Name);
        cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task UpdateUnitTypeAsync(UnitType m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_M_UNIT_TYPE SET CODE=:p_code,NAME=:p_name,IS_ACTIVE=:p_active WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_code", m.Code); cmd.Parameters.AddIn("p_name", m.Name);
        cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0); cmd.Parameters.AddIn("p_id", m.Id);
        await cmd.ExecAsync(ct);
    }

    public async Task SetUnitTypeActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_UNIT_TYPE SET IS_ACTIVE=:p_a WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", isActive ? 1 : 0); cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static UnitType MapUnitType(OracleDataReader r) => new()
    { Id = Long(r, "ID"), Code = Str(r, "CODE"), Name = Str(r, "NAME"), IsActive = Bool(r, "IS_ACTIVE") };

    // ── Units ─────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<Unit>> ListUnitsAsync(long? unitTypeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT ID,UNIT_TYPE_ID,PARENT_UNIT_ID,CODE,NAME,HOLIDAY_CAL_ID,IS_ACTIVE
              FROM RP_M_UNIT WHERE IS_ACTIVE=1 AND (:p_type IS NULL OR UNIT_TYPE_ID=:p_type) ORDER BY NAME",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_type", OracleDbType.Int64, unitTypeId);
        return await cmd.QueryAsync(MapUnit, ct);
    }

    public async Task<IReadOnlyList<Unit>> ListUnitsAllAsync(long? unitTypeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT ID,UNIT_TYPE_ID,PARENT_UNIT_ID,CODE,NAME,HOLIDAY_CAL_ID,IS_ACTIVE
              FROM RP_M_UNIT WHERE (:p_type IS NULL OR UNIT_TYPE_ID=:p_type) ORDER BY NAME",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_type", OracleDbType.Int64, unitTypeId);
        return await cmd.QueryAsync(MapUnit, ct);
    }

    public async Task<long> InsertUnitAsync(Unit m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_M_UNIT(ID,UNIT_TYPE_ID,PARENT_UNIT_ID,CODE,NAME,HOLIDAY_CAL_ID,IS_ACTIVE)
              VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_ut,:p_parent,:p_code,:p_name,:p_cal,:p_active)
              RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_ut", m.UnitTypeId); cmd.Parameters.AddIn("p_parent", OracleDbType.Int64, m.ParentUnitId);
        cmd.Parameters.AddIn("p_code", m.Code); cmd.Parameters.AddIn("p_name", m.Name);
        cmd.Parameters.AddIn("p_cal", OracleDbType.Int64, m.HolidayCalId); cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task UpdateUnitAsync(Unit m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"UPDATE RP_M_UNIT SET UNIT_TYPE_ID=:p_ut,PARENT_UNIT_ID=:p_parent,CODE=:p_code,
              NAME=:p_name,HOLIDAY_CAL_ID=:p_cal,IS_ACTIVE=:p_active WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_ut", m.UnitTypeId); cmd.Parameters.AddIn("p_parent", OracleDbType.Int64, m.ParentUnitId);
        cmd.Parameters.AddIn("p_code", m.Code); cmd.Parameters.AddIn("p_name", m.Name);
        cmd.Parameters.AddIn("p_cal", OracleDbType.Int64, m.HolidayCalId); cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        cmd.Parameters.AddIn("p_id", m.Id);
        await cmd.ExecAsync(ct);
    }

    public async Task SetUnitActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_UNIT SET IS_ACTIVE=:p_a WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", isActive ? 1 : 0); cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static Unit MapUnit(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), UnitTypeId = Long(r, "UNIT_TYPE_ID"), ParentUnitId = LongN(r, "PARENT_UNIT_ID"),
        Code = Str(r, "CODE"), Name = Str(r, "NAME"), HolidayCalId = LongN(r, "HOLIDAY_CAL_ID"), IsActive = Bool(r, "IS_ACTIVE")
    };

    // ── Verticals ─────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<Vertical>> ListVerticalsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("SELECT ID,CODE,NAME,IS_ACTIVE FROM RP_M_VERTICAL WHERE IS_ACTIVE=1 AND TYPE = '1021' ORDER BY NAME", _uow.OracleTx);
        return await cmd.QueryAsync(MapVertical, ct);
    }

    public async Task<IReadOnlyList<Vertical>> ListVerticalsAllAsync(CancellationToken ct = default)
    {
       
            await _uow.EnsureConnectionAsync(ct);
            using var cmd = _uow.OracleConn.Cmd("SELECT ID,CODE,NAME,IS_ACTIVE FROM RP_M_VERTICAL WHERE TYPE = '1021' ORDER BY NAME", _uow.OracleTx);
            return await cmd.QueryAsync(MapVertical, ct);
    }

    public async Task<IReadOnlyList<Vertical>> ListVerticalsAllAsyncRequest(string unit,CancellationToken ct = default)
    {        
        //if (unit == "1021")
        //{
            await _uow.EnsureConnectionAsync(ct);
            using var cmd = _uow.OracleConn.Cmd("SELECT ID,CODE,NAME,IS_ACTIVE FROM RP_M_VERTICAL WHERE type =: type ORDER BY NAME", _uow.OracleTx);
            cmd.Parameters.AddIn("type", unit);
            return await cmd.QueryAsync(MapVertical, ct);
        //}
        //else
        //{
        //    var result = new List<Vertical>();

        //    await using var conn = new SqlConnection(_connString);
        //    await conn.OpenAsync(ct);

        //    await using var cmd = new SqlCommand("sp_ListVerticalsAll", conn)
        //    {
        //        CommandType = CommandType.StoredProcedure
        //    };

        //    cmd.Parameters.Add("@Unit", SqlDbType.NVarChar, 50).Value = unit;

        //    await using var rdr = await cmd.ExecuteReaderAsync(ct);

        //    while (await rdr.ReadAsync(ct))
        //    {
        //        result.Add(new Vertical
        //        {
        //            Id = Convert.ToInt32(rdr["ID"]),
        //            Code = rdr["CODE"]?.ToString(),
        //            Name = rdr["NAME"]?.ToString(),
        //            IsActive = Convert.ToBoolean(rdr["IS_ACTIVE"])
        //        });
        //    }

        //    return result;
        //}

    }
    public async Task<long> InsertVerticalAsync(Vertical m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_M_VERTICAL(ID,CODE,NAME,IS_ACTIVE) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_code,:p_name,:p_active) RETURNING ID INTO :p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_code", m.Code); cmd.Parameters.AddIn("p_name", m.Name);
        cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task UpdateVerticalAsync(Vertical m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_VERTICAL SET CODE=:p_code,NAME=:p_name,IS_ACTIVE=:p_active WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_code", m.Code); cmd.Parameters.AddIn("p_name", m.Name);
        cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0); cmd.Parameters.AddIn("p_id", m.Id);
        await cmd.ExecAsync(ct);
    }

    public async Task SetVerticalActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_VERTICAL SET IS_ACTIVE=:p_a WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", isActive ? 1 : 0); cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static Vertical MapVertical(OracleDataReader r) => new()
    { Id = Long(r, "ID"), Code = Str(r, "CODE"), Name = Str(r, "NAME"), IsActive = Bool(r, "IS_ACTIVE") };

    // ── Departments ───────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<Department>> ListDepartmentsAsync(long? verticalId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,VERTICAL_ID,CODE,NAME,IS_ACTIVE FROM RP_M_DEPARTMENT WHERE IS_ACTIVE=1 AND (:p_v IS NULL OR VERTICAL_ID=:p_v) ORDER BY NAME",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_v", OracleDbType.Int64, verticalId);
        return await cmd.QueryAsync(MapDept, ct);
    }

    public async Task<IReadOnlyList<Department>> ListDepartmentsAllAsync(long? verticalId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,VERTICAL_ID,CODE,NAME,IS_ACTIVE FROM RP_M_DEPARTMENT WHERE (:p_v IS NULL OR VERTICAL_ID=:p_v) ORDER BY NAME",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_v", OracleDbType.Int64, verticalId);
        return await cmd.QueryAsync(MapDept, ct);
    }

    public async Task<long> InsertDepartmentAsync(Department m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_M_DEPARTMENT(ID,VERTICAL_ID,CODE,NAME,IS_ACTIVE) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_v,:p_code,:p_name,:p_active) RETURNING ID INTO :p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_v", m.VerticalId); cmd.Parameters.AddIn("p_code", m.Code);
        cmd.Parameters.AddIn("p_name", m.Name); cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task UpdateDepartmentAsync(Department m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_M_DEPARTMENT SET VERTICAL_ID=:p_v,CODE=:p_code,NAME=:p_name,IS_ACTIVE=:p_active WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_v", m.VerticalId); cmd.Parameters.AddIn("p_code", m.Code);
        cmd.Parameters.AddIn("p_name", m.Name); cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        cmd.Parameters.AddIn("p_id", m.Id); await cmd.ExecAsync(ct);
    }

    public async Task SetDepartmentActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_DEPARTMENT SET IS_ACTIVE=:p_a WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", isActive ? 1 : 0); cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static Department MapDept(OracleDataReader r) => new()
    { Id = Long(r, "ID"), VerticalId = Long(r, "VERTICAL_ID"), Code = Str(r, "CODE"), Name = Str(r, "NAME"), IsActive = Bool(r, "IS_ACTIVE") };

    // ── Activities ────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<Activity>> ListActivitiesAsync(long? departmentId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,DEPARTMENT_ID,CODE,NAME,IS_ACTIVE FROM RP_M_ACTIVITY WHERE IS_ACTIVE=1 AND (:p_d IS NULL OR DEPARTMENT_ID=:p_d) ORDER BY NAME",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_d", OracleDbType.Int64, departmentId);
        return await cmd.QueryAsync(MapActivity, ct);
    }

    public async Task<IReadOnlyList<Activity>> ListActivitiesAllAsync(long? departmentId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,DEPARTMENT_ID,CODE,NAME,IS_ACTIVE FROM RP_M_ACTIVITY WHERE (:p_d IS NULL OR DEPARTMENT_ID=:p_d) ORDER BY NAME",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_d", OracleDbType.Int64, departmentId);
        return await cmd.QueryAsync(MapActivity, ct);
    }

    public async Task<long> InsertActivityAsync(Activity m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_M_ACTIVITY(ID,DEPARTMENT_ID,CODE,NAME,IS_ACTIVE) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_d,:p_code,:p_name,:p_active) RETURNING ID INTO :p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_d", m.DepartmentId); cmd.Parameters.AddIn("p_code", m.Code);
        cmd.Parameters.AddIn("p_name", m.Name); cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task UpdateActivityAsync(Activity m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_M_ACTIVITY SET DEPARTMENT_ID=:p_d,CODE=:p_code,NAME=:p_name,IS_ACTIVE=:p_active WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_d", m.DepartmentId); cmd.Parameters.AddIn("p_code", m.Code);
        cmd.Parameters.AddIn("p_name", m.Name); cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        cmd.Parameters.AddIn("p_id", m.Id); await cmd.ExecAsync(ct);
    }

    public async Task SetActivityActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_ACTIVITY SET IS_ACTIVE=:p_a WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", isActive ? 1 : 0); cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static Activity MapActivity(OracleDataReader r) => new()
    { Id = Long(r, "ID"), DepartmentId = Long(r, "DEPARTMENT_ID"), Code = Str(r, "CODE"), Name = Str(r, "NAME"), IsActive = Bool(r, "IS_ACTIVE") };

    // ── SLA ───────────────────────────────────────────────────────────────────
    public async Task<int> GetSlaWorkingDaysAsync(long requestTypeId, int levelNo, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT WORKING_DAYS FROM RP_M_SLA_CONFIG WHERE REQUEST_TYPE_ID=:p_rt AND LEVEL_NO=:p_lvl", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rt", requestTypeId); cmd.Parameters.AddIn("p_lvl", levelNo);
        return await cmd.ScalarAsync<int?>(ct) ?? 2;
    }

    public async Task<IReadOnlyList<DateTime>> GetHolidaysAsync(long calendarId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT HOLIDAY_DATE FROM RP_M_HOLIDAY WHERE CAL_ID=:p_id AND HOLIDAY_DATE BETWEEN :p_from AND :p_to",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", calendarId);
        cmd.Parameters.AddIn("p_from", fromUtc.Date);
        cmd.Parameters.AddIn("p_to", toUtc.Date);
        return await cmd.QueryAsync(r => Dt(r, "HOLIDAY_DATE"), ct);
    }

    public async Task<IReadOnlyList<SlaConfig>> ListSlaConfigAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,REQUEST_TYPE_ID,LEVEL_NO,WORKING_DAYS FROM RP_M_SLA_CONFIG ORDER BY REQUEST_TYPE_ID,LEVEL_NO", _uow.OracleTx);
        return await cmd.QueryAsync(r => new SlaConfig
        {
            Id = Long(r, "ID"), RequestTypeId = Long(r, "REQUEST_TYPE_ID"),
            LevelNo = Int(r, "LEVEL_NO"), WorkingDays = Int(r, "WORKING_DAYS")
        }, ct);
    }

    public async Task<long> InsertSlaConfigAsync(SlaConfig m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_M_SLA_CONFIG(ID,REQUEST_TYPE_ID,LEVEL_NO,WORKING_DAYS) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rt,:p_lvl,:p_days) RETURNING ID INTO :p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_rt", m.RequestTypeId); cmd.Parameters.AddIn("p_lvl", m.LevelNo);
        cmd.Parameters.AddIn("p_days", m.WorkingDays);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task UpdateSlaConfigAsync(SlaConfig m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_M_SLA_CONFIG SET REQUEST_TYPE_ID=:p_rt,LEVEL_NO=:p_lvl,WORKING_DAYS=:p_days WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rt", m.RequestTypeId); cmd.Parameters.AddIn("p_lvl", m.LevelNo);
        cmd.Parameters.AddIn("p_days", m.WorkingDays); cmd.Parameters.AddIn("p_id", m.Id);
        await cmd.ExecAsync(ct);
    }

    public async Task DeleteSlaConfigAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("DELETE FROM RP_M_SLA_CONFIG WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    // ── Holiday Calendar ──────────────────────────────────────────────────────
    public async Task<IReadOnlyList<HolidayCalendar>> ListHolidayCalendarsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("SELECT ID,CODE,NAME FROM RP_M_HOLIDAY_CAL ORDER BY NAME", _uow.OracleTx);
        return await cmd.QueryAsync(r => new HolidayCalendar { Id = Long(r, "ID"), Code = Str(r, "CODE"), Name = Str(r, "NAME") }, ct);
    }

    public async Task<long> InsertHolidayCalendarAsync(HolidayCalendar m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_M_HOLIDAY_CAL(ID,CODE,NAME) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_code,:p_name) RETURNING ID INTO :p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_code", m.Code); cmd.Parameters.AddIn("p_name", m.Name);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task UpdateHolidayCalendarAsync(HolidayCalendar m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_HOLIDAY_CAL SET CODE=:p_code,NAME=:p_name WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_code", m.Code); cmd.Parameters.AddIn("p_name", m.Name); cmd.Parameters.AddIn("p_id", m.Id);
        await cmd.ExecAsync(ct);
    }

    public async Task DeleteHolidayCalendarAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd1 = _uow.OracleConn.Cmd("DELETE FROM RP_M_HOLIDAY WHERE CAL_ID=:p_id", _uow.OracleTx);
        cmd1.Parameters.AddIn("p_id", id); await cmd1.ExecAsync(ct);
        using var cmd2 = _uow.OracleConn.Cmd("DELETE FROM RP_M_HOLIDAY_CAL WHERE ID=:p_id", _uow.OracleTx);
        cmd2.Parameters.AddIn("p_id", id); await cmd2.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<Holiday>> ListHolidaysForAdminAsync(long calId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,CAL_ID,HOLIDAY_DATE,DESCRIPTION FROM RP_M_HOLIDAY WHERE CAL_ID=:p_id ORDER BY HOLIDAY_DATE", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", calId);
        return await cmd.QueryAsync(r => new Holiday
        {
            Id = Long(r, "ID"), CalId = Long(r, "CAL_ID"),
            HolidayDate = Dt(r, "HOLIDAY_DATE"), Description = StrN(r, "DESCRIPTION")
        }, ct);
    }

    public async Task<long> InsertHolidayAsync(Holiday h, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_M_HOLIDAY(ID,CAL_ID,HOLIDAY_DATE,DESCRIPTION) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_cal,:p_date,:p_desc) RETURNING ID INTO :p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_cal", h.CalId); cmd.Parameters.AddIn("p_date", h.HolidayDate.Date);
        cmd.Parameters.AddIn("p_desc", h.Description);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task UpdateHolidayAsync(Holiday h, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_HOLIDAY SET HOLIDAY_DATE=:p_date,DESCRIPTION=:p_desc WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_date", h.HolidayDate.Date); cmd.Parameters.AddIn("p_desc", h.Description);
        cmd.Parameters.AddIn("p_id", h.Id); await cmd.ExecAsync(ct);
    }

    public async Task DeleteHolidayAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("DELETE FROM RP_M_HOLIDAY WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }
}
