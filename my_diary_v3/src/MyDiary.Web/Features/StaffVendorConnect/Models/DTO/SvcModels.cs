namespace MyDiary.Web.Features.StaffVendorConnect.Models.DTO;

public class SvcCategory
{
    public string CategoryCode { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string GroupType { get; set; } = "";   // EXTERNAL | INTERNAL
    public string? ParentGroup { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public class SvcFieldDefinition
{
    public long FieldId { get; set; }
    public string CategoryCode { get; set; } = "";
    public string FieldKey { get; set; } = "";
    public string FieldLabel { get; set; } = "";
    public string? SectionName { get; set; }
    public string FieldType { get; set; } = "TEXT";   // TEXT | TEXTAREA | NUMERIC | DATE | DROPDOWN
    public string? ValidationRegex { get; set; }
    public string? ValidationHint { get; set; }
    public int? MaxLength { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsReadonly { get; set; }
    public bool IsUnique { get; set; }   // value must not repeat among a category's ACTIVE records
    public bool IsPii { get; set; }   // masked in every read-only/list rendering unless the viewer can manage this category (or is App Admin)
    public string? DropdownSource { get; set; }   // STATIC | BRANCH | CITY | DISTRICT | STATE
    public List<string> DropdownOptions { get; set; } = new();
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }

    // Audit — CREATED_BY/CREATED_DATE (set once, at insert) and UPDATED_BY/UPDATED_DATE (set on
    // every save) columns already present on RP_SVC_FIELD_DEFINITION; surfaced in Form Builder.
    public string? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Populated only for DropdownSource == "MASTER" fields whose master type has a parent type
    // (e.g. INDIAN_CITY's parent is INDIAN_DISTRICT) — lets VendorForm.razor filter this field's
    // options down to only those under whatever value is currently selected in
    // MasterParentFieldKey, instead of showing every value regardless of the parent selection.
    public string? MasterParentFieldKey { get; set; }
    public List<(string Label, string? ParentLabel)> MasterValueParents { get; set; } = new();
}

public class SvcVendorRecord
{
    public long RecordId { get; set; }
    public string CategoryCode { get; set; } = "";
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public string? ZoneCode { get; set; }
    public string? ZoneName { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }

    /// <summary>Field values keyed by FIELD_KEY — populated when the record is fetched for
    /// display/edit; empty for the plain list view.</summary>
    public Dictionary<string, string?> Values { get; set; } = new();

    /// <summary>Convenience accessor used by report/list views for a couple of always-present
    /// "headline" columns without needing the caller to know the category's field keys.</summary>
    public string? DisplayTitle { get; set; }
}

public class SvcAuditEntry
{
    public long AuditId { get; set; }
    public long RecordId { get; set; }
    public string CategoryCode { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string? FieldKey { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangedBy { get; set; } = "";
    public DateTime ChangedAt { get; set; }
}

public class SvcStaffRow
{
    public string EmpId { get; set; } = "";
    public string EmpName { get; set; } = "";
    public string StaffCategoryCode { get; set; } = "";
    public string? SpecialistType { get; set; }
    public string? DeptDesc { get; set; }
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string? BranchSolId { get; set; }
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public string? ZoneCode { get; set; }
    public string? ZoneName { get; set; }
    public string? DivisionCode { get; set; }
    public string? DivisionName { get; set; }
    public string? ScaleDesc { get; set; }
    public string? DesignationDesc { get; set; }
    public string? Gender { get; set; }
    public string? MobileNumber { get; set; }
    public string? EmailAddress { get; set; }
    public DateTime? JoiningDate { get; set; }
    public DateTime? CurrentPostingDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public DateTime DataExtractionDate { get; set; }
}

public class SvcCommLogEntry
{
    public long CommId { get; set; }
    public string TargetModule { get; set; } = "";
    public string Channel { get; set; } = "";
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
    public string? TemplateName { get; set; }
    public string RecipientScope { get; set; } = "";
    public int RecipientCount { get; set; }
    public string SentBy { get; set; } = "";
    public DateTime SentAt { get; set; }
}

public class SvcCommRecipientResult
{
    public string RecipientId { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string RecipientAddress { get; set; } = "";
    public string SendStatus { get; set; } = "";
    public string? ErrorMessage { get; set; }
}

public class SvcSendCommunicationRequest
{
    public string TargetModule { get; set; } = "";   // VENDOR | STAFF
    public string Channel { get; set; } = "";         // EMAIL | SMS | WHATSAPP
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
    public string? TemplateName { get; set; }
    public bool SendToAll { get; set; }
    public List<string> SelectedRecipientIds { get; set; } = new();
    // Email + WhatsApp only — SMS gateways are template-text-only, no attachment support.
    public List<SvcAttachmentFile> Attachments { get; set; } = new();

    // WhatsApp only — Karix ConnectOne RCM API needs a pre-approved templateId, a language code,
    // and positional parameter values (index 0 fills the template's {{1}}, etc.), not free text.
    public string? WhatsAppTemplateId { get; set; }
    public string WhatsAppLanguage { get; set; } = "en";
    public List<string> WhatsAppParameterValues { get; set; } = new();
}

public class SvcAttachmentFile
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class SvcCommAttachmentInfo
{
    public long Id { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
}

public class SvcBranchInfo
{
    public string BranchCode { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string RegionCode { get; set; } = "";
    public string RegionName { get; set; } = "";
    public string ZoneCode { get; set; } = "";
    public string ZoneName { get; set; } = "";
    public string BranchSolId { get; set; } = "";
    public string RegionSolId { get; set; } = "";
    public string ZoneSolId { get; set; } = "";
}

/// <summary>One data row from an uploaded bulk-vendor Excel, after validation (mandatory/regex/
/// duplicate checks — both within the file and against existing DB records). RowNumber is the
/// actual Excel row (so an error message can say "row 7", matching what the user sees when they
/// open the file), Values is keyed by FIELD_KEY, and Errors is empty for a row that's clear to
/// submit.</summary>
public class SvcBulkUploadRow
{
    public int RowNumber { get; set; }
    public Dictionary<string, string?> Values { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool IsValid => Errors.Count == 0;
}

public class SvcBulkUploadResult
{
    public List<SvcBulkUploadRow> Rows { get; set; } = new();
    public List<string> FileErrors { get; set; } = new();   // structural problems (wrong sheet, no header, ...) that block everything
    public int ValidCount => Rows.Count(r => r.IsValid);
    public int ErrorCount => Rows.Count(r => !r.IsValid);
}

// ── Masters (generic dropdown-value system backing DROPDOWN_SOURCE='MASTER' fields) ──

public class SvcMasterType
{
    public string MasterTypeCode { get; set; } = "";
    public string MasterTypeName { get; set; } = "";
    public string? ParentTypeCode { get; set; }   // e.g. INDIAN_CITY's parent is INDIAN_DISTRICT
    public bool IsActive { get; set; } = true;
}

public class SvcMasterValue
{
    public long MasterId { get; set; }
    public string MasterTypeCode { get; set; } = "";
    public string ValueLabel { get; set; } = "";
    public long? ParentValueId { get; set; }
    public string? ParentValueLabel { get; set; }   // resolved for display only
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SvcFieldValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

// ── Vertical filter (reuses RequestPortal's RP_M_VERTICAL) ──

public class SvcVertical
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

// ── Category ↔ Vertical mapping (empty mapping = category applies to ALL verticals) ──

public class SvcCategoryVerticalMap
{
    public string CategoryCode { get; set; } = "";
    public List<long> VerticalIds { get; set; } = new();
}

// ── RP_SVC_CATEGORY_ADMIN allow-list (mirrors RP_SJV_ADMIN / RP_STRIKE_ADMIN) ──

public class SvcCategoryAdminRow
{
    public string EmpCode { get; set; } = "";
    public string AddedBy { get; set; } = "";
    public DateTime AddedAt { get; set; }
}

/// <summary>Vertical(s) an RP_SVC_CATEGORY_ADMIN row is scoped to (RP_SVC_CATEGORY_ADMIN_VERTICAL).
/// No rows for an admin = flat/legacy admin with rights over every category (see
/// StaffVendorConnectService.CanManageCategoryAsync).</summary>

/// <summary>Result of a PF-number staff lookup used by the Category Admins panel (Add as Admin).</summary>
public class SvcStaffLookupRow
{
    public string PfNo { get; set; } = "";
    public string? Name { get; set; }
    public string? ScaleDesc { get; set; }        // JOB_DESC
    public string? DeptDesc { get; set; }
    public string? RegionName { get; set; }
    public string? ZoneName { get; set; }
}
