namespace MyDiary.Web.Features.DitDiary.Models.DTO
{
    public class BudgetRow
    {
        public string SectionName { get; set; } = "";
        public long SectionId { get; set; }
        public int? ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public decimal TotalAllotted { get; set; }
        public decimal Fresh { get; set; }
        public decimal Spillover { get; set; }
        public decimal Utilized { get; set; }
        public bool HasEntry { get; set; }
        public string? SubmittedBy { get; set; }
    }
}