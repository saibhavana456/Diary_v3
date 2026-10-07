namespace MyDiary.Web.Features.Shared.Models
{
    public class ColumnConfig
    {
        public string Header { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty; // Matches DB field name
        public bool IsNumeric { get; set; }
        public bool IsDate { get; set; }
        public bool IsLink { get; set; }
        public bool IsHtml { get; set; }
        public string LinkText { get; set; } = string.Empty;
        public string Format { get; set; } = "N2"; // e.g., "N2" for Lacs, "N0" for counts
        public string Width { get; set; } = "auto"; // e.g., "N2" for Lacs, "N0" for counts
    }
}
