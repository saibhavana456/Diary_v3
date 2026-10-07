using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_PROJECT_MILESTONE")]
    public class ProjectMilestone
    {
        [Key]
        [Column("MILESTONE_ID")]
        public decimal MilestoneId { get; set; }

        [Column("PROJECT_CODE")]
        public string? ProjectCode { get; set; }

        [Column("MILESTONE_CODE")]
        public string? MilestoneCode { get; set; }

        [Column("MILESTONE_NAME")]
        public string? MilestoneName { get; set; }

        [Column("MILESTONE_DESCRIPTION")]
        public string? MilestoneDescription { get; set; } 

        [Column("PLANNED_DATE")]
        public DateTime? PlannedDate { get; set; }

        [Column("BASELINE_DATE")]
        public DateTime? BaselineDate { get; set; }

        [Column("REVISED_DATE")]
        public DateTime? RevisedDate { get; set; }

        [Column("ACTUAL_DATE")]
        public DateTime? ActualDate { get; set; }

        [Column("OWNER_NAME")]
        public string? OwnerName { get; set; }

        [Column("STATUS")]
        public string? Status { get; set; }

        [Column("DEPENDENCIES")]
        public string? Dependencies { get; set; } // Mapped for CLOB

        [Column("IS_SLA_MET")]
        public string? IsSlaMet { get; set; } // Mapped for CHAR(1)

        [Column("IS_PENALTY_APPLICABLE")]
        public string? IsPenaltyApplicable { get; set; } // Mapped for CHAR(1)
    }
}