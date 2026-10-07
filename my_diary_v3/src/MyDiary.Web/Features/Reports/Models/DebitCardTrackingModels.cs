namespace MyDiary.Web.Features.Reports.Models
{
    // Maps to the columns returned by dbo.usp_GetDebitCardTracking
    public class DebitCardTrackingModel
    {
        public int SeqNo { get; set; }
        public string CardNumber { get; set; }
        public string ENCName { get; set; }
        public string AccountNumber { get; set; }
        public string RefOrSoleId { get; set; } // Maps to REF_or_sole_ID
        public string ContactNo { get; set; }
        public string BarCode { get; set; }
        public string CourierName { get; set; }
        public string DispatchDate { get; set; }
    }

    // Wrapper to hold both the data list and the Output parameter (AsOnDate)
    public class DebitCardTrackingResult
    {
        public List<DebitCardTrackingModel> Records { get; set; } = new List<DebitCardTrackingModel>();
        public DateTime? AsOnDate { get; set; }
    }
}