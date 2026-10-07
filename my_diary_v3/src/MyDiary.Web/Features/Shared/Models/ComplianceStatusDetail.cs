namespace MyDiary.Web.Features.Shared.Models
{
    //public class ComplianceStatusDetail
    //{
    //    public int SNo { get; set; }
    //    public string LetterRefNo { get; set; } = "";
    //    public string Subject { get; set; } = "";
    //    public string DeptName { get; set; } = "";
    //    public string LetterType { get; set; } = "";
    //    public string AddressedTo { get; set; } = "";
    //    public string ReceivedDate { get; set; } = "";
    //    public string DueDate { get; set; } = "";
    //    public string Status { get; set; } = "";
    //    public string Timeliness { get; set; } = "";
    //}


    public class ComplianceStatusDetail
    {
        public int SNo { get; set; }

        // Letter Serial No.
        public string LetterRefNo { get; set; } = "";

        // Received From
        public string Authority { get; set; } = "";

        public string Subject { get; set; } = "";

        // Send Dept
        public string DeptName { get; set; } = "";

        // Target Date
        public string DueDate { get; set; } = "";

        // Letter Date
        public string LetterDate { get; set; } = "";

        // Received On
        public string ReceivedDate { get; set; } = "";

        // Action Taken
        public string ActionTaken { get; set; } = "";

        // Action Date
        public string ActionDate { get; set; } = "";

        public string Status { get; set; } = "";
        public string Timeliness { get; set; } = "";
    }

}
