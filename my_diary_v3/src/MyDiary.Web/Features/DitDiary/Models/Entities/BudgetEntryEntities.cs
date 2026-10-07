// MyDiary.Web.Features.DitDiary/Models/Entities/BudgetEntryEntities.cs
// New budget-entry tables (Savita 17-07: our tables inside DIT_DIARY schema,
// mapped to DIT Diary masters PROJECTS_MASTER + SECTIONS).
// One file, six entities — split into separate files if the team prefers.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_FINANCIAL_YEAR")]
    public class FinancialYear
    {
        [Key]
        [Column("FY_ID")]
        public int FyId { get; set; }

        [Column("FY_LABEL")]
        public string FyLabel { get; set; } = "";

        [Column("IS_CURRENT")]
        public string IsCurrent { get; set; } = "N";

        [Column("IS_ACTIVE")]
        public string IsActive { get; set; } = "Y";
    }

    [Table("RP_DIT_EXPENDITURE_HEAD")]
    public class ExpenditureHead
    {
        [Key]
        [Column("ID")]
        public int HeadId { get; set; }

        [Column("NAME")]
        public string HeadName { get; set; } = "";

        [Column("IS_ACTIVE")]
        public string IsActive { get; set; } = "Y";
    }

    [Table("RP_DIT_BUDGET_ALLOC")]
    public class BudgetAllocation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ALLOC_ID")]
        public int AllocId { get; set; }

        [Column("FY_ID")]
        public int FyId { get; set; }

        [Column("BUDGET_TYPE")]
        public string BudgetType { get; set; } = "CAPITAL";

        [Column("PROJECT_ID")]
        public int? ProjectId { get; set; }

        [Column("SECTION_ID")]
        public long? SectionId { get; set; }

        [Column("SPILLOVER_ALLOTTED")]
        public decimal SpilloverAllotted { get; set; }

        [Column("FRESH_ALLOTTED")]
        public decimal FreshAllotted { get; set; }

        [Column("TOTAL_ALLOTTED")]
        public decimal TotalAllotted { get; set; }

        [Column("IS_ACTIVE")]
        public string IsActive { get; set; } = "Y";
    }

    [Table("RP_DIT_CAPITAL_UTIL")]
    public class CapitalUtilization
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ENTRY_ID")]
        public int EntryId { get; set; }

        [Column("PROJECT_ID")]
        public int ProjectId { get; set; }

        [Column("FY_ID")]
        public int FyId { get; set; }

        [Column("MONTH_NAME")]
        public string MonthName { get; set; } = "";

        [Column("ACTUAL_SPILLOVER")]
        public decimal ActualSpillover { get; set; }

        [Column("ACTUAL_FRESH")]
        public decimal ActualFresh { get; set; }

        [Column("EST_SPILLOVER_NEXT")]
        public decimal EstSpilloverNext { get; set; }

        [Column("EST_FRESH_NEXT")]
        public decimal EstFreshNext { get; set; }

        [Column("JUSTIFICATION")]
        public string? Justification { get; set; }

        [Column("SUBMITTED_BY")]
        public string? SubmittedBy { get; set; }

        [Column("SUBMISSION_DATE")]
        public DateTime SubmissionDate { get; set; } = DateTime.Now;

        [Column("IS_ACTIVE")]
        public string IsActive { get; set; } = "Y";
    }

    [Table("RP_DIT_REVENUE_UTIL")]
    public class RevenueUtilization
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ENTRY_ID")]
        public int EntryId { get; set; }

        [Column("SECTION_ID")]
        public long SectionId { get; set; }

        [Column("PROJECT_ID")]
        public int? ProjectId { get; set; }

        [Column("HEAD_ID")]
        public int HeadId { get; set; }

        [Column("FY_ID")]
        public int FyId { get; set; }

        [Column("MONTH_NAME")]
        public string MonthName { get; set; } = "";

        [Column("AMOUNT")]
        public decimal Amount { get; set; }

        [Column("JUSTIFICATION")]
        public string? Justification { get; set; }

        [Column("SUBMITTED_BY")]
        public string? SubmittedBy { get; set; }

        [Column("SUBMISSION_DATE")]
        public DateTime SubmissionDate { get; set; } = DateTime.Now;

        [Column("IS_ACTIVE")]
        public string IsActive { get; set; } = "Y";
    }

    [Table("RP_DIT_OVERBUDGET_LOG")]
    public class OverbudgetLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("LOG_ID")]
        public int LogId { get; set; }

        [Column("BUDGET_TYPE")]
        public string BudgetType { get; set; } = "CAPITAL";

        [Column("PROJECT_ID")]
        public int? ProjectId { get; set; }

        [Column("SECTION_ID")]
        public long? SectionId { get; set; }

        [Column("FY_ID")]
        public int? FyId { get; set; }

        [Column("MONTH_NAME")]
        public string? MonthName { get; set; }

        [Column("ATTEMPTED_AMOUNT")]
        public decimal AttemptedAmount { get; set; }

        [Column("EXCEEDED_BY")]
        public decimal ExceededBy { get; set; }

        [Column("SUBMITTED_BY")]
        public string? SubmittedBy { get; set; }

        [Column("LOG_DATE")]
        public DateTime LogDate { get; set; } = DateTime.Now;
    }
}