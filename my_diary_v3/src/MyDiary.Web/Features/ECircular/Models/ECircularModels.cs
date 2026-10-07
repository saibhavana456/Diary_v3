namespace MyDiary.Web.Features.ECircular.Models;

/// <summary>
/// Lookup item for dropdowns (Department, Circular Type).
/// </summary>
public sealed record ECircularLookupItem(string Id, string Name);

/// <summary>
/// Represents a special listing button (e.g., Caution Advice, Latest Banking News, eAB portal).
/// </summary>
public sealed record SpecialListingOption(
    string Key,
    string DisplayName,
    string UrlTemplate,
    bool OpenInNewTab = false);

/// <summary>
/// Contains all lookup data for ECircular search filters.
/// </summary>
public sealed class ECircularFilterLookup
{
    public List<ECircularLookupItem> Departments { get; set; } = [];
    public List<ECircularLookupItem> CircularTypes { get; set; } = [];
    public List<int> Years { get; set; } = [];
    public List<SpecialListingOption> SpecialListings { get; set; } = [];
}

/// <summary>
/// Search request model for the ECircular DMS API.
/// </summary>
public sealed class ECircularSearchRequest
{
    public string? CircularType { get; set; } = "";
    public string? CircularNo { get; set; } = "";
    public string? Subject { get; set; } = "";
    public string? DepartmentName { get; set; } = "";
    public string? SectionName { get; set; } = "";
    public int? PublishYear { get; set; }
    public int PageNo { get; set; } = 0;

    public bool IsEmpty() =>
        string.IsNullOrWhiteSpace(CircularType) &&
        string.IsNullOrWhiteSpace(CircularNo) &&
        string.IsNullOrWhiteSpace(Subject) &&
        string.IsNullOrWhiteSpace(DepartmentName) &&
        string.IsNullOrWhiteSpace(SectionName) &&
        PublishYear is null;
}

/// <summary>
/// Individual circular item from search results.
/// </summary>
public sealed class ECircularItem
{
    public string Sno { get; set; } = "";
    public string CircularNo { get; set; } = "";
    public string CircularType { get; set; } = "";
    public string PublishDate { get; set; } = "";
    public string Language { get; set; } = "";
    public string Department { get; set; } = "";
    public string Section { get; set; } = "";
    public string Subject { get; set; } = "";
    public string CircularLink { get; set; } = "";
}
