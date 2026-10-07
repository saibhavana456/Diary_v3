namespace MyDiary.Web.Features.OperationDashboard.Models
{
    public class LockerReportDto
    {
        // Present Day (Current)
        public DateTime PrevDayDt { get; set; }
        public int PrevDayNoOfLockers { get; set; }
        public int PrevDayNoOfVacantLockers { get; set; }
        public int PrevDayNoOfLockersWithDueRent { get; set; }
        public decimal PrevDayTotal { get; set; }

        // Previous Day (PrevDay - 1)
        public DateTime PrevDay2Dt { get; set; }
        public int PrevDay2NoOfLockers { get; set; }
        public int PrevDay2NoOfVacantLockers { get; set; }
        public int PrevDay2NoOfLockersWithDueRent { get; set; }
        public decimal PrevDay2Total { get; set; }

        // Previous Month
        public DateTime PrevMonthDt { get; set; }
        public int PrevMonthNoOfLockers { get; set; }
        public int PrevMonthNoOfVacantLockers { get; set; }
        public int PrevMonthNoOfLockersWithDueRent { get; set; }
        public decimal PrevMonthTotal { get; set; }

        public string TableName { get; set; }
    }
}