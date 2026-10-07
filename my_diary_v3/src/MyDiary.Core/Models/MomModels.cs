namespace RequestPortal.Core.Models;

/// <summary>
/// A "MoM Type" authored by a Super Admin in MoM Developer — the generalized
/// config row behind both BRD1 (ZLAC/RLAC Assurance Committee Minutes) and
/// BRD2 (IR/ER &amp; Welfare Association Meetings). Both BRDs are just two rows
/// here; the same set of togglable filters/upload rules drives both.
/// </summary>
public sealed class MomType
{
    public long Id { get; set; }
    public long VerticalId { get; set; }
    public string? VerticalName { get; set; }
    public string TypeName { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public string? Description { get; set; }

    public bool EnableZoneRegionScope { get; set; } = true;
    public bool EnableDateRange { get; set; } = true;
    public bool EnableMeetingType { get; set; }
    public bool EnableQuarter { get; set; }
    public bool EnableYear { get; set; } = true;
    public string? MeetingTypeLabel { get; set; }

    public int? MaxSupportingFiles { get; set; }
    public string AllowedFileTypes { get; set; } = "pdf";
    public string StorageRootPath { get; set; } = "";
    public bool RequirePrimaryFile { get; set; } = true;
    public string IconName { get; set; } = "Description";
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
    public string? CreatedByEmp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Form payload used by MoM Developer create/edit.</summary>
public sealed class MomTypeForm
{
    public long? Id { get; set; }
    public long? VerticalId { get; set; }
    public string TypeName { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public string? Description { get; set; }

    public bool EnableZoneRegionScope { get; set; } = true;
    public bool EnableDateRange { get; set; } = true;
    public bool EnableMeetingType { get; set; }
    public bool EnableQuarter { get; set; }
    public bool EnableYear { get; set; } = true;
    public string? MeetingTypeLabel { get; set; }

    public int? MaxSupportingFiles { get; set; }
    public string AllowedFileTypes { get; set; } = "pdf";
    public string StorageRootPath { get; set; } = "";
    public bool RequirePrimaryFile { get; set; } = true;
    public string IconName { get; set; } = "Description";
    public int SortOrder { get; set; }
}

/// <summary>A meeting category scoped to a MoM Type (e.g. Award Staff Union, Welfare Association - SC).</summary>
public sealed class MomMeetingType
{
    public long Id { get; set; }
    public long MomTypeId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool AppliesAtRo { get; set; } = true;
    public bool AppliesAtZo { get; set; } = true;
    public bool AppliesAtCo { get; set; } = true;
    public int? FrequencyPerQuarter { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Generalizes the CO-scope split (originally IR/ER's own ERD/WELFARE distinction, migration
    /// 007) to any MoM type built via the builder. Meeting types sharing the same non-null code form one CO
    /// scope group for this MoM type — assignable to a CO user via MomCoScopeAdmin. Null = this meeting type
    /// isn't part of any CO scope grouping (a scoped CO user never sees it; an unscoped CO user still does).</summary>
    public string? CoScopeCode { get; set; }

    /// <summary>When true, a CO user scoped to this meeting type's CoScopeCode never sees RO-level entries
    /// for it (only ZO + CO) — the generalized form of the Welfare Cell CO role's "no RO" rule. Meaningless
    /// unless CoScopeCode is set.</summary>
    public bool CoScopeHidesRoLevel { get; set; }
}

/// <summary>A trade union reference row scoped to a MoM Type (Category / Union Name / Affiliation).</summary>
public sealed class MomUnion
{
    public long Id { get; set; }
    public long MomTypeId { get; set; }
    public string Category { get; set; } = "";
    public string UnionName { get; set; } = "";
    public string Affiliation { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>An uploaded MoM record (RO/ZO/CO entry).</summary>
public sealed class MomEntry
{
    public long Id { get; set; }
    public long MomTypeId { get; set; }
    public string OfficeLevel { get; set; } = "";  // RO | ZO | CO
    public string? ZoneName { get; set; }
    public string? RegionName { get; set; }
    public string? BranchSolId { get; set; }
    public long? MeetingTypeId { get; set; }
    public string? MeetingTypeName { get; set; }
    public long? UnionId { get; set; }
    public string? UnionName { get; set; }
    public int MeetingYear { get; set; }
    public int? MeetingMonth { get; set; }
    public DateTime MeetingDate { get; set; }
    public int? Quarter { get; set; }
    public string? Remarks { get; set; }
    public string PrimaryFilePath { get; set; } = "";
    public string PrimaryFileName { get; set; } = "";
    public string CreatedByEmp { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Form payload used to create a MoM entry.</summary>
public sealed class MomEntryForm
{
    public long MomTypeId { get; set; }
    public string OfficeLevel { get; set; } = "";
    public string? ZoneName { get; set; }
    public string? RegionName { get; set; }
    public string? BranchSolId { get; set; }
    public long? MeetingTypeId { get; set; }
    public long? UnionId { get; set; }
    public int MeetingYear { get; set; }
    public int? MeetingMonth { get; set; }
    public DateTime MeetingDate { get; set; }
    public int? Quarter { get; set; }
    public string? Remarks { get; set; }
    public string PrimaryFilePath { get; set; } = "";
    public string PrimaryFileName { get; set; } = "";
}

/// <summary>
/// Item 6 admin UI: a row in RP_M_MOM_CO_SCOPE assigning one CO-level employee
/// into either the "ERD" (Award Staff Union + Officers Union) or "WELFARE"
/// (Welfare Association) scope for a given MoM type.
/// </summary>
public sealed class MomCoScope
{
    public long Id { get; set; }
    public long MomTypeId { get; set; }
    public string EmpCode { get; set; } = "";
    public string? EmpName { get; set; }
    public string ScopeCode { get; set; } = "";  // ERD | WELFARE
    public bool IsActive { get; set; } = true;
    public string AssignedByEmp { get; set; } = "";
    public DateTime AssignedAt { get; set; }
}

/// <summary>A supporting document attached to an <see cref="MomEntry"/>.</summary>
public sealed class MomAttachment
{
    public long Id { get; set; }
    public long EntryId { get; set; }
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public string UploadedByEmp { get; set; } = "";
}
