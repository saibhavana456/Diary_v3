namespace MyDiary.Web.Features.IPDirectory.Models
{
    public class IPDirectoryEntry
    {
        public string Name { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string IPPhone { get; set; } = string.Empty;
    }

    public class IPDirectoryResult
    {
        public List<IPDirectoryEntry> Entries { get; set; } = new();
        public int TotalRecords { get; set; }
    }
    public class IPDirectoryEditModel
    {
        public string Name { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string IPPhone { get; set; } = string.Empty;
    }
}
