namespace MyDiary.Web.Features.StrikeCorner.Models;

/// <summary>One FAQ or SOP document in the Strike Corner document library. The file itself is a BLOB on the
/// row (FILE_BLOB is not selected by list queries -- only fetched by the download/view endpoint).</summary>
public sealed class StrikeFaqDoc
{
    public long Id { get; set; }
    public string Category { get; set; } = "FAQ"; // FAQ | SOP
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long FileSize { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? EntryBy { get; set; }
    public DateTime? EntryAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
