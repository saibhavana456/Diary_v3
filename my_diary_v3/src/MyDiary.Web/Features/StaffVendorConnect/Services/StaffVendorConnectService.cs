using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using MyDiary.Web.Features.StaffVendorConnect.Models.DTO;

namespace MyDiary.Web.Features.StaffVendorConnect.Services;

/// <summary>
/// Staff and Vendor Connect (Assurance Hub — BRD "Group Data for External Members and Internal
/// Staff", UBI_BRD_V1.0). Self-contained (Flat Management style): raw Oracle ADO, no Core/Data
/// layer changes. Vendor data is EAV (RP_SVC_VENDOR_RECORD header + RP_SVC_VENDOR_FIELD_VALUE
/// detail), driven by the field-mapping table RP_SVC_FIELD_DEFINITION — that table IS the form
/// builder Card 1 manages, so a new category or field needs zero code change, only new rows.
/// </summary>
public sealed class StaffVendorConnectService : IStaffVendorConnectService
{
    private readonly string _connString;
    private readonly string _sqlServerConnString;
    private readonly string _branchDiaryConnString;
    private readonly ILogger<StaffVendorConnectService> _logger;
    private readonly IAuthorizationService _authz;

    public StaffVendorConnectService(IConfiguration config, ILogger<StaffVendorConnectService> logger, IAuthorizationService authz)
    {
        _connString = config.GetConnectionString("RP_Owner")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:RP_Owner in appsettings.json");
        // SQL Server (OrganizationsDB) — source of the HRMS StaffDetails table that now backs the
        // Staff Directory (previously the Oracle RP_SVC_STAFF_SNAPSHOT snapshot).
        _sqlServerConnString = config.GetConnectionString("OrganisationsSQLCon")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:OrganisationsSQLCon in appsettings.json");

        _branchDiaryConnString = config.GetConnectionString("SQLServerConnection")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:SQLServerConnection in appsettings.json");

        _logger = logger;
        _authz = authz;
    }

    private OracleConnection Conn() => new(_connString);

    private SqlConnection SqlConn() => new(_sqlServerConnString);

    private SqlConnection BranchDiarySqlConn() => new(_branchDiaryConnString);

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Categories
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<SvcCategory>> GetCategoriesAsync(string? groupType = null, bool activeOnly = true)
    {
        var result = new List<SvcCategory>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand($@"
            SELECT CATEGORY_CODE, CATEGORY_NAME, GROUP_TYPE, PARENT_GROUP, DISPLAY_ORDER, IS_ACTIVE
            FROM RP_SVC_CATEGORY_MASTER
            WHERE (:p_group IS NULL OR GROUP_TYPE = :p_group2)
              AND (:p_active_only = 0 OR IS_ACTIVE = 1)
            ORDER BY DISPLAY_ORDER", cn) { BindByName = true };
        cmd.Parameters.Add("p_group", (object?)groupType ?? DBNull.Value);
        cmd.Parameters.Add("p_group2", (object?)groupType ?? DBNull.Value);
        cmd.Parameters.Add("p_active_only", activeOnly ? 1 : 0);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(MapCategory(r));
        return result;
    }

    public async Task<SvcCategory?> GetCategoryAsync(string categoryCode)
    {
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "SELECT CATEGORY_CODE, CATEGORY_NAME, GROUP_TYPE, PARENT_GROUP, DISPLAY_ORDER, IS_ACTIVE FROM RP_SVC_CATEGORY_MASTER WHERE CATEGORY_CODE = :p_code", cn);
        cmd.Parameters.Add("p_code", categoryCode);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapCategory(r) : null;
    }

    private static SvcCategory MapCategory(OracleDataReader r) => new()
    {
        CategoryCode = r.GetString(0), CategoryName = r.GetString(1), GroupType = r.GetString(2),
        ParentGroup = r.IsDBNull(3) ? null : r.GetString(3),
        DisplayOrder = Convert.ToInt32(r.GetDecimal(4)), IsActive = Convert.ToInt32(r.GetDecimal(5)) == 1
    };

    public async Task UpsertCategoryAsync(SvcCategory category, string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand(@"
            MERGE INTO RP_SVC_CATEGORY_MASTER t USING (SELECT :p_code AS CATEGORY_CODE FROM DUAL) s
            ON (t.CATEGORY_CODE = s.CATEGORY_CODE)
            WHEN MATCHED THEN UPDATE SET CATEGORY_NAME = :p_name, GROUP_TYPE = :p_gt, PARENT_GROUP = :p_pg,
                DISPLAY_ORDER = :p_order, IS_ACTIVE = :p_active, UPDATED_BY = :p_actor, UPDATED_AT = SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (CATEGORY_CODE, CATEGORY_NAME, GROUP_TYPE, PARENT_GROUP, DISPLAY_ORDER, IS_ACTIVE, ENTRY_BY, ENTRY_AT)
                VALUES (s.CATEGORY_CODE, :p_name2, :p_gt2, :p_pg2, :p_order2, :p_active2, :p_actor2, SYSTIMESTAMP)", cn) { BindByName = true };
        cmd.Parameters.Add("p_code", category.CategoryCode);
        cmd.Parameters.Add("p_name", category.CategoryName);
        cmd.Parameters.Add("p_gt", category.GroupType);
        cmd.Parameters.Add("p_pg", (object?)category.ParentGroup ?? DBNull.Value);
        cmd.Parameters.Add("p_order", category.DisplayOrder);
        cmd.Parameters.Add("p_active", category.IsActive ? 1 : 0);
        cmd.Parameters.Add("p_actor", actor);
        cmd.Parameters.Add("p_name2", category.CategoryName);
        cmd.Parameters.Add("p_gt2", category.GroupType);
        cmd.Parameters.Add("p_pg2", (object?)category.ParentGroup ?? DBNull.Value);
        cmd.Parameters.Add("p_order2", category.DisplayOrder);
        cmd.Parameters.Add("p_active2", category.IsActive ? 1 : 0);
        cmd.Parameters.Add("p_actor2", actor);
        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("SVC: category {Code} upserted by {Actor}", category.CategoryCode, actor);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Field definitions (form builder)
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<SvcFieldDefinition>> GetFieldDefinitionsAsync(string categoryCode, bool activeOnly = true)
    {
        var result = new List<SvcFieldDefinition>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand($@"
            SELECT FIELD_ID, CATEGORY_CODE, FIELD_KEY, FIELD_LABEL, SECTION_NAME, FIELD_TYPE,
                   VALIDATION_REGEX, VALIDATION_HINT, MAX_LENGTH, IS_MANDATORY, IS_READONLY,
                   DROPDOWN_SOURCE, DROPDOWN_OPTIONS, DISPLAY_ORDER, IS_ACTIVE, IS_UNIQUE,
                   CREATED_BY, CREATED_DATE, UPDATED_BY, UPDATED_DATE, IS_PII
            FROM RP_SVC_FIELD_DEFINITION
            WHERE CATEGORY_CODE = :p_cat {(activeOnly ? "AND IS_ACTIVE = 1" : "")}
            ORDER BY DISPLAY_ORDER", cn);
        cmd.Parameters.Add("p_cat", categoryCode);
        await cn.OpenAsync();
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) result.Add(await MapFieldDefAsync(r));

        await ResolveMasterDropdownOptionsAsync(cn, result);
        return result;
    }

    /// <summary>Fields with DROPDOWN_SOURCE='MASTER' get their DropdownOptions populated from
    /// RP_SVC_FIELD_MASTER_MAP + RP_SVC_MASTER_VALUE (active values, in DISPLAY_ORDER) — same
    /// property every caller (VendorForm.razor, GenerateVendorTemplateAsync's Excel data
    /// validation, ValidateVendorUploadAsync's allowed-value check) already reads for
    /// DROPDOWN_SOURCE='STATIC' fields, so nothing downstream needs to know the difference.</summary>
    private static async Task ResolveMasterDropdownOptionsAsync(OracleConnection cn, List<SvcFieldDefinition> fields)
    {
        var masterFields = fields.Where(f => f.DropdownSource == "MASTER").ToList();
        if (masterFields.Count == 0) return;

        var ids = string.Join(",", masterFields.Select(f => f.FieldId));

        // Field -> (its own master type, that type's parent type) — used below to find which
        // sibling field (if any) drives this one's cascade.
        var fieldTypeInfo = new Dictionary<long, (string TypeCode, string? ParentTypeCode)>();
        await using (var typeCmd = new OracleCommand($@"
            SELECT m.FIELD_ID, m.MASTER_TYPE_CODE, t.PARENT_TYPE_CODE
            FROM RP_SVC_FIELD_MASTER_MAP m
            JOIN RP_SVC_MASTER_TYPE t ON t.MASTER_TYPE_CODE = m.MASTER_TYPE_CODE
            WHERE m.FIELD_ID IN ({ids})", cn))
        await using (var r = await typeCmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
                fieldTypeInfo[r.GetInt64(0)] = (r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2));
        }

        foreach (var f in masterFields)
        {
            if (!fieldTypeInfo.TryGetValue(f.FieldId, out var info) || info.ParentTypeCode is null) continue;
            // The driver field is whichever sibling in this SAME category maps to the parent
            // master type (e.g. AREA_STATE drives AREA_DISTRICT, which drives AREA_CITY).
            var driver = masterFields.FirstOrDefault(other => other.FieldId != f.FieldId
                && fieldTypeInfo.TryGetValue(other.FieldId, out var otherInfo) && otherInfo.TypeCode == info.ParentTypeCode);
            f.MasterParentFieldKey = driver?.FieldKey;
        }

        var byField = new Dictionary<long, List<string>>();
        var parentsByField = new Dictionary<long, List<(string, string?)>>();
        await using var cmd = new OracleCommand($@"
            SELECT m.FIELD_ID, v.VALUE_LABEL, p.VALUE_LABEL
            FROM RP_SVC_FIELD_MASTER_MAP m
            JOIN RP_SVC_MASTER_VALUE v ON v.MASTER_TYPE_CODE = m.MASTER_TYPE_CODE AND v.IS_ACTIVE = 1
            LEFT JOIN RP_SVC_MASTER_VALUE p ON p.MASTER_ID = v.PARENT_VALUE_ID
            WHERE m.FIELD_ID IN ({ids})
            ORDER BY m.FIELD_ID, v.DISPLAY_ORDER, v.VALUE_LABEL", cn);
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                var fid = r.GetInt64(0);
                var label = r.GetString(1);
                var parentLabel = r.IsDBNull(2) ? null : r.GetString(2);
                if (!byField.TryGetValue(fid, out var list)) byField[fid] = list = new();
                list.Add(label);
                if (!parentsByField.TryGetValue(fid, out var plist)) parentsByField[fid] = plist = new();
                plist.Add((label, parentLabel));
            }
        }
        foreach (var f in masterFields)
        {
            f.DropdownOptions = byField.TryGetValue(f.FieldId, out var opts) ? opts : new();
            f.MasterValueParents = parentsByField.TryGetValue(f.FieldId, out var parents) ? parents : new();
        }
    }

    public async Task<SvcFieldDefinition?> GetFieldDefinitionAsync(long fieldId)
    {
        await using var cn = Conn();
        await using var cmd = new OracleCommand(@"
            SELECT FIELD_ID, CATEGORY_CODE, FIELD_KEY, FIELD_LABEL, SECTION_NAME, FIELD_TYPE,
                   VALIDATION_REGEX, VALIDATION_HINT, MAX_LENGTH, IS_MANDATORY, IS_READONLY,
                   DROPDOWN_SOURCE, DROPDOWN_OPTIONS, DISPLAY_ORDER, IS_ACTIVE, IS_UNIQUE,
                   CREATED_BY, CREATED_DATE, UPDATED_BY, UPDATED_DATE, IS_PII
            FROM RP_SVC_FIELD_DEFINITION WHERE FIELD_ID = :p_id", cn);
        cmd.Parameters.Add("p_id", fieldId);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? await MapFieldDefAsync(r) : null;
    }

    private static Task<SvcFieldDefinition> MapFieldDefAsync(OracleDataReader r)
    {
        string? optionsJson = r.IsDBNull(12) ? null : r.GetOracleClob(12).Value;

        List<string> options = new();
        if (!string.IsNullOrWhiteSpace(optionsJson))
        {
            try { options = JsonSerializer.Deserialize<List<string>>(optionsJson) ?? new(); }
            catch { /* malformed JSON in a hand-edited row — treat as no options rather than throwing */ }
        }

        return Task.FromResult(new SvcFieldDefinition
        {
            FieldId = r.GetInt64(0), CategoryCode = r.GetString(1), FieldKey = r.GetString(2), FieldLabel = r.GetString(3),
            SectionName = r.IsDBNull(4) ? null : r.GetString(4), FieldType = r.GetString(5),
            ValidationRegex = r.IsDBNull(6) ? null : r.GetString(6), ValidationHint = r.IsDBNull(7) ? null : r.GetString(7),
            MaxLength = r.IsDBNull(8) ? null : Convert.ToInt32(r.GetDecimal(8)),
            IsMandatory = Convert.ToInt32(r.GetDecimal(9)) == 1, IsReadonly = Convert.ToInt32(r.GetDecimal(10)) == 1,
            DropdownSource = r.IsDBNull(11) ? null : r.GetString(11), DropdownOptions = options,
            DisplayOrder = Convert.ToInt32(r.GetDecimal(13)), IsActive = Convert.ToInt32(r.GetDecimal(14)) == 1,
            IsUnique = Convert.ToInt32(r.GetDecimal(15)) == 1,
            CreatedBy = r.IsDBNull(16) ? null : r.GetString(16), CreatedAt = r.IsDBNull(17) ? null : r.GetDateTime(17),
            UpdatedBy = r.IsDBNull(18) ? null : r.GetString(18), UpdatedAt = r.IsDBNull(19) ? null : r.GetDateTime(19),
            IsPii = !r.IsDBNull(20) && Convert.ToInt32(r.GetDecimal(20)) == 1
        });
    }

    public async Task<long> UpsertFieldDefinitionAsync(SvcFieldDefinition field, string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        var optionsJson = field.DropdownOptions.Count > 0 ? JsonSerializer.Serialize(field.DropdownOptions) : null;

        if (field.FieldId > 0)
        {
            await using var upd = new OracleCommand(@"
                UPDATE RP_SVC_FIELD_DEFINITION SET
                    FIELD_LABEL = :p_label, SECTION_NAME = :p_section, FIELD_TYPE = :p_type,
                    VALIDATION_REGEX = :p_regex, VALIDATION_HINT = :p_hint, MAX_LENGTH = :p_maxlen,
                    IS_MANDATORY = :p_mand, IS_READONLY = :p_ro, IS_UNIQUE = :p_uniq, DROPDOWN_SOURCE = :p_ddsrc,
                    DROPDOWN_OPTIONS = :p_ddopt, DISPLAY_ORDER = :p_order, IS_ACTIVE = :p_active, IS_PII = :p_pii,
                    UPDATED_BY = :p_by, UPDATED_DATE = SYSTIMESTAMP
                WHERE FIELD_ID = :p_id", cn) { BindByName = true };
            AddFieldParams(upd, field, optionsJson);
            upd.Parameters.Add("p_by", actor);
            upd.Parameters.Add("p_id", field.FieldId);
            await upd.ExecuteNonQueryAsync();
            return field.FieldId;
        }
        else
        {
            await using var ins = new OracleCommand(@"
                INSERT INTO RP_SVC_FIELD_DEFINITION (
                    FIELD_ID, CATEGORY_CODE, FIELD_KEY, FIELD_LABEL, SECTION_NAME, FIELD_TYPE,
                    VALIDATION_REGEX, VALIDATION_HINT, MAX_LENGTH, IS_MANDATORY, IS_READONLY, IS_UNIQUE,
                    DROPDOWN_SOURCE, DROPDOWN_OPTIONS, DISPLAY_ORDER, IS_ACTIVE, IS_PII, CREATED_BY)
                VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_cat, :p_key, :p_label, :p_section, :p_type,
                    :p_regex, :p_hint, :p_maxlen, :p_mand, :p_ro, :p_uniq, :p_ddsrc, :p_ddopt, :p_order, :p_active, :p_pii, :p_by)
                RETURNING FIELD_ID INTO :p_outid", cn) { BindByName = true };
            ins.Parameters.Add("p_cat", field.CategoryCode);
            ins.Parameters.Add("p_key", field.FieldKey);
            AddFieldParams(ins, field, optionsJson);
            ins.Parameters.Add("p_by", actor);
            var outId = new OracleParameter("p_outid", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            ins.Parameters.Add(outId);
            await ins.ExecuteNonQueryAsync();
            return Convert.ToInt64(((OracleDecimal)outId.Value).Value);
        }
    }

    private static void AddFieldParams(OracleCommand cmd, SvcFieldDefinition field, string? optionsJson)
    {
        cmd.Parameters.Add("p_label", field.FieldLabel);
        cmd.Parameters.Add("p_section", (object?)field.SectionName ?? DBNull.Value);
        cmd.Parameters.Add("p_type", field.FieldType);
        cmd.Parameters.Add("p_regex", (object?)field.ValidationRegex ?? DBNull.Value);
        cmd.Parameters.Add("p_hint", (object?)field.ValidationHint ?? DBNull.Value);
        cmd.Parameters.Add("p_maxlen", (object?)field.MaxLength ?? DBNull.Value);
        cmd.Parameters.Add("p_mand", field.IsMandatory ? 1 : 0);
        cmd.Parameters.Add("p_ro", field.IsReadonly ? 1 : 0);
        cmd.Parameters.Add("p_uniq", field.IsUnique ? 1 : 0);
        cmd.Parameters.Add("p_ddsrc", (object?)field.DropdownSource ?? DBNull.Value);
        cmd.Parameters.Add(new OracleParameter("p_ddopt", OracleDbType.Clob) { Value = (object?)optionsJson ?? DBNull.Value });
        cmd.Parameters.Add("p_order", field.DisplayOrder);
        cmd.Parameters.Add("p_active", field.IsActive ? 1 : 0);
        cmd.Parameters.Add("p_pii", field.IsPii ? 1 : 0);
    }

    public async Task SetFieldActiveAsync(long fieldId, bool isActive, string actor)
    {
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "UPDATE RP_SVC_FIELD_DEFINITION SET IS_ACTIVE = :p_active, UPDATED_BY = :p_by, UPDATED_DATE = SYSTIMESTAMP WHERE FIELD_ID = :p_id", cn)
        { BindByName = true };
        cmd.Parameters.Add("p_active", isActive ? 1 : 0);
        cmd.Parameters.Add("p_by", actor);
        cmd.Parameters.Add("p_id", fieldId);
        await cn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Vendor records (EAV)
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<SvcVendorRecord>> GetVendorRecordsAsync(string? categoryCode, string? zoneCode, string? regionCode, string? branchCode, string status = "ACTIVE")
    {
        var result = new List<SvcVendorRecord>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(@"
            SELECT RECORD_ID, CATEGORY_CODE, BRANCH_CODE, BRANCH_NAME, REGION_CODE, REGION_NAME,
                   ZONE_CODE, ZONE_NAME, STATUS, CREATED_BY, CREATED_DATE, UPDATED_BY, UPDATED_DATE
            FROM RP_SVC_VENDOR_RECORD
            WHERE (:p_cat IS NULL OR CATEGORY_CODE = :p_cat2)
              AND (:p_zone IS NULL OR ZONE_CODE = :p_zone2)
              AND (:p_region IS NULL OR REGION_CODE = :p_region2)
              AND (:p_branch IS NULL OR BRANCH_CODE = :p_branch2)
              AND (:p_status IS NULL OR STATUS = :p_status2)
            ORDER BY CREATED_DATE DESC", cn) { BindByName = true };
        cmd.Parameters.Add("p_cat", (object?)categoryCode ?? DBNull.Value);
        cmd.Parameters.Add("p_cat2", (object?)categoryCode ?? DBNull.Value);
        cmd.Parameters.Add("p_zone", (object?)zoneCode ?? DBNull.Value);
        cmd.Parameters.Add("p_zone2", (object?)zoneCode ?? DBNull.Value);
        cmd.Parameters.Add("p_region", (object?)regionCode ?? DBNull.Value);
        cmd.Parameters.Add("p_region2", (object?)regionCode ?? DBNull.Value);
        cmd.Parameters.Add("p_branch", (object?)branchCode ?? DBNull.Value);
        cmd.Parameters.Add("p_branch2", (object?)branchCode ?? DBNull.Value);
        cmd.Parameters.Add("p_status", (object?)status ?? DBNull.Value);
        cmd.Parameters.Add("p_status2", (object?)status ?? DBNull.Value);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(MapRecordHeader(r));

        // A representative "display title" per record (first mandatory text-ish field's value) —
        // cheap enough to fetch in one extra query for the whole page's worth of records.
        if (result.Count > 0)
        {
            // Prefer a genuinely-distinguishing "*_NAME" field (FIRM_NAME, GUARD_NAME, PSA_NAME, ...) —
            // the lowest-DISPLAY_ORDER non-readonly field overall is often a category/type dropdown
            // (e.g. ARCHITECTS' PURPOSE) that reads the same for every record in the category, which
            // made every row's title identical. Fall back to the old "first non-code field" rule only
            // if a category defines no such *_NAME field.
            var recordIds = result.Select(x => x.RecordId).ToList();

            var idsCondition = string.Join(" OR ", recordIds.Chunk(1000).Select(chunk => $"v.RECORD_ID IN ({string.Join(",", chunk)})"));

            await using var cmd2 = new OracleCommand($@"
                SELECT v.RECORD_ID, v.FIELD_VALUE FROM RP_SVC_VENDOR_FIELD_VALUE v
                JOIN RP_SVC_FIELD_DEFINITION f ON f.FIELD_ID = v.FIELD_ID
                WHERE ({idsCondition}) AND f.DISPLAY_ORDER = (
                    SELECT MIN(f2.DISPLAY_ORDER) FROM RP_SVC_FIELD_DEFINITION f2
                    WHERE f2.CATEGORY_CODE = f.CATEGORY_CODE AND f2.IS_READONLY = 0 AND f2.FIELD_TYPE IN ('TEXT','DROPDOWN')
                      AND f2.FIELD_KEY NOT LIKE '%CODE%'
                      AND f2.FIELD_KEY LIKE '%NAME%'
                      AND EXISTS (
                          SELECT 1 FROM RP_SVC_FIELD_DEFINITION f3
                          WHERE f3.CATEGORY_CODE = f.CATEGORY_CODE AND f3.IS_READONLY = 0 AND f3.FIELD_TYPE IN ('TEXT','DROPDOWN')
                            AND f3.FIELD_KEY NOT LIKE '%CODE%' AND f3.FIELD_KEY LIKE '%NAME%'))
                UNION ALL
                SELECT v.RECORD_ID, v.FIELD_VALUE FROM RP_SVC_VENDOR_FIELD_VALUE v
                JOIN RP_SVC_FIELD_DEFINITION f ON f.FIELD_ID = v.FIELD_ID
                WHERE ({idsCondition}) AND f.DISPLAY_ORDER = (
                    SELECT MIN(f2.DISPLAY_ORDER) FROM RP_SVC_FIELD_DEFINITION f2
                    WHERE f2.CATEGORY_CODE = f.CATEGORY_CODE AND f2.IS_READONLY = 0 AND f2.FIELD_TYPE IN ('TEXT','DROPDOWN')
                      AND f2.FIELD_KEY NOT LIKE '%CODE%')
                  AND NOT EXISTS (
                          SELECT 1 FROM RP_SVC_FIELD_DEFINITION f3
                          WHERE f3.CATEGORY_CODE = f.CATEGORY_CODE AND f3.IS_READONLY = 0 AND f3.FIELD_TYPE IN ('TEXT','DROPDOWN')
                            AND f3.FIELD_KEY NOT LIKE '%CODE%' AND f3.FIELD_KEY LIKE '%NAME%')", cn);
            await using var r2 = await cmd2.ExecuteReaderAsync();
            var titles = new Dictionary<long, string>();
            while (await r2.ReadAsync())
                if (!r2.IsDBNull(1)) titles[r2.GetInt64(0)] = r2.GetString(1);
            foreach (var rec in result)
                rec.DisplayTitle = titles.TryGetValue(rec.RecordId, out var t) ? t : $"Record #{rec.RecordId}";
        }

        return result;
    }

    private static SvcVendorRecord MapRecordHeader(OracleDataReader r) => new()
    {
        RecordId = r.GetInt64(0), CategoryCode = r.GetString(1),
        BranchCode = r.IsDBNull(2) ? null : r.GetString(2), BranchName = r.IsDBNull(3) ? null : r.GetString(3),
        RegionCode = r.IsDBNull(4) ? null : r.GetString(4), RegionName = r.IsDBNull(5) ? null : r.GetString(5),
        ZoneCode = r.IsDBNull(6) ? null : r.GetString(6), ZoneName = r.IsDBNull(7) ? null : r.GetString(7),
        Status = r.GetString(8), CreatedBy = r.GetString(9), CreatedDate = r.GetDateTime(10),
        UpdatedBy = r.IsDBNull(11) ? null : r.GetString(11), UpdatedDate = r.IsDBNull(12) ? null : r.GetDateTime(12)
    };

    public async Task<SvcVendorRecord?> GetVendorRecordDetailAsync(long recordId, bool canSeeUnmasked = true)
    {
        await using var cn = Conn();
        await cn.OpenAsync();

        SvcVendorRecord? record = null;
        await using (var cmd = new OracleCommand(@"
            SELECT RECORD_ID, CATEGORY_CODE, BRANCH_CODE, BRANCH_NAME, REGION_CODE, REGION_NAME,
                   ZONE_CODE, ZONE_NAME, STATUS, CREATED_BY, CREATED_DATE, UPDATED_BY, UPDATED_DATE
            FROM RP_SVC_VENDOR_RECORD WHERE RECORD_ID = :p_id", cn))
        {
            cmd.Parameters.Add("p_id", recordId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) record = MapRecordHeader(r);
        }
        if (record is null) return null;

        // Seed the readonly Branch/Region/Zone name+code fields from the RECORD header columns.
        // These fields are IS_READONLY, so SaveVendorRecordAsync never writes them to the EAV table
        // (RP_SVC_VENDOR_FIELD_VALUE) — it only persists them as header columns on
        // RP_SVC_VENDOR_RECORD. The edit form binds every field via its FIELD_KEY against
        // record.Values, so without this seed those fields render blank on edit even though the
        // values were saved correctly. The composite ZONE_CODE_NAME / REGION_CODE_NAME keys mirror
        // how VendorForm.razor's ValidateSolIdAsync populates them. The EAV loop below runs after
        // this and overrides any key that does have a stored value (e.g. the editable SOL-ID input).
        record.Values["BRANCH_CODE"] = record.BranchCode;
        record.Values["BRANCH_NAME"] = record.BranchName;
        record.Values["REGION_CODE"] = record.RegionCode;
        record.Values["REGION_NAME"] = record.RegionName;
        record.Values["ZONE_CODE"] = record.ZoneCode;
        record.Values["ZONE_NAME"] = record.ZoneName;
        record.Values["ZONE_CODE_NAME"] = string.IsNullOrWhiteSpace(record.ZoneCode) && string.IsNullOrWhiteSpace(record.ZoneName)
            ? null : $"{record.ZoneCode} - {record.ZoneName}";
        record.Values["REGION_CODE_NAME"] = string.IsNullOrWhiteSpace(record.RegionCode) && string.IsNullOrWhiteSpace(record.RegionName)
            ? null : $"{record.RegionCode} - {record.RegionName}";

        // FIELD_KEY, FIELD_VALUE, IS_PII, FIELD_LABEL — the latter two only needed to decide
        // whether/how to mask below; never sent back to the caller as separate columns.
        await using (var cmd = new OracleCommand(@"
            SELECT f.FIELD_KEY, v.FIELD_VALUE, f.IS_PII, f.FIELD_LABEL FROM RP_SVC_VENDOR_FIELD_VALUE v
            JOIN RP_SVC_FIELD_DEFINITION f ON f.FIELD_ID = v.FIELD_ID
            WHERE v.RECORD_ID = :p_id", cn))
        {
            cmd.Parameters.Add("p_id", recordId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var key = r.GetString(0);
                var value = r.IsDBNull(1) ? null : r.GetString(1);
                var isPii = !r.IsDBNull(2) && Convert.ToInt32(r.GetDecimal(2)) == 1;
                var label = r.IsDBNull(3) ? key : r.GetString(3);
                record.Values[key] = (isPii && !canSeeUnmasked) ? PiiMasker.Mask(value, label) : value;
            }
        }

        // If the category has readonly SOL-ID display fields (BRANCH_SOL_ID / REGION_SOL_ID /
        // ZONE_SOL_ID) they were never persisted (readonly ⇒ not in EAV, and the header only keeps
        // branch code + names). Re-resolve the branch from the stored SOL ID (the editable input
        // field's EAV value) so those display fields and the composites show correctly on edit —
        // the same auto-population the form applies on blur. Skipped when the category defines no
        // SOL-ID input or none of those readonly display fields exist, to avoid a needless query.
        var fields = await GetFieldDefinitionsAsync(record.CategoryCode);
        var solIdKey = GetBranchSolIdFieldKey(fields);
        if (record.Values.TryGetValue(solIdKey, out var recSolId) && !string.IsNullOrWhiteSpace(recSolId)
            && fields.Any(f => f.IsReadonly && (f.FieldKey is "BRANCH_SOL_ID" or "REGION_SOL_ID" or "ZONE_SOL_ID")))
        {
            var branch = await GetBranchBySolIdAsync(recSolId.Trim());
            if (branch is not null) ApplyBranchToReadonlyFields(branch, fields, record.Values);
        }

        return record;
    }

    public SvcFieldValidationResult ValidateFieldValues(List<SvcFieldDefinition> fields, Dictionary<string, string?> values)
    {
        var result = new SvcFieldValidationResult { IsValid = true };
        foreach (var f in fields.Where(f => f.IsActive && !f.IsReadonly))
        {
            values.TryGetValue(f.FieldKey, out var raw);
            var val = raw?.Trim();

            if (f.IsMandatory && string.IsNullOrWhiteSpace(val))
            {
                result.Errors.Add($"{f.FieldLabel} is required.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(val)) continue;

            if (f.MaxLength is int max && val.Length > max)
                result.Errors.Add($"{f.FieldLabel} must be at most {max} characters.");

            if (!string.IsNullOrWhiteSpace(f.ValidationRegex))
            {
                try
                {
                    if (!Regex.IsMatch(val, f.ValidationRegex))
                        result.Errors.Add(f.ValidationHint ?? $"{f.FieldLabel} is not in a valid format.");
                }
                catch (ArgumentException)
                {
                    // malformed regex saved via the form builder — don't block the whole save on that
                    _logger.LogWarning("SVC: field {Key} has an invalid validation regex, skipping check", f.FieldKey);
                }
            }

            // Dropdown values must come from the field's own allowed list (STATIC JSON or a
            // resolved Masters lookup — both populate DropdownOptions identically) — "allow
            // those values only which are valid" / "validate uploaded file with masters."
            // Empty DropdownOptions means the list isn't configured yet; don't block on that.
            if (f.FieldType == "DROPDOWN" && f.DropdownOptions.Count > 0
                && !f.DropdownOptions.Any(o => string.Equals(o, val, StringComparison.OrdinalIgnoreCase)))
            {
                result.Errors.Add($"{f.FieldLabel} '{val}' is not one of the allowed values.");
            }
        }
        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    public async Task<(bool Success, long RecordId, List<string> Errors)> SaveVendorRecordAsync(
        long? recordId, string categoryCode, string branchCode, string branchName,
        string regionCode, string regionName, string zoneCode, string zoneName,
        Dictionary<string, string?> fieldValues, string actor)
    {
        var fields = await GetFieldDefinitionsAsync(categoryCode);
        var validation = ValidateFieldValues(fields, fieldValues);
        if (!validation.IsValid) return (false, 0, validation.Errors);

        var dupErrors = await FindDuplicateFieldErrorsAsync(categoryCode, fields, fieldValues, recordId);
        if (dupErrors.Count > 0) return (false, 0, dupErrors);

        await using var cn = Conn();
        await cn.OpenAsync();
        await using var tx = cn.BeginTransaction();

        try
        {
            long theRecordId;
            var isNew = recordId is null;

            if (isNew)
            {
                await using var ins = new OracleCommand(@"
                    INSERT INTO RP_SVC_VENDOR_RECORD (RECORD_ID, CATEGORY_CODE, BRANCH_CODE, BRANCH_NAME,
                        REGION_CODE, REGION_NAME, ZONE_CODE, ZONE_NAME, CREATED_BY)
                    VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_cat, :p_bc, :p_bn, :p_rc, :p_rn, :p_zc, :p_zn, :p_by)
                    RETURNING RECORD_ID INTO :p_outid", cn) { Transaction = tx, BindByName = true };
                ins.Parameters.Add("p_cat", categoryCode);
                ins.Parameters.Add("p_bc", branchCode); ins.Parameters.Add("p_bn", branchName);
                ins.Parameters.Add("p_rc", regionCode); ins.Parameters.Add("p_rn", regionName);
                ins.Parameters.Add("p_zc", zoneCode); ins.Parameters.Add("p_zn", zoneName);
                ins.Parameters.Add("p_by", actor);
                var outId = new OracleParameter("p_outid", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
                ins.Parameters.Add(outId);
                await ins.ExecuteNonQueryAsync();
                theRecordId = Convert.ToInt64(((OracleDecimal)outId.Value).Value);

                await LogAuditAsync(cn, tx, theRecordId, categoryCode, "CREATE", null, null, null, actor);
            }
            else
            {
                theRecordId = recordId!.Value;
                await using var upd = new OracleCommand(@"
                    UPDATE RP_SVC_VENDOR_RECORD SET BRANCH_CODE = :p_bc, BRANCH_NAME = :p_bn,
                        REGION_CODE = :p_rc, REGION_NAME = :p_rn, ZONE_CODE = :p_zc, ZONE_NAME = :p_zn,
                        UPDATED_BY = :p_by, UPDATED_DATE = SYSTIMESTAMP
                    WHERE RECORD_ID = :p_id", cn) { Transaction = tx, BindByName = true };
                upd.Parameters.Add("p_bc", branchCode); upd.Parameters.Add("p_bn", branchName);
                upd.Parameters.Add("p_rc", regionCode); upd.Parameters.Add("p_rn", regionName);
                upd.Parameters.Add("p_zc", zoneCode); upd.Parameters.Add("p_zn", zoneName);
                upd.Parameters.Add("p_by", actor); upd.Parameters.Add("p_id", theRecordId);
                await upd.ExecuteNonQueryAsync();
            }

            // Existing values, for diffing on update (and to know which fields to insert vs update).
            var existing = new Dictionary<long, string?>();
            if (!isNew)
            {
                await using var cmd = new OracleCommand("SELECT FIELD_ID, FIELD_VALUE FROM RP_SVC_VENDOR_FIELD_VALUE WHERE RECORD_ID = :p_id", cn) { Transaction = tx };
                cmd.Parameters.Add("p_id", theRecordId);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) existing[r.GetInt64(0)] = r.IsDBNull(1) ? null : r.GetString(1);
            }

            foreach (var f in fields.Where(f => !f.IsReadonly))
            {
                fieldValues.TryGetValue(f.FieldKey, out var newVal);
                newVal = string.IsNullOrWhiteSpace(newVal) ? null : newVal.Trim();

                var hadExisting = existing.TryGetValue(f.FieldId, out var oldVal);
                if (hadExisting)
                {
                    if (oldVal == newVal) continue;
                    await using var upd = new OracleCommand(
                        "UPDATE RP_SVC_VENDOR_FIELD_VALUE SET FIELD_VALUE = :p_val WHERE RECORD_ID = :p_rid AND FIELD_ID = :p_fid", cn)
                    { Transaction = tx, BindByName = true };
                    upd.Parameters.Add("p_val", (object?)newVal ?? DBNull.Value);
                    upd.Parameters.Add("p_rid", theRecordId); upd.Parameters.Add("p_fid", f.FieldId);
                    await upd.ExecuteNonQueryAsync();
                }
                else if (newVal is not null)
                {
                    await using var ins = new OracleCommand(
                        "INSERT INTO RP_SVC_VENDOR_FIELD_VALUE (RECORD_ID, FIELD_ID, FIELD_VALUE) VALUES (:p_rid, :p_fid, :p_val)", cn)
                    { Transaction = tx, BindByName = true };
                    ins.Parameters.Add("p_rid", theRecordId); ins.Parameters.Add("p_fid", f.FieldId);
                    ins.Parameters.Add("p_val", newVal);
                    await ins.ExecuteNonQueryAsync();
                }
                else
                {
                    continue; // no existing value, no new value — nothing to do
                }

                if (!isNew)
                    await LogAuditAsync(cn, tx, theRecordId, categoryCode, "UPDATE", f.FieldKey, oldVal, newVal, actor);
            }

            tx.Commit();
            return (true, theRecordId, new List<string>());
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "SVC: SaveVendorRecordAsync failed for category {Category}", categoryCode);
            return (false, 0, new List<string> { $"Save failed: {ex.Message}" });
        }
    }

    /// <summary>Checks every IS_UNIQUE field's value against other ACTIVE records in the same
    /// category (case-insensitive exact match). Used by both the single-record form save and
    /// the bulk Excel upload (there, also cross-checked within the batch itself — see
    /// ValidateVendorUploadAsync). Blank values are never treated as duplicates of each other.</summary>
    private async Task<List<string>> FindDuplicateFieldErrorsAsync(
        string categoryCode, List<SvcFieldDefinition> fields, Dictionary<string, string?> fieldValues, long? excludeRecordId)
    {
        var errors = new List<string>();
        var uniqueFields = fields.Where(f => f.IsUnique).ToList();
        if (uniqueFields.Count == 0) return errors;

        await using var cn = Conn();
        await cn.OpenAsync();
        foreach (var f in uniqueFields)
        {
            if (!fieldValues.TryGetValue(f.FieldKey, out var val) || string.IsNullOrWhiteSpace(val)) continue;
            val = val.Trim();

            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*) FROM RP_SVC_VENDOR_FIELD_VALUE v
                JOIN RP_SVC_VENDOR_RECORD r ON r.RECORD_ID = v.RECORD_ID
                WHERE v.FIELD_ID = :p_fid AND r.STATUS = 'ACTIVE' AND UPPER(v.FIELD_VALUE) = UPPER(:p_val)
                  AND (:p_exclude IS NULL OR r.RECORD_ID != :p_exclude2)", cn) { BindByName = true };
            cmd.Parameters.Add("p_fid", f.FieldId);
            cmd.Parameters.Add("p_val", val);
            cmd.Parameters.Add("p_exclude", (object?)excludeRecordId ?? DBNull.Value);
            cmd.Parameters.Add("p_exclude2", (object?)excludeRecordId ?? DBNull.Value);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            if (count > 0)
                errors.Add($"{f.FieldLabel} '{val}' is already used by another {categoryCode} record — this field must be unique.");
        }
        return errors;
    }

    private static async Task LogAuditAsync(OracleConnection cn, OracleTransaction tx, long recordId, string categoryCode,
        string actionType, string? fieldKey, string? oldValue, string? newValue, string actor)
    {
        await using var cmd = new OracleCommand(@"
            INSERT INTO RP_SVC_VENDOR_AUDIT_LOG (AUDIT_ID, RECORD_ID, CATEGORY_CODE, ACTION_TYPE, FIELD_KEY, OLD_VALUE, NEW_VALUE, CHANGED_BY)
            VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_rid, :p_cat, :p_action, :p_key, :p_old, :p_new, :p_by)", cn)
        { Transaction = tx, BindByName = true };
        cmd.Parameters.Add("p_rid", recordId);
        cmd.Parameters.Add("p_cat", categoryCode);
        cmd.Parameters.Add("p_action", actionType);
        cmd.Parameters.Add("p_key", (object?)fieldKey ?? DBNull.Value);
        cmd.Parameters.Add("p_old", (object?)oldValue ?? DBNull.Value);
        cmd.Parameters.Add("p_new", (object?)newValue ?? DBNull.Value);
        cmd.Parameters.Add("p_by", actor);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetVendorRecordStatusAsync(long recordId, string status, string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var tx = cn.BeginTransaction();

        string categoryCode;
        await using (var chk = new OracleCommand("SELECT CATEGORY_CODE FROM RP_SVC_VENDOR_RECORD WHERE RECORD_ID = :p_id", cn) { Transaction = tx })
        {
            chk.Parameters.Add("p_id", recordId);
            var res = await chk.ExecuteScalarAsync();
            if (res is null) { tx.Rollback(); return; }
            categoryCode = res.ToString()!;
        }

        await using (var upd = new OracleCommand(
            "UPDATE RP_SVC_VENDOR_RECORD SET STATUS = :p_status, UPDATED_BY = :p_by, UPDATED_DATE = SYSTIMESTAMP WHERE RECORD_ID = :p_id", cn)
        { Transaction = tx, BindByName = true })
        {
            upd.Parameters.Add("p_status", status); upd.Parameters.Add("p_by", actor); upd.Parameters.Add("p_id", recordId);
            await upd.ExecuteNonQueryAsync();
        }

        await LogAuditAsync(cn, tx, recordId, categoryCode, status == "ACTIVE" ? "REACTIVATE" : "DEACTIVATE", null, null, null, actor);
        tx.Commit();
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Audit log
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<SvcAuditEntry>> GetAuditLogAsync(string? categoryCode, DateTime? fromDate, DateTime? toDate, int maxRows = 200)
    {
        var result = new List<SvcAuditEntry>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(@"
            SELECT * FROM (
                SELECT AUDIT_ID, RECORD_ID, CATEGORY_CODE, ACTION_TYPE, FIELD_KEY, OLD_VALUE, NEW_VALUE, CHANGED_BY, CHANGED_AT
                FROM RP_SVC_VENDOR_AUDIT_LOG
                WHERE (:p_cat IS NULL OR CATEGORY_CODE = :p_cat2)
                  AND (:p_from IS NULL OR CHANGED_AT >= :p_from2)
                  AND (:p_to IS NULL OR CHANGED_AT <= :p_to2)
                ORDER BY CHANGED_AT DESC
            ) WHERE ROWNUM <= :p_max", cn) { BindByName = true };
        cmd.Parameters.Add("p_cat", (object?)categoryCode ?? DBNull.Value);
        cmd.Parameters.Add("p_cat2", (object?)categoryCode ?? DBNull.Value);
        cmd.Parameters.Add("p_from", (object?)fromDate ?? DBNull.Value);
        cmd.Parameters.Add("p_from2", (object?)fromDate ?? DBNull.Value);
        cmd.Parameters.Add("p_to", (object?)toDate ?? DBNull.Value);
        cmd.Parameters.Add("p_to2", (object?)toDate ?? DBNull.Value);
        cmd.Parameters.Add("p_max", maxRows);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new SvcAuditEntry
            {
                AuditId = r.GetInt64(0), RecordId = r.GetInt64(1), CategoryCode = r.GetString(2), ActionType = r.GetString(3),
                FieldKey = r.IsDBNull(4) ? null : r.GetString(4), OldValue = r.IsDBNull(5) ? null : r.GetString(5),
                NewValue = r.IsDBNull(6) ? null : r.GetString(6), ChangedBy = r.GetString(7), ChangedAt = r.GetDateTime(8)
            });
        }
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Staff directory
    // ══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Staff Directory rows, now sourced live from the HRMS <c>StaffDetails</c> table in
    /// OrganizationsDB (SQL Server, <c>SQLServerConnection</c>) instead of the old Oracle
    /// RP_SVC_STAFF_SNAPSHOT snapshot. Only active staff (<c>HR_STATUS = 'A'</c>) who have a
    /// Category (<c>UBI_SPCL_OFFICER</c> not null/blank) are returned.
    ///
    /// Column mapping (only the fields the Staff Directory UI displays):
    ///   EMPLID          → EmpId
    ///   NAME            → EmpName
    ///   UBI_SPCL_OFFICER→ StaffCategoryCode (Category)
    ///   DESCR           → BranchName
    ///   REGION_NAME     → RegionName
    ///   DIVISION_NAME   → ZoneName
    ///   EMP_SCALE_DESCR → ScaleDesc
    ///   EMP_DESGN_DESC  → DesignationDesc
    ///   PHONE           → MobileNumber   (PII-masked when canSeeUnmasked is false)
    ///   EMAIL           → EmailAddress   (PII-masked when canSeeUnmasked is false)
    /// Specialization (SpecialistType) is intentionally left unmapped/commented out until the
    /// correct StaffDetails column is confirmed.
    ///
    /// Zone/region/branch filters take the display *name* (matching GetStaffFilterOptionsAsync,
    /// populated from the same columns), so filtering stays consistent with what's shown.
    /// </summary>
    public async Task<List<SvcStaffRow>> GetStaffAsync(string? categoryCode, string? zoneName, string? regionName, string? branchName, string? specialistType, string? scaleDesc, bool canSeeUnmasked = true)
    {
        var result = new List<SvcStaffRow>();
        await using var cn = SqlConn();
        await using var cmd = new SqlCommand(@"
            SELECT EMPLID, NAME, UBI_SPCL_OFFICER, DESCR, REGION_NAME, DIVISION_NAME,
                   EMP_SCALE_DESCR, EMP_DESGN_DESC, PHONE, EMAIL
            FROM StaffDetails
            WHERE HR_STATUS = 'A'
              AND UBI_SPCL_OFFICER IS NOT NULL AND LTRIM(RTRIM(UBI_SPCL_OFFICER)) <> ''
              AND (@p_cat    IS NULL OR UBI_SPCL_OFFICER = @p_cat)
              AND (@p_zone   IS NULL OR DIVISION_NAME    = @p_zone)
              AND (@p_region IS NULL OR REGION_NAME      = @p_region)
              AND (@p_branch IS NULL OR DESCR            = @p_branch)
              AND (@p_scale  IS NULL OR EMP_SCALE_DESCR  = @p_scale)
            ORDER BY DIVISION_NAME, REGION_NAME, DESCR, NAME", cn);
        cmd.Parameters.AddWithValue("@p_cat", (object?)categoryCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p_zone", (object?)zoneName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p_region", (object?)regionName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p_branch", (object?)branchName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p_scale", (object?)scaleDesc ?? DBNull.Value);
        // Specialization filter (specialistType) is intentionally not applied — the Specialization
        // field/column mapping is deferred (see below).
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        string? Str(int i) => r.IsDBNull(i) ? null : r.GetString(i);
        while (await r.ReadAsync())
        {
            result.Add(new SvcStaffRow
            {
                EmpId = r.GetString(0),
                EmpName = Str(1) ?? "",
                StaffCategoryCode = Str(2) ?? "",           // UBI_SPCL_OFFICER = Category
                // SpecialistType (Specialization) — DB mapping to be confirmed later.
                // SpecialistType = Str(?),
                BranchName = Str(3),                          // DESCR
                RegionName = Str(4),                          // REGION_NAME
                ZoneName = Str(5),                            // DIVISION_NAME
                ScaleDesc = Str(6),                           // EMP_SCALE_DESCR
                DesignationDesc = Str(7),                     // EMP_DESGN_DESC
                MobileNumber = canSeeUnmasked ? Str(8) : PiiMasker.MaskPhone(Str(8)),   // PHONE
                EmailAddress = canSeeUnmasked ? Str(9) : PiiMasker.MaskEmail(Str(9))    // EMAIL
            });
        }
        return result;
    }

    public async Task<(List<string> Zones, List<string> Regions, List<string> Branches, List<string> Specializations, List<string> Scales)> GetStaffFilterOptionsAsync(string? categoryCode)
    {
        // Filter dropdowns are populated from the SAME StaffDetails rows the directory shows, with
        // identical WHERE constraints (HR_STATUS='A', UBI_SPCL_OFFICER present), so every option is
        // guaranteed to match at least one visible row.
        async Task<List<string>> Distinct(SqlConnection cn, string column)
        {
            var list = new List<string>();
            var sql =
                $"SELECT DISTINCT {column} FROM StaffDetails " +
                "WHERE HR_STATUS = 'A' " +
                "AND UBI_SPCL_OFFICER IS NOT NULL AND LTRIM(RTRIM(UBI_SPCL_OFFICER)) <> '' " +
                $"AND {column} IS NOT NULL AND LTRIM(RTRIM({column})) <> '' " +
                (categoryCode is not null ? "AND UBI_SPCL_OFFICER = @p_cat " : "") +
                $"ORDER BY {column}";
            await using var cmd = new SqlCommand(sql, cn);
            if (categoryCode is not null) cmd.Parameters.AddWithValue("@p_cat", categoryCode);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(r.GetString(0));
            return list;
        }

        await using var cn = SqlConn();
        await cn.OpenAsync();
        return (
            await Distinct(cn, "DIVISION_NAME"),   // Zone
            await Distinct(cn, "REGION_NAME"),     // Region
            await Distinct(cn, "DESCR"),           // Branch
            // Specializations — deferred until the StaffDetails column is confirmed. Returned empty
            // so the (now hidden) Specialization dropdown has no static values.
            new List<string>(),
            await Distinct(cn, "EMP_SCALE_DESCR")  // Scale
        );
    }

    public async Task<List<string>> GetStaffCategoriesAsync()
    {
        var list = new List<string>();
        await using var cn = SqlConn();
        await using var cmd = new SqlCommand(
            "SELECT DISTINCT UBI_SPCL_OFFICER FROM StaffDetails " +
            "WHERE HR_STATUS = 'A' " +
            "AND UBI_SPCL_OFFICER IS NOT NULL AND LTRIM(RTRIM(UBI_SPCL_OFFICER)) <> '' " +
            "ORDER BY UBI_SPCL_OFFICER", cn);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Dropdown source helpers
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<(string Code, string Name)>> GetBranchListAsync()
    {
        var result = new List<(string, string)>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand("SELECT BRANCH_CODE, BRANCH_NAME FROM BRANCH_MASTER WHERE BRANCH_CODE IS NOT NULL ORDER BY BRANCH_NAME", cn);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            if (!r.IsDBNull(0)) result.Add((r.GetString(0), r.IsDBNull(1) ? r.GetString(0) : r.GetString(1)));
        return result;
    }

    public async Task<List<SvcBranchInfo>> GetBranchDetailsAsync()
    {
        var result = new List<SvcBranchInfo>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "SELECT BRANCH_CODE, BRANCH_NAME, REGION_CODE, REGION_NAME, ZONE_CODE, ZONE_NAME FROM BRANCH_MASTER WHERE BRANCH_CODE IS NOT NULL ORDER BY BRANCH_NAME", cn);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new SvcBranchInfo
            {
                BranchCode = r.GetString(0), BranchName = r.IsDBNull(1) ? r.GetString(0) : r.GetString(1),
                RegionCode = r.IsDBNull(2) ? "" : r.GetString(2), RegionName = r.IsDBNull(3) ? "" : r.GetString(3),
                ZoneCode = r.IsDBNull(4) ? "" : r.GetString(4), ZoneName = r.IsDBNull(5) ? "" : r.GetString(5)
            });
        }
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Report
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<(List<SvcFieldDefinition> Columns, List<SvcVendorRecord> Rows)> GetVendorReportAsync(string categoryCode, string? zoneCode, string? regionCode, string? branchCode)
    {
        var columns = await GetFieldDefinitionsAsync(categoryCode);
        var headers = await GetVendorRecordsAsync(categoryCode, zoneCode, regionCode, branchCode);

        await using var cn = Conn();
        await cn.OpenAsync();
        if (headers.Count > 0)
        {
            var ids = string.Join(",", headers.Select(h => h.RecordId));
            await using var cmd = new OracleCommand($@"
                SELECT v.RECORD_ID, f.FIELD_KEY, v.FIELD_VALUE
                FROM RP_SVC_VENDOR_FIELD_VALUE v
                JOIN RP_SVC_FIELD_DEFINITION f ON f.FIELD_ID = v.FIELD_ID
                WHERE v.RECORD_ID IN ({ids})", cn);
            await using var r = await cmd.ExecuteReaderAsync();
            var byRecord = headers.ToDictionary(h => h.RecordId);
            while (await r.ReadAsync())
            {
                var rid = r.GetInt64(0);
                if (byRecord.TryGetValue(rid, out var rec) && !r.IsDBNull(2))
                    rec.Values[r.GetString(1)] = r.GetString(2);
            }
        }
        return (columns, headers);
    }

    public async Task<byte[]> ExportVendorReportToExcelAsync(string categoryCode, string? zoneCode, string? regionCode, string? branchCode)
    {
        var (columns, rows) = await GetVendorReportAsync(categoryCode, zoneCode, regionCode, branchCode);
        var category = await GetCategoryAsync(categoryCode);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add((category?.CategoryName ?? categoryCode).Length > 31
            ? categoryCode[..31] : (category?.CategoryName ?? categoryCode));

        for (int c = 0; c < columns.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = columns[c].FieldLabel;
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
        }
        ws.Cell(1, columns.Count + 1).Value = "Status";
        ws.Cell(1, columns.Count + 2).Value = "Created By";
        ws.Cell(1, columns.Count + 3).Value = "Created Date";
        ws.Range(1, 1, 1, columns.Count + 3).Style.Font.SetBold();

        for (int rIdx = 0; rIdx < rows.Count; rIdx++)
        {
            var row = rows[rIdx];
            for (int c = 0; c < columns.Count; c++)
            {
                row.Values.TryGetValue(columns[c].FieldKey, out var val);
                ws.Cell(rIdx + 2, c + 1).Value = val ?? "";
            }
            ws.Cell(rIdx + 2, columns.Count + 1).Value = row.Status;
            ws.Cell(rIdx + 2, columns.Count + 2).Value = row.CreatedBy;
            ws.Cell(rIdx + 2, columns.Count + 3).Value = row.CreatedDate.ToString("dd-MMM-yyyy HH:mm");
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Bulk Excel upload — Download Template / Upload+Validate / Submit / Exception Report
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<byte[]> GenerateVendorTemplateAsync(string categoryCode)
    {
        var fields = await GetFieldDefinitionsAsync(categoryCode);
        var editable = fields.Where(f => f.IsActive && !f.IsReadonly).OrderBy(f => f.DisplayOrder).ToList();

        using var workbook = new XLWorkbook();

        // Data sheet: headers are the machine FIELD_KEY (not the display label) so re-upload can
        // match columns reliably even if a label gets tweaked later in Form Builder — the
        // Instructions sheet is where the human-friendly description lives.
        var data = workbook.Worksheets.Add("Data");
        for (int c = 0; c < editable.Count; c++)
        {
            var cell = data.Cell(1, c + 1);
            cell.Value = editable[c].FieldKey;
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
        }
        data.SheetView.FreezeRows(1);
        if (editable.Count > 0) data.Columns(1, editable.Count).AdjustToContents();

        // Real Excel data validation (native dropdown/list picker) for every DROPDOWN column
        // with a resolved option list (STATIC JSON or a Masters lookup) — "data validations
        // should be there in dropdown fields ... allow those values only which are valid in the
        // template." A literal (non-range) list formula must be wrapped in explicit quotes or
        // Excel's own schema validator silently strips the whole data-validation feature on open
        // (same fix already applied to Positive Branch List's Parameter dropdown) — but a literal
        // list also has a hard ~255-char formula limit, which silently dropped the dropdown
        // picker for anything with enough options to exceed it (36 Indian states alone is already
        // past it — "state validation also [not there] in the template"). A hidden "Lists" sheet
        // with one option per row, referenced by range, has no such limit and covers every
        // DROPDOWN column the same way regardless of how many options it has.
        const int templateDataRows = 500;
        var lists = workbook.Worksheets.Add("Lists");
        lists.Visibility = XLWorksheetVisibility.VeryHidden;
        int listCol = 1;
        for (int c = 0; c < editable.Count; c++)
        {
            var f = editable[c];
            if (f.FieldType != "DROPDOWN" || f.DropdownOptions.Count == 0) continue;

            for (int i = 0; i < f.DropdownOptions.Count; i++)
                lists.Cell(i + 1, listCol).Value = f.DropdownOptions[i];
            var optionsRange = lists.Range(1, listCol, f.DropdownOptions.Count, listCol);
            listCol++;

            var range = data.Range(2, c + 1, templateDataRows + 1, c + 1);
            range.SetDataValidation().List(optionsRange);
        }

        var instr = workbook.Worksheets.Add("Instructions");
        string[] headers = { "Column (must match Data sheet header exactly)", "Label", "Type", "Mandatory", "Unique", "Validation Hint", "Dropdown Options" };
        for (int c = 0; c < headers.Length; c++)
        {
            instr.Cell(1, c + 1).Value = headers[c];
            instr.Cell(1, c + 1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
        }
        for (int i = 0; i < editable.Count; i++)
        {
            var f = editable[i];
            int row = i + 2;

            // The branch field (DROPDOWN_SOURCE='BRANCH') is filled with a SOL ID — the single-record
            // form relabels it "SOL ID", so present it the same way in the template's Instructions
            // (Label + Hint) even if its DB FIELD_LABEL still reads "Branch Code". The Data-sheet
            // header stays the raw FIELD_KEY (now BRANCH_SOL_ID, or legacy BRANCH_CODE) so the upload
            // matches columns by key. Branch/Region/Zone name+code are auto-filled from this SOL ID
            // on upload, so they're (correctly) absent from the template as readonly fields.
            var isBranchSolId = f.DropdownSource == "BRANCH";
            instr.Cell(row, 1).Value = f.FieldKey;
            instr.Cell(row, 2).Value = isBranchSolId ? "SOL ID" : f.FieldLabel;
            instr.Cell(row, 3).Value = f.FieldType;
            instr.Cell(row, 4).Value = f.IsMandatory ? "Yes" : "";
            instr.Cell(row, 5).Value = f.IsUnique ? "Yes — no two vendor records may share this value" : "";
            instr.Cell(row, 6).Value = isBranchSolId
                ? "Enter the branch SOL ID. Branch/Region/Zone details are auto-filled from it on upload."
                : (f.ValidationHint ?? "");
            instr.Cell(row, 7).Value = f.DropdownOptions.Count > 0 ? string.Join(" | ", f.DropdownOptions) : "";
        }
        instr.Cell(1, 1).WorksheetColumn().Width = 32;
        instr.Columns(2, headers.Length).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<SvcBulkUploadResult> ValidateVendorUploadAsync(string categoryCode, Stream fileStream, string actor)
    {
        var result = new SvcBulkUploadResult();
        var fields = await GetFieldDefinitionsAsync(categoryCode);
        var editable = fields.Where(f => f.IsActive && !f.IsReadonly).ToList();

        // Branch resolution now goes through the Branch Diary DB by SOL ID (same as the single-record
        // form), so we look each SOL ID up on demand and cache within this upload to avoid re-querying
        // the same branch for every row that uses it. The SOL-ID column is whatever editable field
        // has DROPDOWN_SOURCE='BRANCH' (FIELD_KEY 'BRANCH_SOL_ID', or legacy 'BRANCH_CODE').
        var solIdKey = GetBranchSolIdFieldKey(fields);
        var branchCache = new Dictionary<string, SvcBranchInfo?>(StringComparer.OrdinalIgnoreCase);

        using var workbook = new XLWorkbook(fileStream);
        if (!workbook.Worksheets.Contains("Data"))
        {
            result.FileErrors.Add("The uploaded file has no 'Data' sheet — please fill in the downloaded template rather than a different file.");
            return result;
        }
        var ws = workbook.Worksheet("Data");

        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        var colToKey = new Dictionary<int, string>();
        for (int c = 1; c <= lastCol; c++)
        {
            var key = ws.Cell(1, c).GetString().Trim();
            if (!string.IsNullOrEmpty(key)) colToKey[c] = key;
        }
        if (colToKey.Count == 0)
        {
            result.FileErrors.Add("The 'Data' sheet has no header row.");
            return result;
        }

        var knownKeys = editable.Select(f => f.FieldKey).ToHashSet();
        var unknownCols = colToKey.Values.Where(k => !knownKeys.Contains(k)).Distinct().ToList();
        if (unknownCols.Count > 0)
            result.FileErrors.Add($"Unrecognized column header(s): {string.Join(", ", unknownCols)}. Use the downloaded template's headers exactly (see the Instructions sheet).");

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var seenInFile = new Dictionary<string, Dictionary<string, int>>();

        for (int r = 2; r <= lastRow; r++)
        {
            var rowIsEmpty = colToKey.Keys.All(c => string.IsNullOrWhiteSpace(ws.Cell(r, c).GetString()));
            if (rowIsEmpty) continue;

            var values = new Dictionary<string, string?>();
            foreach (var (c, key) in colToKey)
            {
                var s = ws.Cell(r, c).GetString().Trim();
                values[key] = s.Length > 0 ? s : null;
            }

            var rowResult = new SvcBulkUploadRow { RowNumber = r, Values = values };

            // Auto-populate this category's readonly Branch/Region/Zone fields from the SOL ID the
            // user entered — exactly the same path the single-record form uses (GetBranchBySolIdAsync
            // + ApplyBranchToReadonlyFields), just looked up per row against the Branch Diary DB and
            // cached. The template's editable SOL-ID column (solIdKey) carries the SOL ID; readonly
            // columns aren't in the template, so this is the only place they get filled for a bulk
            // upload.
            if (values.TryGetValue(solIdKey, out var solId) && !string.IsNullOrWhiteSpace(solId))
            {
                solId = solId.Trim();
                values[solIdKey] = solId;   // normalise the stored SOL ID
                if (!branchCache.TryGetValue(solId, out var branch))
                    branchCache[solId] = branch = await GetBranchBySolIdAsync(solId);

                if (branch is null)
                    rowResult.Errors.Add($"SOL ID '{solId}' was not found.");
                else
                    ApplyBranchToReadonlyFields(branch, fields, values);
            }

            var validation = ValidateFieldValues(fields, values);
            rowResult.Errors.AddRange(validation.Errors);

            // Duplicate check — within this file first (no DB round trip needed), then against
            // existing ACTIVE records.
            foreach (var f in fields.Where(f => f.IsUnique))
            {
                if (!values.TryGetValue(f.FieldKey, out var val) || string.IsNullOrWhiteSpace(val)) continue;
                val = val.Trim();
                if (!seenInFile.TryGetValue(f.FieldKey, out var seen))
                    seenInFile[f.FieldKey] = seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (seen.TryGetValue(val, out var firstRow))
                    rowResult.Errors.Add($"{f.FieldLabel} '{val}' is duplicated with row {firstRow} in this file.");
                else
                    seen[val] = r;
            }
            rowResult.Errors.AddRange(await FindDuplicateFieldErrorsAsync(categoryCode, fields, values, null));

            result.Rows.Add(rowResult);
        }

        if (result.Rows.Count == 0 && result.FileErrors.Count == 0)
            result.FileErrors.Add("No data rows found below the header row.");

        return result;
    }

    public async Task<SvcBulkUploadResult> SubmitVendorUploadAsync(string categoryCode, List<SvcBulkUploadRow> rows, string actor)
    {
        var result = new SvcBulkUploadResult();
        var fields = await GetFieldDefinitionsAsync(categoryCode);
        var solIdKey = GetBranchSolIdFieldKey(fields);
        var branchCache = new Dictionary<string, SvcBranchInfo?>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var rowResult = new SvcBulkUploadRow { RowNumber = row.RowNumber, Values = row.Values };
            if (!row.IsValid)
            {
                rowResult.Errors.Add("Skipped — this row still had validation errors at submit time.");
                result.Rows.Add(rowResult);
                continue;
            }

            // Re-resolve the branch from the row's SOL ID so the header columns are populated the
            // same way as the single-record form, regardless of which readonly Branch/Region/Zone
            // fields the category happens to define. This also re-fills the row's readonly field
            // values in case the preview grid was edited before submit. The header BRANCH_CODE
            // column stores the ACTUAL branch code (from the Branch Diary DB), not the SOL ID.
            string branchCode = "", branchName = "", regionCode = "", regionName = "", zoneCode = "", zoneName = "";
            if (row.Values.TryGetValue(solIdKey, out var solId) && !string.IsNullOrWhiteSpace(solId))
            {
                solId = solId.Trim();
                if (!branchCache.TryGetValue(solId, out var branch))
                    branchCache[solId] = branch = await GetBranchBySolIdAsync(solId);

                if (branch is null)
                {
                    rowResult.Errors.Add($"SOL ID '{solId}' was not found.");
                    result.Rows.Add(rowResult);
                    continue;
                }

                ApplyBranchToReadonlyFields(branch, fields, row.Values);
                branchCode = branch.BranchCode; branchName = branch.BranchName;
                regionCode = branch.RegionCode; regionName = branch.RegionName;
                zoneCode = branch.ZoneCode; zoneName = branch.ZoneName;
            }

            var (success, _, errors) = await SaveVendorRecordAsync(
                null, categoryCode, branchCode, branchName, regionCode, regionName,
                zoneCode, zoneName, row.Values, actor);

            if (!success) rowResult.Errors.AddRange(errors);
            result.Rows.Add(rowResult);
        }
        return result;
    }

    public async Task<byte[]> GenerateExceptionReportAsync(string categoryCode, SvcBulkUploadResult result)
    {
        var fields = (await GetFieldDefinitionsAsync(categoryCode)).Where(f => f.IsActive && !f.IsReadonly).OrderBy(f => f.DisplayOrder).ToList();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Exceptions");

        for (int c = 0; c < fields.Count; c++)
        {
            ws.Cell(1, c + 1).Value = fields[c].FieldLabel;
            ws.Cell(1, c + 1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
        }
        ws.Cell(1, fields.Count + 1).Value = "Row";
        ws.Cell(1, fields.Count + 2).Value = "Status";
        ws.Cell(1, fields.Count + 3).Value = "Errors";
        ws.Range(1, 1, 1, fields.Count + 3).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);

        int outRow = 2;
        foreach (var row in result.Rows)
        {
            for (int c = 0; c < fields.Count; c++)
            {
                row.Values.TryGetValue(fields[c].FieldKey, out var val);
                ws.Cell(outRow, c + 1).Value = val ?? "";
            }
            ws.Cell(outRow, fields.Count + 1).Value = row.RowNumber;
            ws.Cell(outRow, fields.Count + 2).Value = row.IsValid ? "OK" : "ERROR";
            ws.Cell(outRow, fields.Count + 3).Value = string.Join(" | ", row.Errors);
            if (!row.IsValid)
                ws.Row(outRow).Style.Fill.SetBackgroundColor(XLColor.MistyRose);
            outRow++;
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Masters
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<SvcMasterType>> GetMasterTypesAsync()
    {
        var result = new List<SvcMasterType>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "SELECT MASTER_TYPE_CODE, MASTER_TYPE_NAME, PARENT_TYPE_CODE, IS_ACTIVE FROM RP_SVC_MASTER_TYPE ORDER BY MASTER_TYPE_NAME", cn);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            result.Add(new SvcMasterType
            {
                MasterTypeCode = r.GetString(0), MasterTypeName = r.GetString(1),
                ParentTypeCode = r.IsDBNull(2) ? null : r.GetString(2),
                IsActive = Convert.ToInt32(r.GetDecimal(3)) == 1
            });
        return result;
    }

    public async Task UpsertMasterTypeAsync(SvcMasterType type, string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand(@"
            MERGE INTO RP_SVC_MASTER_TYPE t USING (SELECT :p_code AS CODE FROM DUAL) s ON (t.MASTER_TYPE_CODE = s.CODE)
            WHEN MATCHED THEN UPDATE SET MASTER_TYPE_NAME = :p_name, PARENT_TYPE_CODE = :p_parent, IS_ACTIVE = :p_active
            WHEN NOT MATCHED THEN INSERT (MASTER_TYPE_CODE, MASTER_TYPE_NAME, PARENT_TYPE_CODE, IS_ACTIVE, CREATED_BY)
                VALUES (:p_code, :p_name, :p_parent, :p_active, :p_by)", cn) { BindByName = true };
        cmd.Parameters.Add("p_code", type.MasterTypeCode);
        cmd.Parameters.Add("p_name", type.MasterTypeName);
        cmd.Parameters.Add("p_parent", (object?)type.ParentTypeCode ?? DBNull.Value);
        cmd.Parameters.Add("p_active", type.IsActive ? 1 : 0);
        cmd.Parameters.Add("p_by", actor);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<SvcMasterValue>> GetMasterValuesAsync(string masterTypeCode)
    {
        var result = new List<SvcMasterValue>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(@"
            SELECT v.MASTER_ID, v.MASTER_TYPE_CODE, v.VALUE_LABEL, v.PARENT_VALUE_ID, p.VALUE_LABEL, v.DISPLAY_ORDER, v.IS_ACTIVE
            FROM RP_SVC_MASTER_VALUE v
            LEFT JOIN RP_SVC_MASTER_VALUE p ON p.MASTER_ID = v.PARENT_VALUE_ID
            WHERE v.MASTER_TYPE_CODE = :p_type
            ORDER BY v.DISPLAY_ORDER, v.VALUE_LABEL", cn);
        cmd.Parameters.Add("p_type", masterTypeCode);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            result.Add(new SvcMasterValue
            {
                MasterId = r.GetInt64(0), MasterTypeCode = r.GetString(1), ValueLabel = r.GetString(2),
                ParentValueId = r.IsDBNull(3) ? null : r.GetInt64(3), ParentValueLabel = r.IsDBNull(4) ? null : r.GetString(4),
                DisplayOrder = Convert.ToInt32(r.GetDecimal(5)), IsActive = Convert.ToInt32(r.GetDecimal(6)) == 1
            });
        return result;
    }

    public async Task<long> UpsertMasterValueAsync(SvcMasterValue value, string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        if (value.MasterId > 0)
        {
            await using var upd = new OracleCommand(@"
                UPDATE RP_SVC_MASTER_VALUE SET VALUE_LABEL = :p_label, PARENT_VALUE_ID = :p_parent,
                    DISPLAY_ORDER = :p_order, IS_ACTIVE = :p_active, UPDATED_BY = :p_by, UPDATED_DATE = SYSTIMESTAMP
                WHERE MASTER_ID = :p_id", cn) { BindByName = true };
            upd.Parameters.Add("p_label", value.ValueLabel);
            upd.Parameters.Add("p_parent", (object?)value.ParentValueId ?? DBNull.Value);
            upd.Parameters.Add("p_order", value.DisplayOrder);
            upd.Parameters.Add("p_active", value.IsActive ? 1 : 0);
            upd.Parameters.Add("p_by", actor);
            upd.Parameters.Add("p_id", value.MasterId);
            await upd.ExecuteNonQueryAsync();
            return value.MasterId;
        }
        else
        {
            await using var ins = new OracleCommand(@"
                INSERT INTO RP_SVC_MASTER_VALUE (MASTER_ID, MASTER_TYPE_CODE, VALUE_LABEL, PARENT_VALUE_ID, DISPLAY_ORDER, CREATED_BY)
                VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_type, :p_label, :p_parent, :p_order, :p_by)
                RETURNING MASTER_ID INTO :p_outid", cn) { BindByName = true };
            ins.Parameters.Add("p_type", value.MasterTypeCode);
            ins.Parameters.Add("p_label", value.ValueLabel);
            ins.Parameters.Add("p_parent", (object?)value.ParentValueId ?? DBNull.Value);
            ins.Parameters.Add("p_order", value.DisplayOrder);
            ins.Parameters.Add("p_by", actor);
            var outId = new OracleParameter("p_outid", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            ins.Parameters.Add(outId);
            await ins.ExecuteNonQueryAsync();
            return Convert.ToInt64(((OracleDecimal)outId.Value).Value);
        }
    }

    public async Task SetMasterValueActiveAsync(long masterId, bool isActive, string actor)
    {
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "UPDATE RP_SVC_MASTER_VALUE SET IS_ACTIVE = :p_active, UPDATED_BY = :p_by, UPDATED_DATE = SYSTIMESTAMP WHERE MASTER_ID = :p_id", cn)
        { BindByName = true };
        cmd.Parameters.Add("p_active", isActive ? 1 : 0);
        cmd.Parameters.Add("p_by", actor);
        cmd.Parameters.Add("p_id", masterId);
        await cn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<byte[]> ExportMasterValuesAsync(string masterTypeCode)
    {
        var values = await GetMasterValuesAsync(masterTypeCode);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Values");
        ws.Cell(1, 1).Value = "Value Label"; ws.Cell(1, 2).Value = "Parent Label"; ws.Cell(1, 3).Value = "Display Order"; ws.Cell(1, 4).Value = "Active";
        ws.Range(1, 1, 1, 4).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
        for (int i = 0; i < values.Count; i++)
        {
            var v = values[i];
            ws.Cell(i + 2, 1).Value = v.ValueLabel;
            ws.Cell(i + 2, 2).Value = v.ParentValueLabel ?? "";
            ws.Cell(i + 2, 3).Value = v.DisplayOrder;
            ws.Cell(i + 2, 4).Value = v.IsActive ? "Yes" : "No";
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<SvcBulkUploadResult> ImportMasterValuesAsync(string masterTypeCode, Stream fileStream, string actor)
    {
        var result = new SvcBulkUploadResult();
        var types = await GetMasterTypesAsync();
        var type = types.FirstOrDefault(t => t.MasterTypeCode == masterTypeCode);
        if (type is null) { result.FileErrors.Add("Unknown master type."); return result; }

        var existing = await GetMasterValuesAsync(masterTypeCode);
        var existingLabels = existing.Select(v => v.ValueLabel).ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<SvcMasterValue> parentValues = new();
        if (!string.IsNullOrEmpty(type.ParentTypeCode))
            parentValues = await GetMasterValuesAsync(type.ParentTypeCode);

        using var workbook = new XLWorkbook(fileStream);
        if (!workbook.Worksheets.Contains("Values"))
        {
            result.FileErrors.Add("The uploaded file has no 'Values' sheet — use the downloaded template.");
            return result;
        }
        var ws = workbook.Worksheet("Values");
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int r = 2; r <= lastRow; r++)
        {
            var label = ws.Cell(r, 1).GetString().Trim();
            var parentLabel = ws.Cell(r, 2).GetString().Trim();
            var orderText = ws.Cell(r, 3).GetString().Trim();
            if (string.IsNullOrWhiteSpace(label)) continue;

            var row = new SvcBulkUploadRow { RowNumber = r, Values = new() { ["ValueLabel"] = label, ["ParentLabel"] = parentLabel } };

            if (existingLabels.Contains(label))
                row.Errors.Add($"'{label}' already exists in this master type.");
            if (!seen.Add(label))
                row.Errors.Add($"'{label}' is duplicated in this file.");

            long? parentId = null;
            if (!string.IsNullOrEmpty(type.ParentTypeCode))
            {
                if (string.IsNullOrWhiteSpace(parentLabel))
                    row.Errors.Add($"Parent Label is required ({type.ParentTypeCode}).");
                else
                {
                    var parent = parentValues.FirstOrDefault(p => string.Equals(p.ValueLabel, parentLabel, StringComparison.OrdinalIgnoreCase));
                    if (parent is null) row.Errors.Add($"Parent Label '{parentLabel}' was not found in {type.ParentTypeCode}.");
                    else parentId = parent.MasterId;
                }
            }

            if (row.IsValid)
            {
                await UpsertMasterValueAsync(new SvcMasterValue
                {
                    MasterTypeCode = masterTypeCode, ValueLabel = label, ParentValueId = parentId,
                    DisplayOrder = int.TryParse(orderText, out var o) ? o : 0
                }, actor);
                existingLabels.Add(label);
            }
            result.Rows.Add(row);
        }
        if (result.Rows.Count == 0) result.FileErrors.Add("No data rows found below the header row.");
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Verticals (reuses RequestPortal's RP_M_VERTICAL — same Oracle schema/instance as My Diary)
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<SvcVertical>> GetActiveVerticalsAsync()
    {
        var result = new List<SvcVertical>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "SELECT ID, CODE, NAME FROM RP_M_VERTICAL WHERE IS_ACTIVE = 1 AND TYPE = '1021' ORDER BY NAME", cn);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            result.Add(new SvcVertical { Id = Convert.ToInt64(r.GetDecimal(0)), Code = r.GetString(1), Name = r.GetString(2) });
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Vendor Category admin block (Masters.razor pseudo master-type "Vendor Category")
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task SetCategoryActiveAsync(string categoryCode, bool isActive, string actor)
    {
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "UPDATE RP_SVC_CATEGORY_MASTER SET IS_ACTIVE = :p_active, UPDATED_BY = :p_by, UPDATED_AT = SYSTIMESTAMP WHERE CATEGORY_CODE = :p_code", cn)
        { BindByName = true };
        cmd.Parameters.Add("p_active", isActive ? 1 : 0);
        cmd.Parameters.Add("p_by", actor);
        cmd.Parameters.Add("p_code", categoryCode);
        await cn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<long>> GetCategoryVerticalIdsAsync(string categoryCode)
    {
        var result = new List<long>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "SELECT VERTICAL_ID FROM RP_SVC_CATEGORY_VERTICAL_MAP WHERE CATEGORY_CODE = :p_code", cn) { BindByName = true };
        cmd.Parameters.Add("p_code", categoryCode);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(Convert.ToInt64(r.GetDecimal(0)));
        return result;
    }

    public async Task SetCategoryVerticalMapAsync(string categoryCode, List<long> verticalIds, string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using (var del = new OracleCommand("DELETE FROM RP_SVC_CATEGORY_VERTICAL_MAP WHERE CATEGORY_CODE = :p_code", cn) { BindByName = true })
        {
            del.Parameters.Add("p_code", categoryCode);
            await del.ExecuteNonQueryAsync();
        }
        foreach (var vid in verticalIds.Distinct())
        {
            await using var ins = new OracleCommand(
                "INSERT INTO RP_SVC_CATEGORY_VERTICAL_MAP (CATEGORY_CODE, VERTICAL_ID) VALUES (:p_code, :p_vid)", cn) { BindByName = true };
            ins.Parameters.Add("p_code", categoryCode);
            ins.Parameters.Add("p_vid", vid);
            await ins.ExecuteNonQueryAsync();
        }
        await using var upd = new OracleCommand(
            "UPDATE RP_SVC_CATEGORY_MASTER SET UPDATED_BY = :p_by, UPDATED_AT = SYSTIMESTAMP WHERE CATEGORY_CODE = :p_code2", cn) { BindByName = true };
        upd.Parameters.Add("p_by", actor);
        upd.Parameters.Add("p_code2", categoryCode);
        await upd.ExecuteNonQueryAsync();
    }

    public async Task<List<SvcCategory>> GetCategoriesForVerticalAsync(string groupType, long? verticalId)
    {
        var all = await GetCategoriesAsync(groupType);
        if (verticalId is null) return all;

        var result = new List<SvcCategory>();
        await using var cn = Conn();
        await cn.OpenAsync();
        foreach (var cat in all)
        {
            await using var cmd = new OracleCommand(
                "SELECT COUNT(*) FROM RP_SVC_CATEGORY_VERTICAL_MAP WHERE CATEGORY_CODE = :p_code", cn) { BindByName = true };
            cmd.Parameters.Add("p_code", cat.CategoryCode);
            var mappedCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            if (mappedCount == 0) { result.Add(cat); continue; } // unmapped = applies to ALL verticals

            await using var cmd2 = new OracleCommand(
                "SELECT COUNT(*) FROM RP_SVC_CATEGORY_VERTICAL_MAP WHERE CATEGORY_CODE = :p_code AND VERTICAL_ID = :p_vid", cn) { BindByName = true };
            cmd2.Parameters.Add("p_code", cat.CategoryCode);
            cmd2.Parameters.Add("p_vid", verticalId.Value);
            if (Convert.ToInt32(await cmd2.ExecuteScalarAsync()) > 0) result.Add(cat);
        }
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // RP_SVC_CATEGORY_ADMIN allow-list — mirrors RP_SJV_ADMIN / RP_STRIKE_ADMIN exactly.
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<bool> IsSvcCategoryAdminAsync(ClaimsPrincipal user)
    {
        if ((await _authz.AuthorizeAsync(user, "AppAdmin")).Succeeded) return true;

        var emp = user.FindFirst("emplid")?.Value;
        if (string.IsNullOrWhiteSpace(emp)) return false;
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand(
            "SELECT COUNT(*) FROM RP_SVC_CATEGORY_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_e) AND IS_ACTIVE=1", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", emp);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<List<SvcCategoryAdminRow>> ListSvcCategoryAdminsAsync()
    {
        var result = new List<SvcCategoryAdminRow>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "SELECT EMP_CODE, NVL(ADDED_BY,''), ADDED_AT FROM RP_SVC_CATEGORY_ADMIN WHERE IS_ACTIVE=1 ORDER BY EMP_CODE", cn);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            result.Add(new SvcCategoryAdminRow { EmpCode = r.GetString(0), AddedBy = r.GetString(1), AddedAt = r.GetDateTime(2) });
        return result;
    }

    public async Task AddSvcCategoryAdminAsync(string empCode, string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand(@"
            MERGE INTO RP_SVC_CATEGORY_ADMIN t USING (SELECT :p_e AS E FROM DUAL) s ON (UPPER(t.EMP_CODE)=UPPER(s.E))
            WHEN MATCHED THEN UPDATE SET IS_ACTIVE = 1, ADDED_BY = :p_by, ADDED_AT = SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (EMP_CODE, IS_ACTIVE, ADDED_BY, ADDED_AT) VALUES (:p_e2, 1, :p_by2, SYSTIMESTAMP)", cn)
        { BindByName = true };
        cmd.Parameters.Add("p_e", empCode);
        cmd.Parameters.Add("p_by", actor);
        cmd.Parameters.Add("p_e2", empCode);
        cmd.Parameters.Add("p_by2", actor);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveSvcCategoryAdminAsync(string empCode)
    {
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "UPDATE RP_SVC_CATEGORY_ADMIN SET IS_ACTIVE=0 WHERE UPPER(EMP_CODE)=UPPER(:p_e)", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", empCode);
        await cn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    // NOTE: verified live against ORGANISATION.STAFF_DETAILS in the dev Oracle container
    // (ALL_TAB_COLUMNS, OWNER='ORGANISATION') on 2026-09-24. All columns below exist as named.
    // Uses SCALE_DESC (the actual "Scale" column) rather than JOB_DESC, which is the job/
    // designation description, not scale.
    public async Task<SvcStaffLookupRow?> GetStaffByPfAsync(string pfNo)
    {
        await using var cn = SqlConn();
        await using var cmd = new SqlCommand(@"
        SELECT EMPLID,
               NAME,
               DEPTID,
               DESCR1,
               REGION_CODE,
               REGION_NAME,
               DIVISION_CODE,
               DIVISION_NAME,
               EMP_SCALE_DESCR,
               PHONE,
               EMAIL
        FROM StaffDetails
        WHERE EMPLID = @p_pf", cn);
        cmd.Parameters.AddWithValue("@p_pf", pfNo.Trim());
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync())
            return null;

        string? Str(int i) => r.IsDBNull(i) ? null : r.GetString(i);
        return new SvcStaffLookupRow
        {
            PfNo = Str(0) ?? "",            // EMPLID
            Name = Str(1),                  // NAME
            DeptDesc = Str(3),              // DESCR1
            RegionName = Str(5),            // REGION_NAME
            ZoneName = Str(7),              // DIVISION_NAME
            ScaleDesc = Str(8)              // EMP_SCALE_DESCR
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Vertical-scoped Category Admins (RP_SVC_CATEGORY_ADMIN_VERTICAL)
    // ══════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<long>> GetAdminVerticalIdsAsync(string empCode)
    {
        var result = new List<long>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "SELECT VERTICAL_ID FROM RP_SVC_CATEGORY_ADMIN_VERTICAL WHERE UPPER(EMP_CODE) = UPPER(:p_e)", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", empCode);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(Convert.ToInt64(r.GetDecimal(0)));
        return result;
    }

    public async Task SetAdminVerticalIdsAsync(string empCode, List<long> verticalIds, string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using (var del = new OracleCommand("DELETE FROM RP_SVC_CATEGORY_ADMIN_VERTICAL WHERE UPPER(EMP_CODE) = UPPER(:p_e)", cn) { BindByName = true })
        {
            del.Parameters.Add("p_e", empCode);
            await del.ExecuteNonQueryAsync();
        }
        foreach (var vid in verticalIds.Distinct())
        {
            await using var ins = new OracleCommand(
                "INSERT INTO RP_SVC_CATEGORY_ADMIN_VERTICAL (EMP_CODE, VERTICAL_ID) VALUES (:p_e, :p_vid)", cn) { BindByName = true };
            ins.Parameters.Add("p_e", empCode);
            ins.Parameters.Add("p_vid", vid);
            await ins.ExecuteNonQueryAsync();
        }
        _logger.LogInformation("SVC: admin {Emp} verticals set to [{Verts}] by {Actor}", empCode, string.Join(",", verticalIds), actor);
    }

    public async Task<bool> CanManageCategoryAsync(ClaimsPrincipal user, string categoryCode)
    {
        if ((await _authz.AuthorizeAsync(user, "AppAdmin")).Succeeded) return true; // Super admin — all rights, sees all.

        var emp = user.FindFirst("emplid")?.Value;
        if (string.IsNullOrWhiteSpace(emp)) return false;
        if (!await IsSvcCategoryAdminAsync(user)) return false; // must be an active category admin at all

        var adminVerticals = await GetAdminVerticalIdsAsync(emp);
        if (adminVerticals.Count == 0) return true; // flat/legacy admin (no vertical scoping assigned) — all categories

        var categoryVerticals = await GetCategoryVerticalIdsAsync(categoryCode);
        if (categoryVerticals.Count == 0) return true; // category unmapped = applies to ALL verticals, reachable by every vertical-admin

        return categoryVerticals.Intersect(adminVerticals).Any();
    }


    /// <summary>Looks a branch up in the Branch Diary DB (SQL Server, <c>SQLServerConnection</c> —
    /// same connection the Reports feature uses against dbo.Branch_Master) by its SOL ID. This is
    /// the single place the SOL-ID → branch/region/zone resolution lives; both VendorForm.razor
    /// (single-record add/edit) and the bulk-upload validator call it so there's exactly one query
    /// and one connection for this. Returns null when the SOL ID isn't found.
    ///
    /// Column mapping (verified against the Reports services' live Branch_Master queries —
    /// SundryReportService/ChequeIssuedReportService):
    ///   SOL_ID        → BranchSolId   (the branch's SOL ID; what we now key on)
    ///   BRANCH_CODE   → BranchCode
    ///   BRANCH_NAME   → BranchName
    ///   REGION_SOLID  → RegionSolId
    ///   REGION_CODE   → RegionCode
    ///   REGION_NAME   → RegionName
    ///   ZONE_SOLID    → ZoneSolId
    ///   ZONE_CODE     → ZoneCode
    ///   ZONE_NAME_ENG → ZoneName</summary>
    public async Task<SvcBranchInfo?> GetBranchBySolIdAsync(string solId)
    {
        await using var cn = BranchDiarySqlConn();

        await using var cmd = new SqlCommand(@"
        SELECT
            SOL_ID,
            BRANCH_CODE,
            BRANCH_NAME,
            REGION_SOLID,
            REGION_CODE,
            REGION_NAME,
            ZONE_SOLID,
            ZONE_CODE,
            ZONE_NAME_ENG
        FROM Branch_Master
        WHERE SOL_ID = @p_solId", cn);

        cmd.Parameters.AddWithValue("@p_solId", solId);

        await cn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync())
            return null;

        string Str(int i) => r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? "";

        return new SvcBranchInfo
        {
            BranchSolId = Str(0),
            BranchCode = Str(1),
            BranchName = Str(2),
            RegionSolId = Str(3),
            RegionCode = Str(4),
            RegionName = Str(5),
            ZoneSolId = Str(6),
            ZoneCode = Str(7),
            ZoneName = Str(8)
        };
    }

    /// <summary>The FIELD_KEYs the branch auto-population recognises. The SOL-ID input field
    /// itself (DROPDOWN_SOURCE='BRANCH') is deliberately NOT in this set — it holds the value the
    /// user typed and must never be overwritten by the auto-fill. Kept as one list so
    /// ApplyBranchToReadonlyFields and ClearBranchReadonlyFields can't drift apart.</summary>
    private static readonly string[] BranchAutoFillKeys =
    {
        "BRANCH_NAME", "BRANCH_CODE", "BRANCH_SOL_ID",
        "REGION_CODE", "REGION_NAME", "REGION_SOL_ID",
        "ZONE_CODE", "ZONE_NAME", "ZONE_SOL_ID",
        "ZONE_CODE_NAME", "REGION_CODE_NAME"
    };

    /// <summary>Finds the branch SOL-ID input field's FIELD_KEY for a category — the (editable)
    /// field with DROPDOWN_SOURCE='BRANCH'. This is the value the user types (a branch SOL ID) and
    /// the key the form/bulk-upload read it under. Returns 'BRANCH_SOL_ID' as the default when a
    /// category defines no such field, so callers always have a stable key to look up. Handles the
    /// old 'BRANCH_CODE' key transparently since it's matched by DROPDOWN_SOURCE, not by name.</summary>
    public string GetBranchSolIdFieldKey(IEnumerable<SvcFieldDefinition> fields) =>
        fields.FirstOrDefault(f => f.DropdownSource == "BRANCH" && !f.IsReadonly)?.FieldKey ?? "BRANCH_SOL_ID";

    /// <summary>Writes a resolved branch's Branch/Region/Zone details into the readonly fields of
    /// a category's value dictionary. This is the single source of truth for the SOL-ID →
    /// auto-populated-fields mapping, shared by VendorForm.razor (single add/edit) and
    /// ValidateVendorUploadAsync (bulk upload) so the two can never drift apart. Only IS_READONLY
    /// fields are touched (the SOL-ID input field itself and any editable fields are left alone),
    /// and only the branch-related keys below are recognised — any other readonly field a category
    /// defines is left untouched. Pure in-memory mapping, no DB access.</summary>
    public void ApplyBranchToReadonlyFields(
        SvcBranchInfo branch, IEnumerable<SvcFieldDefinition> fields, Dictionary<string, string?> values)
    {
        foreach (var f in fields.Where(f => f.IsReadonly))
        {
            switch (f.FieldKey)
            {
                case "BRANCH_NAME": values[f.FieldKey] = branch.BranchName; break;
                case "BRANCH_CODE": values[f.FieldKey] = branch.BranchCode; break;
                case "BRANCH_SOL_ID": values[f.FieldKey] = branch.BranchSolId; break;
                case "REGION_CODE": values[f.FieldKey] = branch.RegionCode; break;
                case "REGION_NAME": values[f.FieldKey] = branch.RegionName; break;
                case "REGION_SOL_ID": values[f.FieldKey] = branch.RegionSolId; break;
                case "ZONE_CODE": values[f.FieldKey] = branch.ZoneCode; break;
                case "ZONE_NAME": values[f.FieldKey] = branch.ZoneName; break;
                case "ZONE_SOL_ID": values[f.FieldKey] = branch.ZoneSolId; break;
                case "ZONE_CODE_NAME": values[f.FieldKey] = $"{branch.ZoneCode} - {branch.ZoneName}"; break;
                case "REGION_CODE_NAME": values[f.FieldKey] = $"{branch.RegionCode} - {branch.RegionName}"; break;
            }
        }
    }

    /// <summary>Clears the readonly Branch/Region/Zone fields (used by VendorForm.razor when the
    /// SOL ID is emptied or invalid). Shares the same key list as ApplyBranchToReadonlyFields.</summary>
    public void ClearBranchReadonlyFields(IEnumerable<SvcFieldDefinition> fields, Dictionary<string, string?> values)
    {
        foreach (var f in fields.Where(f => f.IsReadonly && BranchAutoFillKeys.Contains(f.FieldKey)))
            values[f.FieldKey] = "";
    }
}
