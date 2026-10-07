namespace MyDiary.Web.Features.UnionHubVideoLaunch.Models.DTO;

public sealed class UhvVertical
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class UhvAttachment
{
    public long Id { get; set; }
    public long LaunchId { get; set; }
    public string Kind { get; set; } = ""; // IMAGE | INFOGRAPHIC | PPT
    public string FileName { get; set; } = "";
    public string? Mime { get; set; }
}

public sealed class UhvComment
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public string EmpName { get; set; } = "";
    public string CommentText { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>One card in the feed / one row in the admin list.</summary>
public sealed class UhvLaunch
{
    public long Id { get; set; }
    public long VerticalId { get; set; }
    public string VerticalName { get; set; } = "";
    public string ProductName { get; set; } = "";
    public DateTime LaunchDate { get; set; }
    public string? Description { get; set; }
    public bool HasVideo { get; set; }
    public string? VideoFileName { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string CreatedByEmp { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public int LikeCount { get; set; }
    public bool LikedByMe { get; set; }
    public List<UhvAttachment> Attachments { get; set; } = new();
    public List<UhvComment> Comments { get; set; } = new();

    // UI-only transient state for the feed page (not persisted) — mirrors FeedPost's
    // ShowComments/Draft convention in RequestPortal/Pages/UnionHub/UnionHub.razor.
    public bool ShowComments { get; set; }
    public string? Draft { get; set; }
}

/// <summary>A file selected in the browser, buffered to bytes before the ADO insert
/// (mirrors the InputFile -&gt; MemoryStream pattern used by Features/MediationOfNation's
/// admin upload page).</summary>
public sealed class UhvUploadFile
{
    public string FileName { get; set; } = "";
    public string Mime { get; set; } = "";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public sealed class UhvCreateDraftRequest
{
    public long VerticalId { get; set; }
    public string ProductName { get; set; } = "";
    public DateTime LaunchDate { get; set; }
    public string? Description { get; set; }
    public UhvUploadFile? Video { get; set; }
    public List<(string Kind, UhvUploadFile File)> Attachments { get; set; } = new();
    public string CreatedByEmp { get; set; } = "";
}
