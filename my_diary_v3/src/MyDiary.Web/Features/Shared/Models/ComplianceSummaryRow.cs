namespace MyDiary.Web.Features.Shared.Models
{
    public class ComplianceSummaryRow
    {
        public string Authority { get; set; } = "";
        public int CntSubmittedIntime { get; set; }
        public int CntSubmittedDelayed { get; set; }
        public int CntTotalSubmitted { get; set; }
        public int CntInterimIntime { get; set; }
        public int CntInterimDelayed { get; set; }
        public int CntTotalInterim { get; set; }
        public int CntPendingInprogress { get; set; }
        public int CntPendingDelayed { get; set; }
        public int CntPending { get; set; }
        public int CntTotal { get; set; }
    }
}
