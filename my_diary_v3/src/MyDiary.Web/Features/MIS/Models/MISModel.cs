namespace MyDiary.Web.Features.MIS.Models
{
    public class MISModel
    {
        public int S_NO { get; set; }
        public string VERTICAL { get; set; } = string.Empty;
        public string SUB_CATEGORY { get; set; } = string.Empty;
        public string CATEGORY { get; set; } = string.Empty;
        public string URL { get; set; } = string.Empty;
    }

    public class QlikTicketDetails
    {
        public int sno { get; set; }
        public string user_id { get; set; } = string.Empty;
        public string qlik_url { get; set; } = string.Empty;
        public string qlik_ticket { get; set; } = string.Empty;
        public string entry_date_timestamp { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
    }
}