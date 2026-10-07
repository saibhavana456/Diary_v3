namespace MyDiary.Web.Features.RatesCharges.Models
{
    public class MclrRate
    {
        public string Tenor { get; set; } = "";
        public string Tenor_HI { get; set; } = "";          // ✅ NEW
        public string Rate { get; set; } = "";
        public DateTime EffectiveFrom { get; set; }
        public DateTime EffectiveTo { get; set; }
        public int SortOrder { get; set; }
    }

    public class BaseRateBplr
    {
        public string RateType { get; set; } = "";
        public string RateType_HI { get; set; } = "";       // ✅ NEW
        public string EffectiveDate { get; set; } = "";
        public string Rate { get; set; } = "";
        public int SortOrder { get; set; }
    }

    public class RatesChargesLink
    {
        public string Section { get; set; } = "";
        public string Title { get; set; } = "";
        public string Title_HI { get; set; } = "";          // ✅ NEW
        public string Url { get; set; } = "";
        public int SortOrder { get; set; }
    }
}
