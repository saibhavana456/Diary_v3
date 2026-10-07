using System;

namespace MyDiary.Web.Features.Business.Models
{
    // Used in Sections 4 (Consumer), can reuse Complaint VM
    public class PartBComplaintVm
    {
        public string? ComplainantName { get; set; }
        public DateTime? ComplaintDate { get; set; }
        public string? Remarks { get; set; }
    }

    // Used in Sections 5, 9, 10(a), 10(b)
    public class PartBAccountVm
    {
        public string? AccountNo { get; set; }
        public string? BorrowerName { get; set; }
        public string? Remarks { get; set; }
    }

    // Used in Section 6
    public class PartBCapitalAssetVm
    {
        public string? AssetDescription { get; set; }
        public string? Discrepancy { get; set; }
        public string? Remarks { get; set; }
    }

    // Used in Section 7
    public class PartBSundryVm
    {
        public DateTime? EntryDate { get; set; }
        public decimal Amount { get; set; }
        public string? Particulars { get; set; }
    }
}