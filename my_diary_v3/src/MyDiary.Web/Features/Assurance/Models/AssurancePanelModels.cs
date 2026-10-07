namespace MyDiary.Web.Features.Assurance.Models;

/// <summary>One panel (e.g. "KYC", "Renewal Pendency") with its column headers and row data.</summary>
public sealed class AssurancePanel
{
    public int PanelId { get; set; }
    public string Title { get; set; } = "";
    public string? RowLabelHeader { get; set; }
    public List<string> ColumnHeaders { get; set; } = new();   
    public List<AssurancePanelRow> Rows { get; set; } = new();

    //public string AsOnDate {  get; set; }
}

public sealed class AssurancePanelRow
{
    public string RowLabel { get; set; } = "";
    public long? ParameterId { get; set; }
    public List<string> Cells { get; set; } = new();
}

/// <summary>A row of panels that render side by side (same CATEGORY_LABEL + PAIR_GROUP).</summary>
public sealed class AssurancePanelRowGroup
{
    public List<AssurancePanel> Panels { get; set; } = new();
}

/// <summary>One red category banner, containing one or more side-by-side panel row-groups.</summary>
public sealed class AssuranceCategory
{
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
    public List<AssurancePanelRowGroup> PanelRowGroups { get; set; } = new();
}
