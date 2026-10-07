namespace MyDiary.Web.Features.Banner.Models;

public class BannerConfig
{
    public int BannerId { get; set; }
    public string ImageUrl { get; set; } = "";
    public string RedirectUrl { get; set; } = "";
    public string? Title { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedDate { get; set; }
}
