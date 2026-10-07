namespace MyDiary.Web.Features.Shared.Models
{
    public class ComplianceFilters
    {
        public int FyStartYear { get; set; }      // e.g., 2025 => FY 2025-04-01 to 2026-03-31 (default Apr–Mar)

        // ✅ NEW: Custom date range filters
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? DeptName { get; set; }
        public string? LetterType { get; set; }
        public string? AddressedTo { get; set; }
        public string? DashStatus { get; set; }   // Submitted | Interim | Pending
        public string? Timeliness { get; set; }   // Intime | Delayed
        public string? Authority { get; set; } = "All";
        public string? PendingBucket { get; set; } = "All";
    }
}
