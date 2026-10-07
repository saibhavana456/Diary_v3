namespace MyDiary.Web.Features.Departments.Models
{
    public class Document
    {
        public int DocumentID { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public int Downloads { get; set; }
        public int ActivityID { get; set; }
        public string NewFilePath { get; set; } = string.Empty;
        public bool IsJspCompleted { get; set; }
    }

    public class DocumentResult
    {
        public List<Document> Documents { get; set; } = new();
        public int TotalRecords { get; set; }
        public string ActivityName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public int DepartmentID { get; set; }
    }
}
