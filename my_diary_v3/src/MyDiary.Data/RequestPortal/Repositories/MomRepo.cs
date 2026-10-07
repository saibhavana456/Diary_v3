using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class MomRepo : IMomRepo
{
    private readonly AdoUnitOfWork _uow;
    public MomRepo(AdoUnitOfWork uow) => _uow = uow;

    // ── MoM Types ────────────────────────────────────────────────────────────
    private const string TypeSelectCols = @"
        t.ID, t.VERTICAL_ID, v.NAME AS VNAME, t.TYPE_NAME, t.TYPE_CODE, t.DESCRIPTION,
        t.ENABLE_ZONE_REGION_SCOPE, t.ENABLE_DATE_RANGE, t.ENABLE_MEETING_TYPE, t.ENABLE_QUARTER, t.ENABLE_YEAR,
        t.MEETING_TYPE_LABEL, t.MAX_SUPPORTING_FILES, t.ALLOWED_FILE_TYPES, t.STORAGE_ROOT_PATH,
        t.REQUIRE_PRIMARY_FILE, t.ICON_NAME, t.SORT_ORDER, t.IS_ACTIVE, t.CREATED_BY_EMP, t.CREATED_AT, t.UPDATED_AT";

    public async Task<IReadOnlyList<MomType>> ListTypesAsync(long? verticalId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var sql = $@"SELECT {TypeSelectCols}
                    FROM RP_M_MOM_TYPE t
                    JOIN RP_M_VERTICAL v ON v.ID = t.VERTICAL_ID
                    WHERE 1=1";
        if (verticalId.HasValue) sql += " AND t.VERTICAL_ID=:p_vid";
        sql += " ORDER BY t.SORT_ORDER, t.TYPE_NAME";

        using var cmd = _uow.OracleConn.Cmd(sql, _uow.OracleTx);
        if (verticalId.HasValue) cmd.Parameters.AddIn("p_vid", verticalId.Value);
        return await cmd.QueryAsync(MapType, ct);
    }

    public async Task<MomType?> GetTypeAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            $@"SELECT {TypeSelectCols}
              FROM RP_M_MOM_TYPE t
              JOIN RP_M_VERTICAL v ON v.ID = t.VERTICAL_ID
              WHERE t.ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        return await cmd.QueryOneAsync(MapType, ct);
    }

    public async Task<long> InsertTypeAsync(MomTypeForm f, string createdByEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_M_MOM_TYPE
                (VERTICAL_ID, TYPE_NAME, TYPE_CODE, DESCRIPTION,
                 ENABLE_ZONE_REGION_SCOPE, ENABLE_DATE_RANGE, ENABLE_MEETING_TYPE, ENABLE_QUARTER, ENABLE_YEAR,
                 MEETING_TYPE_LABEL, MAX_SUPPORTING_FILES, ALLOWED_FILE_TYPES, STORAGE_ROOT_PATH,
                 REQUIRE_PRIMARY_FILE, ICON_NAME, SORT_ORDER, CREATED_BY_EMP)
              VALUES
                (:p_vid,:p_name,:p_code,:p_desc,
                 :p_zone,:p_daterange,:p_mtg,:p_qtr,:p_year,
                 :p_mtglbl,:p_maxfiles,:p_ftypes,:p_root,
                 :p_reqprim,:p_icon,:p_sort,:p_emp)
              RETURNING ID INTO :p_id", _uow.OracleTx);
        BindTypeForm(cmd, f);
        cmd.Parameters.AddIn("p_emp", createdByEmp);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task UpdateTypeAsync(long id, MomTypeForm f, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"UPDATE RP_M_MOM_TYPE SET
                VERTICAL_ID=:p_vid, TYPE_NAME=:p_name, TYPE_CODE=:p_code, DESCRIPTION=:p_desc,
                ENABLE_ZONE_REGION_SCOPE=:p_zone, ENABLE_DATE_RANGE=:p_daterange, ENABLE_MEETING_TYPE=:p_mtg,
                ENABLE_QUARTER=:p_qtr, ENABLE_YEAR=:p_year,
                MEETING_TYPE_LABEL=:p_mtglbl, MAX_SUPPORTING_FILES=:p_maxfiles, ALLOWED_FILE_TYPES=:p_ftypes,
                STORAGE_ROOT_PATH=:p_root, REQUIRE_PRIMARY_FILE=:p_reqprim, ICON_NAME=:p_icon, SORT_ORDER=:p_sort,
                UPDATED_AT=SYSTIMESTAMP
              WHERE ID=:p_id", _uow.OracleTx);
        BindTypeForm(cmd, f);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    public async Task SetTypeActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_MOM_TYPE SET IS_ACTIVE=:p_a WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", isActive ? 1 : 0);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static void BindTypeForm(OracleCommand cmd, MomTypeForm f)
    {
        cmd.Parameters.AddIn("p_vid", f.VerticalId!.Value);
        cmd.Parameters.AddIn("p_name", f.TypeName);
        cmd.Parameters.AddIn("p_code", f.TypeCode);
        cmd.Parameters.AddIn("p_desc", f.Description);
        cmd.Parameters.AddIn("p_zone", f.EnableZoneRegionScope ? 1 : 0);
        cmd.Parameters.AddIn("p_daterange", f.EnableDateRange ? 1 : 0);
        cmd.Parameters.AddIn("p_mtg", f.EnableMeetingType ? 1 : 0);
        cmd.Parameters.AddIn("p_qtr", f.EnableQuarter ? 1 : 0);
        cmd.Parameters.AddIn("p_year", f.EnableYear ? 1 : 0);
        cmd.Parameters.AddIn("p_mtglbl", f.MeetingTypeLabel);
        cmd.Parameters.AddIn("p_maxfiles", OracleDbType.Int32, (object?)f.MaxSupportingFiles ?? DBNull.Value);
        cmd.Parameters.AddIn("p_ftypes", f.AllowedFileTypes);
        cmd.Parameters.AddIn("p_root", f.StorageRootPath);
        cmd.Parameters.AddIn("p_reqprim", f.RequirePrimaryFile ? 1 : 0);
        cmd.Parameters.AddIn("p_icon", f.IconName);
        cmd.Parameters.AddIn("p_sort", f.SortOrder);
    }

    private static MomType MapType(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"),
        VerticalId = Long(r, "VERTICAL_ID"),
        VerticalName = StrN(r, "VNAME"),
        TypeName = Str(r, "TYPE_NAME"),
        TypeCode = Str(r, "TYPE_CODE"),
        Description = StrN(r, "DESCRIPTION"),
        EnableZoneRegionScope = Bool(r, "ENABLE_ZONE_REGION_SCOPE"),
        EnableDateRange = Bool(r, "ENABLE_DATE_RANGE"),
        EnableMeetingType = Bool(r, "ENABLE_MEETING_TYPE"),
        EnableQuarter = Bool(r, "ENABLE_QUARTER"),
        EnableYear = Bool(r, "ENABLE_YEAR"),
        MeetingTypeLabel = StrN(r, "MEETING_TYPE_LABEL"),
        MaxSupportingFiles = IntN(r, "MAX_SUPPORTING_FILES"),
        AllowedFileTypes = Str(r, "ALLOWED_FILE_TYPES"),
        StorageRootPath = Str(r, "STORAGE_ROOT_PATH"),
        RequirePrimaryFile = Bool(r, "REQUIRE_PRIMARY_FILE"),
        IconName = Str(r, "ICON_NAME"),
        SortOrder = Int(r, "SORT_ORDER"),
        IsActive = Bool(r, "IS_ACTIVE"),
        CreatedByEmp = StrN(r, "CREATED_BY_EMP"),
        CreatedAt = Dt(r, "CREATED_AT"),
        UpdatedAt = DtN(r, "UPDATED_AT")
    };

    // ── Meeting Types ────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<MomMeetingType>> ListMeetingTypesAsync(long momTypeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT ID, MOM_TYPE_ID, CODE, NAME, APPLIES_AT_RO, APPLIES_AT_ZO, APPLIES_AT_CO,
                     FREQUENCY_PER_QUARTER, SORT_ORDER, IS_ACTIVE, CO_SCOPE_CODE, CO_SCOPE_HIDES_RO
              FROM RP_M_MOM_MEETING_TYPE WHERE MOM_TYPE_ID=:p_tid ORDER BY SORT_ORDER, NAME", _uow.OracleTx);
        cmd.Parameters.AddIn("p_tid", momTypeId);
        return await cmd.QueryAsync(MapMeetingType, ct);
    }

    public async Task<long> UpsertMeetingTypeAsync(long momTypeId, MomMeetingType m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        if (m.Id > 0)
        {
            using var cmd = _uow.OracleConn.Cmd(
                @"UPDATE RP_M_MOM_MEETING_TYPE SET
                    CODE=:p_code, NAME=:p_name, APPLIES_AT_RO=:p_ro, APPLIES_AT_ZO=:p_zo, APPLIES_AT_CO=:p_co,
                    FREQUENCY_PER_QUARTER=:p_freq, SORT_ORDER=:p_sort, IS_ACTIVE=:p_active,
                    CO_SCOPE_CODE=:p_coscope, CO_SCOPE_HIDES_RO=:p_hidesro
                  WHERE ID=:p_id", _uow.OracleTx);
            BindMeetingType(cmd, m);
            cmd.Parameters.AddIn("p_id", m.Id);
            await cmd.ExecAsync(ct);
            return m.Id;
        }
        else
        {
            using var cmd = _uow.OracleConn.Cmd(
                @"INSERT INTO RP_M_MOM_MEETING_TYPE
                    (MOM_TYPE_ID, CODE, NAME, APPLIES_AT_RO, APPLIES_AT_ZO, APPLIES_AT_CO, FREQUENCY_PER_QUARTER, SORT_ORDER, IS_ACTIVE, CO_SCOPE_CODE, CO_SCOPE_HIDES_RO)
                  VALUES
                    (:p_tid,:p_code,:p_name,:p_ro,:p_zo,:p_co,:p_freq,:p_sort,:p_active,:p_coscope,:p_hidesro)
                  RETURNING ID INTO :p_id", _uow.OracleTx);
            cmd.Parameters.AddIn("p_tid", momTypeId);
            BindMeetingType(cmd, m);
            var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
            await cmd.ExecAsync(ct);
            return outId.OutId();
        }
    }

    private static void BindMeetingType(OracleCommand cmd, MomMeetingType m)
    {
        cmd.Parameters.AddIn("p_code", m.Code);
        cmd.Parameters.AddIn("p_name", m.Name);
        cmd.Parameters.AddIn("p_ro", m.AppliesAtRo ? 1 : 0);
        cmd.Parameters.AddIn("p_zo", m.AppliesAtZo ? 1 : 0);
        cmd.Parameters.AddIn("p_co", m.AppliesAtCo ? 1 : 0);
        cmd.Parameters.AddIn("p_freq", OracleDbType.Int32, (object?)m.FrequencyPerQuarter ?? DBNull.Value);
        cmd.Parameters.AddIn("p_sort", m.SortOrder);
        cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        cmd.Parameters.AddIn("p_coscope", OracleDbType.Varchar2, (object?)m.CoScopeCode ?? DBNull.Value);
        cmd.Parameters.AddIn("p_hidesro", m.CoScopeHidesRoLevel ? 1 : 0);
    }

    private static MomMeetingType MapMeetingType(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"),
        MomTypeId = Long(r, "MOM_TYPE_ID"),
        Code = Str(r, "CODE"),
        Name = Str(r, "NAME"),
        AppliesAtRo = Bool(r, "APPLIES_AT_RO"),
        AppliesAtZo = Bool(r, "APPLIES_AT_ZO"),
        AppliesAtCo = Bool(r, "APPLIES_AT_CO"),
        FrequencyPerQuarter = IntN(r, "FREQUENCY_PER_QUARTER"),
        SortOrder = Int(r, "SORT_ORDER"),
        IsActive = Bool(r, "IS_ACTIVE"),
        CoScopeCode = StrN(r, "CO_SCOPE_CODE"),
        CoScopeHidesRoLevel = Bool(r, "CO_SCOPE_HIDES_RO")
    };

    // ── Unions ───────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<MomUnion>> ListUnionsAsync(long momTypeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT ID, MOM_TYPE_ID, CATEGORY, UNION_NAME, AFFILIATION, SORT_ORDER, IS_ACTIVE
              FROM RP_M_MOM_UNION WHERE MOM_TYPE_ID=:p_tid ORDER BY SORT_ORDER, UNION_NAME", _uow.OracleTx);
        cmd.Parameters.AddIn("p_tid", momTypeId);
        return await cmd.QueryAsync(MapUnion, ct);
    }

    public async Task<long> UpsertUnionAsync(long momTypeId, MomUnion u, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        if (u.Id > 0)
        {
            using var cmd = _uow.OracleConn.Cmd(
                @"UPDATE RP_M_MOM_UNION SET
                    CATEGORY=:p_cat, UNION_NAME=:p_uname, AFFILIATION=:p_aff, SORT_ORDER=:p_sort, IS_ACTIVE=:p_active
                  WHERE ID=:p_id", _uow.OracleTx);
            BindUnion(cmd, u);
            cmd.Parameters.AddIn("p_id", u.Id);
            await cmd.ExecAsync(ct);
            return u.Id;
        }
        else
        {
            using var cmd = _uow.OracleConn.Cmd(
                @"INSERT INTO RP_M_MOM_UNION
                    (MOM_TYPE_ID, CATEGORY, UNION_NAME, AFFILIATION, SORT_ORDER, IS_ACTIVE)
                  VALUES
                    (:p_tid,:p_cat,:p_uname,:p_aff,:p_sort,:p_active)
                  RETURNING ID INTO :p_id", _uow.OracleTx);
            cmd.Parameters.AddIn("p_tid", momTypeId);
            BindUnion(cmd, u);
            var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
            await cmd.ExecAsync(ct);
            return outId.OutId();
        }
    }

    private static void BindUnion(OracleCommand cmd, MomUnion u)
    {
        cmd.Parameters.AddIn("p_cat", u.Category);
        cmd.Parameters.AddIn("p_uname", u.UnionName);
        cmd.Parameters.AddIn("p_aff", u.Affiliation);
        cmd.Parameters.AddIn("p_sort", u.SortOrder);
        cmd.Parameters.AddIn("p_active", u.IsActive ? 1 : 0);
    }

    private static MomUnion MapUnion(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"),
        MomTypeId = Long(r, "MOM_TYPE_ID"),
        Category = Str(r, "CATEGORY"),
        UnionName = Str(r, "UNION_NAME"),
        Affiliation = Str(r, "AFFILIATION"),
        SortOrder = Int(r, "SORT_ORDER"),
        IsActive = Bool(r, "IS_ACTIVE")
    };

    // ── Entries ──────────────────────────────────────────────────────────────
    private const string EntrySelectCols = @"
        e.ID, e.MOM_TYPE_ID, e.OFFICE_LEVEL, e.ZONE_NAME, e.REGION_NAME, e.BRANCH_SOL_ID,
        e.MEETING_TYPE_ID, mt.NAME AS MTNAME, e.UNION_ID, u.UNION_NAME AS UNAME,
        e.MEETING_YEAR, e.MEETING_MONTH, e.MEETING_DATE, e.QUARTER, e.REMARKS,
        e.PRIMARY_FILE_PATH, e.PRIMARY_FILE_NAME, e.CREATED_BY_EMP, e.CREATED_AT, e.UPDATED_AT";

    public async Task<IReadOnlyList<MomEntry>> ListEntriesAsync(long momTypeId, string? zone, string? region, DateTime? dateFrom, DateTime? dateTo,
        long? meetingTypeId, int? quarter, int? year, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var sql = $@"SELECT {EntrySelectCols}
                    FROM RP_MOM_ENTRY e
                    LEFT JOIN RP_M_MOM_MEETING_TYPE mt ON mt.ID = e.MEETING_TYPE_ID
                    LEFT JOIN RP_M_MOM_UNION u ON u.ID = e.UNION_ID
                    WHERE e.MOM_TYPE_ID=:p_tid";
        if (!string.IsNullOrWhiteSpace(zone)) sql += " AND e.ZONE_NAME=:p_zone";
        if (!string.IsNullOrWhiteSpace(region)) sql += " AND e.REGION_NAME=:p_region";
        if (dateFrom.HasValue) sql += " AND e.MEETING_DATE >= :p_from";
        if (dateTo.HasValue) sql += " AND e.MEETING_DATE <= :p_to";
        if (meetingTypeId.HasValue) sql += " AND e.MEETING_TYPE_ID=:p_mtid";
        if (quarter.HasValue) sql += " AND e.QUARTER=:p_qtr";
        if (year.HasValue) sql += " AND e.MEETING_YEAR=:p_year";
        sql += " ORDER BY e.MEETING_DATE DESC";

        using var cmd = _uow.OracleConn.Cmd(sql, _uow.OracleTx);
        cmd.Parameters.AddIn("p_tid", momTypeId);
        if (!string.IsNullOrWhiteSpace(zone)) cmd.Parameters.AddIn("p_zone", zone);
        if (!string.IsNullOrWhiteSpace(region)) cmd.Parameters.AddIn("p_region", region);
        if (dateFrom.HasValue) cmd.Parameters.AddIn("p_from", dateFrom.Value);
        if (dateTo.HasValue) cmd.Parameters.AddIn("p_to", dateTo.Value);
        if (meetingTypeId.HasValue) cmd.Parameters.AddIn("p_mtid", meetingTypeId.Value);
        if (quarter.HasValue) cmd.Parameters.AddIn("p_qtr", quarter.Value);
        if (year.HasValue) cmd.Parameters.AddIn("p_year", year.Value);

        return await cmd.QueryAsync(MapEntry, ct);
    }

    public async Task<MomEntry?> GetEntryAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            $@"SELECT {EntrySelectCols}
              FROM RP_MOM_ENTRY e
              LEFT JOIN RP_M_MOM_MEETING_TYPE mt ON mt.ID = e.MEETING_TYPE_ID
              LEFT JOIN RP_M_MOM_UNION u ON u.ID = e.UNION_ID
              WHERE e.ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        return await cmd.QueryOneAsync(MapEntry, ct);
    }

    public async Task<long> InsertEntryAsync(MomEntryForm f, string createdByEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_MOM_ENTRY
                (MOM_TYPE_ID, OFFICE_LEVEL, ZONE_NAME, REGION_NAME, BRANCH_SOL_ID, MEETING_TYPE_ID, UNION_ID,
                 MEETING_YEAR, MEETING_MONTH, MEETING_DATE, QUARTER, REMARKS, PRIMARY_FILE_PATH, PRIMARY_FILE_NAME, CREATED_BY_EMP)
              VALUES
                (:p_tid,:p_lvl,:p_zone,:p_region,:p_sol,:p_mtid,:p_uid,
                 :p_year,:p_month,:p_date,:p_qtr,:p_rem,:p_fpath,:p_fname,:p_emp)
              RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_tid", f.MomTypeId);
        cmd.Parameters.AddIn("p_lvl", f.OfficeLevel);
        cmd.Parameters.AddIn("p_zone", f.ZoneName);
        cmd.Parameters.AddIn("p_region", f.RegionName);
        cmd.Parameters.AddIn("p_sol", f.BranchSolId);
        cmd.Parameters.AddIn("p_mtid", OracleDbType.Int64, (object?)f.MeetingTypeId ?? DBNull.Value);
        cmd.Parameters.AddIn("p_uid", OracleDbType.Int64, (object?)f.UnionId ?? DBNull.Value);
        cmd.Parameters.AddIn("p_year", f.MeetingYear);
        cmd.Parameters.AddIn("p_month", OracleDbType.Int32, (object?)f.MeetingMonth ?? DBNull.Value);
        cmd.Parameters.AddIn("p_date", f.MeetingDate);
        cmd.Parameters.AddIn("p_qtr", OracleDbType.Int32, (object?)f.Quarter ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rem", f.Remarks);
        cmd.Parameters.AddIn("p_fpath", f.PrimaryFilePath);
        cmd.Parameters.AddIn("p_fname", f.PrimaryFileName);
        cmd.Parameters.AddIn("p_emp", createdByEmp);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task UpdateEntryAsync(long id, MomEntryForm f, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"UPDATE RP_MOM_ENTRY SET
                OFFICE_LEVEL=:p_lvl, ZONE_NAME=:p_zone, REGION_NAME=:p_region, BRANCH_SOL_ID=:p_sol,
                MEETING_TYPE_ID=:p_mtid, UNION_ID=:p_uid, MEETING_YEAR=:p_year, MEETING_MONTH=:p_month,
                MEETING_DATE=:p_date, QUARTER=:p_qtr, REMARKS=:p_rem,
                PRIMARY_FILE_PATH=:p_fpath, PRIMARY_FILE_NAME=:p_fname, UPDATED_AT=SYSTIMESTAMP
              WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_lvl", f.OfficeLevel);
        cmd.Parameters.AddIn("p_zone", f.ZoneName);
        cmd.Parameters.AddIn("p_region", f.RegionName);
        cmd.Parameters.AddIn("p_sol", f.BranchSolId);
        cmd.Parameters.AddIn("p_mtid", OracleDbType.Int64, (object?)f.MeetingTypeId ?? DBNull.Value);
        cmd.Parameters.AddIn("p_uid", OracleDbType.Int64, (object?)f.UnionId ?? DBNull.Value);
        cmd.Parameters.AddIn("p_year", f.MeetingYear);
        cmd.Parameters.AddIn("p_month", OracleDbType.Int32, (object?)f.MeetingMonth ?? DBNull.Value);
        cmd.Parameters.AddIn("p_date", f.MeetingDate);
        cmd.Parameters.AddIn("p_qtr", OracleDbType.Int32, (object?)f.Quarter ?? DBNull.Value);
        cmd.Parameters.AddIn("p_rem", f.Remarks);
        cmd.Parameters.AddIn("p_fpath", f.PrimaryFilePath);
        cmd.Parameters.AddIn("p_fname", f.PrimaryFileName);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    public async Task DeleteEntryAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using (var delAtt = _uow.OracleConn.Cmd("DELETE FROM RP_MOM_ATTACHMENT WHERE ENTRY_ID=:p_id", _uow.OracleTx))
        {
            delAtt.Parameters.AddIn("p_id", id);
            await delAtt.ExecAsync(ct);
        }
        using var cmd = _uow.OracleConn.Cmd("DELETE FROM RP_MOM_ENTRY WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    public async Task<string?> GetCoScopeAsync(long momTypeId, string empCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT SCOPE_CODE FROM RP_M_MOM_CO_SCOPE
              WHERE MOM_TYPE_ID=:p_tid AND EMP_CODE=:p_emp AND IS_ACTIVE=1", _uow.OracleTx);
        cmd.Parameters.AddIn("p_tid", momTypeId);
        cmd.Parameters.AddIn("p_emp", empCode);
        return await cmd.QueryOneAsync(r => Str(r, "SCOPE_CODE"), ct);
    }

    public async Task<IReadOnlyList<(string ScopeCode, string Label)>> ListCoScopeGroupsAsync(long momTypeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT CO_SCOPE_CODE, LISTAGG(NAME, ' + ') WITHIN GROUP (ORDER BY SORT_ORDER, NAME) AS NAMES
              FROM RP_M_MOM_MEETING_TYPE
              WHERE MOM_TYPE_ID=:p_tid AND CO_SCOPE_CODE IS NOT NULL AND IS_ACTIVE=1
              GROUP BY CO_SCOPE_CODE
              ORDER BY CO_SCOPE_CODE", _uow.OracleTx);
        cmd.Parameters.AddIn("p_tid", momTypeId);
        return await cmd.QueryAsync(r => (Str(r, "CO_SCOPE_CODE"), $"{Str(r, "CO_SCOPE_CODE")} ({Str(r, "NAMES")})"), ct);
    }

    // ── CO Scope assignments (Item 6 admin UI) ─────────────────────────────────
    public async Task<IReadOnlyList<MomCoScope>> ListCoScopeAsync(long momTypeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT s.ID, s.MOM_TYPE_ID, s.EMP_CODE, e.EMP_NAME, s.SCOPE_CODE, s.IS_ACTIVE, s.ASSIGNED_BY_EMP, s.ASSIGNED_AT
              FROM RP_M_MOM_CO_SCOPE s
              LEFT JOIN RP_M_EMPLOYEE e ON e.PF_NUMBER = s.EMP_CODE
              WHERE s.MOM_TYPE_ID=:p_tid AND s.IS_ACTIVE=1
              ORDER BY s.ASSIGNED_AT DESC", _uow.OracleTx);
        cmd.Parameters.AddIn("p_tid", momTypeId);
        return await cmd.QueryAsync(MapCoScope, ct);
    }

    public async Task<long> AddCoScopeAsync(long momTypeId, string empCode, string scopeCode, string assignedByEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var existing = _uow.OracleConn.Cmd(
            @"SELECT ID FROM RP_M_MOM_CO_SCOPE WHERE MOM_TYPE_ID=:p_tid AND EMP_CODE=:p_emp", _uow.OracleTx);
        existing.Parameters.AddIn("p_tid", momTypeId);
        existing.Parameters.AddIn("p_emp", empCode);
        var existingId = await existing.QueryOneValueAsync(r => Long(r, "ID"), ct);

        if (existingId.HasValue)
        {
            using var upd = _uow.OracleConn.Cmd(
                @"UPDATE RP_M_MOM_CO_SCOPE SET SCOPE_CODE=:p_scope, IS_ACTIVE=1, ASSIGNED_BY_EMP=:p_by, ASSIGNED_AT=SYSTIMESTAMP
                  WHERE ID=:p_id", _uow.OracleTx);
            upd.Parameters.AddIn("p_scope", scopeCode);
            upd.Parameters.AddIn("p_by", assignedByEmp);
            upd.Parameters.AddIn("p_id", existingId.Value);
            await upd.ExecAsync(ct);
            return existingId.Value;
        }

        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_M_MOM_CO_SCOPE (MOM_TYPE_ID, EMP_CODE, SCOPE_CODE, IS_ACTIVE, ASSIGNED_BY_EMP)
              VALUES (:p_tid,:p_emp,:p_scope,1,:p_by)
              RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_tid", momTypeId);
        cmd.Parameters.AddIn("p_emp", empCode);
        cmd.Parameters.AddIn("p_scope", scopeCode);
        cmd.Parameters.AddIn("p_by", assignedByEmp);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task RemoveCoScopeAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_MOM_CO_SCOPE SET IS_ACTIVE=0 WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static MomCoScope MapCoScope(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"),
        MomTypeId = Long(r, "MOM_TYPE_ID"),
        EmpCode = Str(r, "EMP_CODE"),
        EmpName = StrN(r, "EMP_NAME"),
        ScopeCode = Str(r, "SCOPE_CODE"),
        IsActive = Bool(r, "IS_ACTIVE"),
        AssignedByEmp = Str(r, "ASSIGNED_BY_EMP"),
        AssignedAt = Dt(r, "ASSIGNED_AT")
    };

    private static MomEntry MapEntry(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"),
        MomTypeId = Long(r, "MOM_TYPE_ID"),
        OfficeLevel = Str(r, "OFFICE_LEVEL"),
        ZoneName = StrN(r, "ZONE_NAME"),
        RegionName = StrN(r, "REGION_NAME"),
        BranchSolId = StrN(r, "BRANCH_SOL_ID"),
        MeetingTypeId = LongN(r, "MEETING_TYPE_ID"),
        MeetingTypeName = StrN(r, "MTNAME"),
        UnionId = LongN(r, "UNION_ID"),
        UnionName = StrN(r, "UNAME"),
        MeetingYear = Int(r, "MEETING_YEAR"),
        MeetingMonth = IntN(r, "MEETING_MONTH"),
        MeetingDate = Dt(r, "MEETING_DATE"),
        Quarter = IntN(r, "QUARTER"),
        Remarks = StrN(r, "REMARKS"),
        PrimaryFilePath = Str(r, "PRIMARY_FILE_PATH"),
        PrimaryFileName = Str(r, "PRIMARY_FILE_NAME"),
        CreatedByEmp = Str(r, "CREATED_BY_EMP"),
        CreatedAt = Dt(r, "CREATED_AT"),
        UpdatedAt = DtN(r, "UPDATED_AT")
    };

    // ── Attachments ──────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<MomAttachment>> ListAttachmentsAsync(long entryId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT ID, ENTRY_ID, FILE_NAME, FILE_PATH, UPLOADED_AT, UPLOADED_BY_EMP
              FROM RP_MOM_ATTACHMENT WHERE ENTRY_ID=:p_eid ORDER BY UPLOADED_AT", _uow.OracleTx);
        cmd.Parameters.AddIn("p_eid", entryId);
        return await cmd.QueryAsync(MapAttachment, ct);
    }

    public async Task<long> InsertAttachmentAsync(long entryId, string fileName, string filePath, string uploadedByEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_MOM_ATTACHMENT (ENTRY_ID, FILE_NAME, FILE_PATH, UPLOADED_BY_EMP)
              VALUES (:p_eid,:p_fname,:p_fpath,:p_emp)
              RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_eid", entryId);
        cmd.Parameters.AddIn("p_fname", fileName);
        cmd.Parameters.AddIn("p_fpath", filePath);
        cmd.Parameters.AddIn("p_emp", uploadedByEmp);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    private static MomAttachment MapAttachment(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"),
        EntryId = Long(r, "ENTRY_ID"),
        FileName = Str(r, "FILE_NAME"),
        FilePath = Str(r, "FILE_PATH"),
        UploadedAt = Dt(r, "UPLOADED_AT"),
        UploadedByEmp = Str(r, "UPLOADED_BY_EMP")
    };
}
