namespace MyDiary.Web.Features.OperationDashboard.Models
{
    public class OCRMComplaintReportDto
    {
        public DateTime PrevDayDt { get; set; }
        public int PrevDayNoOfComplaints { get; set; }

        public DateTime PrevDay2Dt { get; set; }
        public int PrevDay2NoOfComplaints { get; set; }

        public DateTime PrevMonthDt { get; set; }
        public int PrevMonthNoOfComplaints { get; set; }

        public string TableName { get; set; }
    }
}