namespace MyDiary.Web.Features.Departments.Models
{
    public class Activity
    {
        public int ActivityID { get; set; }
        public string ActivityName { get; set; } = string.Empty;
        public string ActivityCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int EstimatedHours { get; set; }
    }
}
