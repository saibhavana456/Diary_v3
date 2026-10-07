
using System;

namespace MyDiary.Web.Features.Dashboard_new.Models
{
    /// <summary>
    /// Maps to your WebForms dataset: data_type, PrevDay2NoOfDormant, PrevDayNoOfDormant, PrevDay2Dt_dormant, PrevDayDt_dormant
    /// </summary>
    public sealed class OperationalItem
    {
        public string Parameter { get; set; } = string.Empty;  // data_type
        public decimal PrevDay2No { get; set; }                // PrevDay2NoOfDormant
        public decimal PrevDayNo { get; set; }                 // PrevDayNoOfDormant
        public DateTime PrevDay2Date { get; set; }             // PrevDay2Dt_dormant
        public DateTime PrevDayDate { get; set; }              // PrevDayDt_dormant
    }
}