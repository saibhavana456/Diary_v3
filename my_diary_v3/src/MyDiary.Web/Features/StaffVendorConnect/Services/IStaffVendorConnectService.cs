using System.Security.Claims;
using MyDiary.Web.Features.StaffVendorConnect.Models.DTO;

namespace MyDiary.Web.Features.StaffVendorConnect.Services;

public interface IStaffVendorConnectService
{
    // ── Categories & field definitions (the form-builder / field-mapping table) ──
    Task<List<SvcCategory>> GetCategoriesAsync(string? groupType = null, bool activeOnly = true);
    Task<SvcCategory?> GetCategoryAsync(string categoryCode);
    Task UpsertCategoryAsync(SvcCategory category, string actor);

    Task<List<SvcFieldDefinition>> GetFieldDefinitionsAsync(string categoryCode, bool activeOnly = true);
    Task<SvcFieldDefinition?> GetFieldDefinitionAsync(long fieldId);
    Task<long> UpsertFieldDefinitionAsync(SvcFieldDefinition field, string actor);
    Task SetFieldActiveAsync(long fieldId, bool isActive, string actor);

    // ── Vendor records (EAV) ──
    Task<List<SvcVendorRecord>> GetVendorRecordsAsync(string? categoryCode, string? zoneCode, string? regionCode, string? branchCode, string status = "ACTIVE");

    /// <summary>Fetches one record's full field values. When <paramref name="canSeeUnmasked"/> is
    /// false (the caller has already checked CanManageCategoryAsync / App Admin), every value
    /// whose field definition is IS_PII gets masked via PiiMasker before the record leaves the
    /// service — masking happens here, not in the page, since Blazor Server would otherwise ship
    /// the real value to the browser regardless of how the UI renders it.</summary>
    Task<SvcVendorRecord?> GetVendorRecordDetailAsync(long recordId, bool canSeeUnmasked = true);

    /// <summary>Validates against each field's definition (mandatory/regex/max-length), then
    /// inserts (recordId null) or updates (recordId set) the header + EAV values, diff-logging
    /// every changed field to the audit table. Returns the saved record's id.</summary>
    Task<(bool Success, long RecordId, List<string> Errors)> SaveVendorRecordAsync(
        long? recordId, string categoryCode, string branchCode, string branchName,
        string regionCode, string regionName, string zoneCode, string zoneName,
        Dictionary<string, string?> fieldValues, string actor);

    Task SetVendorRecordStatusAsync(long recordId, string status, string actor);

    SvcFieldValidationResult ValidateFieldValues(List<SvcFieldDefinition> fields, Dictionary<string, string?> values);

    // ── Audit log ──
    Task<List<SvcAuditEntry>> GetAuditLogAsync(string? categoryCode, DateTime? fromDate, DateTime? toDate, int maxRows = 200);

    // ── Staff directory ──
    /// <summary>When <paramref name="canSeeUnmasked"/> is false, MOBILE_NUMBER and EMAIL_ADDRESS
    /// are masked via PiiMasker before the rows leave the service (Staff Directory has fixed
    /// columns, not dynamic IS_PII-flagged fields, so these two are masked directly in code).</summary>
    Task<List<SvcStaffRow>> GetStaffAsync(string? categoryCode, string? zoneCode, string? regionCode, string? branchCode, string? specialistType, string? scaleDesc, bool canSeeUnmasked = true);
    Task<(List<string> Zones, List<string> Regions, List<string> Branches, List<string> Specializations, List<string> Scales)> GetStaffFilterOptionsAsync(string? categoryCode);

    /// <summary>Distinct Category values (StaffDetails.UBI_SPCL_OFFICER) for active staff, used to
    /// populate the Staff Directory's Category filter dynamically from the same data the grid shows
    /// (replaces the old static RP_SVC_CATEGORY_MASTER list for this page).</summary>
    Task<List<string>> GetStaffCategoriesAsync();

    // ── Dropdown source helpers ──
    Task<List<(string Code, string Name)>> GetBranchListAsync();
    Task<List<SvcBranchInfo>> GetBranchDetailsAsync();

    // ── Report ──
    Task<(List<SvcFieldDefinition> Columns, List<SvcVendorRecord> Rows)> GetVendorReportAsync(string categoryCode, string? zoneCode, string? regionCode, string? branchCode);
    Task<byte[]> ExportVendorReportToExcelAsync(string categoryCode, string? zoneCode, string? regionCode, string? branchCode);

    // ── Bulk Excel upload (Add New → Download Template / Upload) ──
    /// <summary>Builds the fill-in-and-upload template for a category: a Data sheet with one
    /// column per non-readonly active field (header = FIELD_KEY, for reliable round-tripping) and
    /// an Instructions sheet describing each column (label, type, mandatory/unique, validation
    /// hint, dropdown options).</summary>
    Task<byte[]> GenerateVendorTemplateAsync(string categoryCode);

    /// <summary>Parses the uploaded workbook's Data sheet and validates every row: mandatory/
    /// regex (via ValidateFieldValues), a branch SOL-ID lookup against the Branch Diary DB to
    /// auto-fill any readonly Branch/Region/Zone fields the category defines, and IS_UNIQUE checks
    /// both within the file itself and against existing ACTIVE DB records. Nothing is written to
    /// the database by this call — it's the "check validations and show errors, else preview" step.</summary>
    Task<SvcBulkUploadResult> ValidateVendorUploadAsync(string categoryCode, Stream fileStream, string actor);

    /// <summary>Inserts every row in <paramref name="rows"/> as a new ACTIVE vendor record (each
    /// row is expected to already be IsValid — re-validates defensively and skips/reports any
    /// that aren't, e.g. if the DB changed between preview and submit). Returns the same
    /// SvcBulkUploadResult shape so the caller can render a final outcome/exception report
    /// identically to the pre-submit preview.</summary>
    Task<SvcBulkUploadResult> SubmitVendorUploadAsync(string categoryCode, List<SvcBulkUploadRow> rows, string actor);

    /// <summary>Renders a validation/submit outcome back to Excel — original row data plus an
    /// "Errors" column — so a user can fix the flagged rows and re-upload rather than tracking
    /// mistakes by eye across a long preview grid ("exception report").</summary>
    Task<byte[]> GenerateExceptionReportAsync(string categoryCode, SvcBulkUploadResult result);

    // ── Masters ("create masters ... modify masters ... download/upload master in the same page") ──
    Task<List<SvcMasterType>> GetMasterTypesAsync();
    Task UpsertMasterTypeAsync(SvcMasterType type, string actor);
    Task<List<SvcMasterValue>> GetMasterValuesAsync(string masterTypeCode);
    Task<long> UpsertMasterValueAsync(SvcMasterValue value, string actor);
    Task SetMasterValueActiveAsync(long masterId, bool isActive, string actor);
    Task<byte[]> ExportMasterValuesAsync(string masterTypeCode);
    /// <summary>Parses an uploaded master-values Excel (columns: Value Label, Parent Label,
    /// Display Order) and adds/updates rows for masterTypeCode — validates duplicate labels
    /// within the file and, for a hierarchical type, that every Parent Label already exists in
    /// the parent type.</summary>
    Task<SvcBulkUploadResult> ImportMasterValuesAsync(string masterTypeCode, Stream fileStream, string actor);

    // ── Verticals (reuses RequestPortal's RP_M_VERTICAL — same Oracle schema) ──
    Task<List<SvcVertical>> GetActiveVerticalsAsync();

    // ── Vendor Category admin block (rendered on Masters.razor as a pseudo master-type) ──
    Task SetCategoryActiveAsync(string categoryCode, bool isActive, string actor);
    Task<List<long>> GetCategoryVerticalIdsAsync(string categoryCode);
    Task SetCategoryVerticalMapAsync(string categoryCode, List<long> verticalIds, string actor);
    /// <summary>Categories visible for verticalId — either unmapped (applies to ALL) or explicitly
    /// mapped to verticalId. verticalId == null returns every active category (current behaviour).</summary>
    Task<List<SvcCategory>> GetCategoriesForVerticalAsync(string groupType, long? verticalId);

    // ── RP_SVC_CATEGORY_ADMIN allow-list (App Admin, or active member here, may manage the
    //    "Vendor Category" pseudo master type on Masters.razor) ──
    Task<bool> IsSvcCategoryAdminAsync(ClaimsPrincipal user);
    Task<List<SvcCategoryAdminRow>> ListSvcCategoryAdminsAsync();
    Task AddSvcCategoryAdminAsync(string empCode, string actor);
    Task RemoveSvcCategoryAdminAsync(string empCode);

    /// <summary>PF-number staff lookup (ORGANISATION.STAFF_DETAILS) used by the Category Admins
    /// panel's "Search" step before "Add as Admin".</summary>
    Task<SvcStaffLookupRow?> GetStaffByPfAsync(string pfNo);

    // ── Vertical-scoped Category Admins (RP_SVC_CATEGORY_ADMIN_VERTICAL) ──
    /// <summary>Vertical ids empCode is scoped to. Empty list = flat/legacy admin (all categories).</summary>
    Task<List<long>> GetAdminVerticalIdsAsync(string empCode);
    Task SetAdminVerticalIdsAsync(string empCode, List<long> verticalIds, string actor);

    /// <summary>True for App Admin (always, "sees/manages all"), or for an active
    /// RP_SVC_CATEGORY_ADMIN whose assigned verticals (RP_SVC_CATEGORY_ADMIN_VERTICAL) reach
    /// categoryCode — either categoryCode is unmapped in RP_SVC_CATEGORY_VERTICAL_MAP (applies to
    /// ALL verticals) or shares a vertical with the admin. An admin with no vertical rows assigned
    /// is flat/legacy and can manage every category. Used to gate Form Builder edits/toggles and
    /// the Vendor Record List's "Send Communication" button.</summary>
    Task<bool> CanManageCategoryAsync(ClaimsPrincipal user, string categoryCode);

    /// <summary>Resolves a branch from the Branch Diary DB by its SOL ID (SQL Server,
    /// SQLServerConnection). Single source for SOL-ID → branch/region/zone lookup, shared by the
    /// vendor form and the bulk-upload validator. Returns null when the SOL ID isn't found.</summary>
    Task<SvcBranchInfo?> GetBranchBySolIdAsync(string solId);

    /// <summary>Writes a resolved branch's details into a category's readonly Branch/Region/Zone
    /// fields (the single mapping shared by the form and the bulk upload, so they can't drift).</summary>
    void ApplyBranchToReadonlyFields(SvcBranchInfo branch, IEnumerable<SvcFieldDefinition> fields, Dictionary<string, string?> values);

    /// <summary>Clears the readonly Branch/Region/Zone fields (SOL ID emptied or invalid).</summary>
    void ClearBranchReadonlyFields(IEnumerable<SvcFieldDefinition> fields, Dictionary<string, string?> values);

    /// <summary>The FIELD_KEY of a category's branch SOL-ID input field (the editable field with
    /// DROPDOWN_SOURCE='BRANCH'), defaulting to 'BRANCH_SOL_ID'. Lets the form and bulk upload find
    /// the SOL-ID column without hard-coding a key, so a category can use 'BRANCH_SOL_ID' (new) or
    /// the legacy 'BRANCH_CODE' interchangeably.</summary>
    string GetBranchSolIdFieldKey(IEnumerable<SvcFieldDefinition> fields);
}
