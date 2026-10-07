namespace MyDiary.Web.Features.Landing.Services;

/// <summary>
/// Represents a single carousel/banner image entry from the MD_LANDING_CAROUSEL table.
/// </summary>
public sealed class CarouselImageItem
{
    public long Id { get; set; }
    public string FileName { get; set; } = "";
    public string AltText { get; set; } = "";
    public int SortOrder { get; set; }
}

/// <summary>
/// Represents a Board of Directors member from the MD_LANDING_BOARD_MEMBERS table.
/// </summary>
public sealed class BoardMemberItem
{
    public long Id { get; set; }
    public string NameKey { get; set; } = "";
    public string RoleKey { get; set; } = "";
    public string ImageFileName { get; set; } = "";
    public string DialogImageFileName { get; set; } = "";
    public string DialogImageHindiFileName { get; set; } = "";
    public string SummaryI18nKey { get; set; } = "";
    public string BioI18nKey { get; set; } = "";
    public bool IsMD { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// Fetches landing page carousel images and board members from the database.
/// Images are served from NFS (when enabled) or wwwroot/images (development).
/// </summary>
public interface ILandingCarouselService
{
    /// <summary>Returns all active carousel/banner images ordered by SortOrder.</summary>
    Task<List<CarouselImageItem>> GetActiveImagesAsync();

    /// <summary>Returns all active board members ordered by SortOrder.</summary>
    Task<List<BoardMemberItem>> GetActiveBoardMembersAsync();
}
