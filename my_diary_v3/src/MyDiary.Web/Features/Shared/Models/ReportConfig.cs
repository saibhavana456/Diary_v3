namespace MyDiary.Web.Features.Shared.Models
{
    public class ReportConfig
    {
        public string Title { get; set; } = string.Empty;
        public string ReportId { get; set; } = string.Empty;
        public List<ColumnConfig> Columns { get; set; } = new();
    }
}
