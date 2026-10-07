// MyDiary.Web.Features.DitDiary/Models.Entities/BudgetUtilization.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_BUDGET_UTIL")]
    public class BudgetUtilization
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Added to support auto-increment
        [Column("S_NO")]
        public int S_NO { get; set; }

        [Column("FINANCIAL_YEAR")]
        public string? FINANCIAL_YEAR { get; set; }

        [Column("MONTH")]
        public string? MONTH { get; set; }

        [Column("VERTICAL")]
        public string? VERTICAL { get; set; }

        [Column("PROJECT_NAME")]
        public string? PROJECT_NAME { get; set; }

        [Column("FRESH_BUDGET")]
        public decimal? FRESH_BUDGET { get; set; }

        [Column("SPILL_OVER_BUDGET")]
        public decimal? SPILL_OVER_BUDGET { get; set; }

        [Column("TOTAL_BUDGET_ALLOCATED")]
        public decimal? TOTAL_BUDGET_ALLOCATED { get; set; }

        [Column("ACTUAL_BUDGET")]
        public decimal? ACTUAL_BUDGET { get; set; }

        [Column("BUDGET_UTILIZATION")]
        public decimal? BUDGET_UTILIZATION_VAL { get; set; }

        [Column("BUDGET_TYPE")]
        public string? BUDGET_TYPE { get; set; }

        [Column("SECTION")]
        public string? SECTION { get; set; }
    }
}