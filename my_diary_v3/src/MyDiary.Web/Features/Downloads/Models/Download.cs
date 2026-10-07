namespace MyDiary.Web.Features.Downloads.Models
{
    public class Download
    {
        public int ActivityID { get; set; }
        public string ActivityName { get; set; } = "";
        public string ActivityName_HI { get; set; } = "";   // ✅ NEW

        public string ActivityCode { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconClass { get; set; } = "";
        public string Status { get; set; } = "";

        // ✅ Language-aware display
        public string DisplayName(string lang) =>
            lang == "hi" && !string.IsNullOrWhiteSpace(ActivityName_HI)
                ? ActivityName_HI
                : ActivityName;
    }
}