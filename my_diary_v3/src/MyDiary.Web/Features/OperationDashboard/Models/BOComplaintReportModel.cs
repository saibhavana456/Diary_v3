namespace MyDiary.Web.Features.OperationDashboard.Models
{
    public class BOComplaintReportDto
    {
        // Present Day
        public DateTime PrevDayDt { get; set; }
        public int PrevDayNoOfComplaints { get; set; }

        // Previous Day
        public DateTime PrevDay2Dt { get; set; }
        public int PrevDay2NoOfComplaints { get; set; }

        // Previous Month
        public DateTime PrevMonthDt { get; set; }
        public int PrevMonthNoOfComplaints { get; set; }

        public string TableName { get; set; }
    }
}