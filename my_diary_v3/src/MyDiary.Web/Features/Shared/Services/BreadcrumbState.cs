namespace MyDiary.Web.Features.Shared.Services;

/// <summary>
/// Scoped service that holds the breadcrumb navigation trail.
/// Dashboards2 (Assurance Hub), BusinessHub and selector pages push their
/// context here before navigating, so child pages can render the
/// breadcrumb without query-string clutter.
/// </summary>
public class BreadcrumbState
{
    /// <summary>Root origin label (e.g. "Assurance Hub", "Business Hub").</summary>
    public string SourceLabel { get; private set; } = "Assurance Hub";

    /// <summary>Root origin href (e.g. "Dashboards2", "BusinessHub").</summary>
    public string SourceHref { get; private set; } = "Dashboards2";

    /// <summary>Optional mid-level selector label (e.g. "Accounts Open/Closed").</summary>
    public string? SelectorLabel { get; private set; }

    /// <summary>Optional mid-level selector href.</summary>
    public string? SelectorHref { get; private set; }

    /// <summary>Current (leaf) page title.</summary>
    public string? CurrentLabel { get; private set; }

    /// <summary>Category the item belongs to (e.g. "Assurance Corner", "Alerts").</summary>
    public string? Category { get; private set; }

    /// <summary>SubCategory the item belongs to (e.g. "Operations", "Risk").</summary>
    public string? SubCategory { get; private set; }

    /// <summary>True once any page has pushed context.</summary>
    public bool HasContext => !string.IsNullOrWhiteSpace(SourceLabel);

    /// <summary>
    /// Called by a hub page (Assurance Hub / Business Hub) before navigating
    /// to a direct report or selector.
    /// </summary>
    public void SetFromDashboard(string reportTitle, string? category = null, string? subCategory = null,
        string sourceLabel = "Assurance Hub", string sourceHref = "Dashboards2")
    {
        SourceLabel = sourceLabel;
        SourceHref = sourceHref;
        SelectorLabel = null;
        SelectorHref = null;
        CurrentLabel = reportTitle;
        Category = category;
        SubCategory = subCategory;
    }

    /// <summary>
    /// Called by a selector page before navigating to a nested report.
    /// </summary>
    public void SetFromSelector(string selectorLabel, string selectorHref, string reportTitle,
        string? category = null, string? subCategory = null,
        string? sourceLabel = null, string? sourceHref = null)
    {
        if (sourceLabel != null) SourceLabel = sourceLabel;
        if (sourceHref != null) SourceHref = sourceHref;
        SelectorLabel = selectorLabel;
        SelectorHref = selectorHref;
        CurrentLabel = reportTitle;
        if (category != null) Category = category;
        if (subCategory != null) SubCategory = subCategory;
    }

    /// <summary>
    /// Called by a selector page on load to set itself as the current leaf
    /// (so the breadcrumb shows SourceLabel > SelectorName).
    /// </summary>
    public void SetSelectorAsCurrent(string selectorLabel)
    {
        SelectorLabel = null;
        SelectorHref = null;
        CurrentLabel = selectorLabel;
    }
}
