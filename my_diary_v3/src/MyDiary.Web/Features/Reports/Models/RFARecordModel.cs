namespace MyDiary.Web.Features.Reports.Models
{
    public class RFARecordModel
    {
        public long SNo { get; set; }

        public long UploadId { get; set; }

        public DateTime PositionAsOfDate { get; set; }

        public string? ZonalOffice { get; set; }

        public string? RegionalOffice { get; set; }

        public string? BranchName { get; set; }

        public string? BranchSOL { get; set; }

        public string? NameOfBorrower { get; set; }

        public string? CIFID { get; set; }

        public string? PAN { get; set; }

        public string? NameOfBank { get; set; }

        public DateTime? DateOfRFAClassification { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public string? Remarks { get; set; }

        public bool IsActive { get; set; }
    }

    public class RFAUploadDate
    {
        public long UploadId { get; set; }

        public DateTime PositionAsOfDate { get; set; }
    }

    public class RFAListResult
    {
        public List<RFARecordModel> Records { get; set; } = new();

        public int TotalRecords { get; set; }
    }

    public class RFAImportResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public int Inserted { get; set; }

        public int Updated { get; set; }

        public int Skipped { get; set; }
    }

    public class RFAAuditLogEntry
    {
        public long AuditId { get; set; }

        public long SNo { get; set; }

        public string? FieldName { get; set; }

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public string? ModifiedBy { get; set; }

        public DateTime ModifiedDate { get; set; }
    }

    public class RFAEditModel
    {
        public long SNo { get; set; }

        public long UploadId { get; set; }

        public DateTime PositionAsOfDate { get; set; }

        public string ZonalOffice { get; set; } = string.Empty;

        public string RegionalOffice { get; set; } = string.Empty;

        public string BranchName { get; set; } = string.Empty;

        public string BranchSOL { get; set; } = string.Empty;

        public string NameOfBorrower { get; set; } = string.Empty;

        public string CIFID { get; set; } = string.Empty;

        public string PAN { get; set; } = string.Empty;

        public string NameOfBank { get; set; } = string.Empty;

        public DateTime? DateOfRFAClassification { get; set; }

        public string Remarks { get; set; } = string.Empty;
    }

    public class RFAUploadHistoryModel
    {
        public long UploadId { get; set; }

        public DateTime PositionAsOfDate { get; set; }

        public int VersionNo { get; set; }

        public string UploadedBy { get; set; } = "";

        public DateTime UploadedOn { get; set; }

        public string UploadType { get; set; } = "";

        public string FileName { get; set; } = "";

        public long FileSize { get; set; }
    }

    public class RFAAuditSummaryModel
    {
        public DateTime PositionAsOfDate { get; set; }

        public int VersionNo { get; set; }

        public string UploadType { get; set; } = "";

        public string UploadedBy { get; set; } = "";

        public DateTime UploadedOn { get; set; }

        public int DownloadCount { get; set; }
    }

    public class RFADownloadDetailModel
    {
        public DateTime PositionAsOfDate { get; set; }

        public string DownloadedBy { get; set; } = "";

        public DateTime DownloadedDate { get; set; }

        public string DownloadType { get; set; } = "";
    }

}
