namespace RequestPortal.Web.Features.PageInfo.Models;

/// <summary>
/// Metadata record for a portal page — stored in RP_PAGE_INFO table.
/// Displayed via the floating ⓘ button on every page.
/// </summary>
public record PageInfo(
    string   PageKey,
    string   PageName,
    string   PageType,
    string   Description,
    string   OwnerVertical,
    string   ModuleOwner,
    string   IpNumber,
    string   MailId,
    DateTime CreatedDate,
    string   Version,
    string   Status);

/// <summary>Mutable form model for add / edit operations.</summary>
public class PageInfoForm
{
    public string PageKey       { get; set; } = "";
    public string PageName      { get; set; } = "";
    public string PageType      { get; set; } = "Dashboard";
    public string Description   { get; set; } = "";
    public string OwnerVertical { get; set; } = "";
    public string ModuleOwner   { get; set; } = "";
    public string IpNumber      { get; set; } = "";
    public string MailId        { get; set; } = "";
    public string Version       { get; set; } = "v1.0";
    public string Status        { get; set; } = "ACTIVE";

    public static PageInfoForm From(PageInfo p) => new()
    {
        PageKey       = p.PageKey,
        PageName      = p.PageName,
        PageType      = p.PageType,
        Description   = p.Description,
        OwnerVertical = p.OwnerVertical,
        ModuleOwner   = p.ModuleOwner,
        IpNumber      = p.IpNumber,
        MailId        = p.MailId,
        Version       = p.Version,
        Status        = p.Status,
    };
}
