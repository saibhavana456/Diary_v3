using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_PROJECT_ISSUE")]
    public class ProjectIssue
    {
        [Key]
        [Column("ISSUE_ID")]
        public decimal IssueId { get; set; }

        [Column("ISSUE_CODE")]
        public string? IssueCode { get; set; }

        [Column("PROJECT_CODE")]
        public string? ProjectCode { get; set; }

        [Column("ISSUE_DESCRIPTION")]
        public string? IssueDescription { get; set; } // Mapped for CLOB

        [Column("IMPACT")]
        public string? Impact { get; set; } // Mapped for CLOB

        [Column("ROOT_CAUSE")]
        public string? RootCause { get; set; } // Mapped for CLOB

        [Column("OWNER_NAME")]
        public string? OwnerName { get; set; }

        [Column("STATUS")]
        public string? Status { get; set; }

        [Column("RESOLUTION_SUMMARY")]
        public string? ResolutionSummary { get; set; } // Mapped for CLOB

        [Column("DATE_RAISED")]
        public DateTime? DateRaised { get; set; }

        [Column("TARGET_RESOLUTION_DATE")]
        public DateTime? TargetResolutionDate { get; set; }

        [Column("ACTUAL_RESOLUTION_DATE")]
        public DateTime? ActualResolutionDate { get; set; }
    }
}