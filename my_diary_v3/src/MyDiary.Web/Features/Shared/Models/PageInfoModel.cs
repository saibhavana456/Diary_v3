namespace MyDiary.Web.Features.Shared.Models;

public class PageInfoModel
{
    public string PageKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public string Category { get; set; } = "DASHBOARD";
    public string Status { get; set; } = "ACTIVE";
    public string Version { get; set; } = "v1.0";

    public string OwnerVertical { get; set; } = "";
    public string ModuleOwner { get; set; } = "";
    public string MailId { get; set; } = "";

    public string IpNumber { get; set; } = "";
    public DateTime? CreatedDate { get; set; }

    public string PageVersion { get; set; } = "";
}
