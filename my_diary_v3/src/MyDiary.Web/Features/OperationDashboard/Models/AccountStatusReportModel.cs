namespace MyDiary.Web.Features.OperationDashboard.Models
{
    public class AccountStatusReportDto
    {
        // Present Day (Current)
        public DateTime PrevDayDt { get; set; }
        public int PrevDayNoOfDormant { get; set; }
        public int PrevDayNoOfInactive { get; set; }
        public int PrevDayNoOfDeaf { get; set; }
        public int PrevDayNoOfNoNomination { get; set; }
        public int PrevDayLienMarked { get; set; }
        public decimal PrevDayAmountDormant { get; set; }
        public decimal PrevDayAmountInactive { get; set; }
        public decimal PrevDayAmountDeaf { get; set; }
        public decimal PrevDayAmountNoNomination { get; set; }
        public decimal PrevDayAmountLienMarked { get; set; }

        // Previous Day
        public DateTime PrevDay2Dt { get; set; }
        public int PrevDay2NoOfDormant { get; set; }
        public int PrevDay2NoOfInactive { get; set; }
        public int PrevDay2NoOfDeaf { get; set; }
        public int PrevDay2NoOfNoNomination { get; set; }
        public int PrevDay2LienMarked { get; set; }
        public decimal PrevDay2AmountDormant { get; set; }
        public decimal PrevDay2AmountInactive { get; set; }
        public decimal PrevDay2AmountDeaf { get; set; }
        public decimal PrevDay2AmountNoNomination { get; set; }
        public decimal PrevDay2AmountLienMarked { get; set; }

        // Previous Month
        public DateTime PrevMonthDt { get; set; }
        public int PrevMonthNoOfDormant { get; set; }
        public int PrevMonthNoOfInactive { get; set; }
        public int PrevMonthNoOfDeaf { get; set; }
        public int PrevMonthNoOfNoNomination { get; set; }
        public int PrevMonthLienMarked { get; set; }
        public decimal PrevMonthAmountDormant { get; set; }
        public decimal PrevMonthAmountInactive { get; set; }
        public decimal PrevMonthAmountDeaf { get; set; }
        public decimal PrevMonthAmountNoNomination { get; set; }
        public decimal PrevMonthAmountLienMarked { get; set; }

        public string TableName { get; set; }
    }
}