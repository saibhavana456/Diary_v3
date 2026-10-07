public class BreadcrumbContext
{
    public List<BreadcrumbItem> Items { get; set; } = new();
    public string? PageTitle { get; set; }
}

public class BreadcrumbItem
{
    public string Text { get; set; } = string.Empty;
    public string? Url { get; set; }   // null = current page
}