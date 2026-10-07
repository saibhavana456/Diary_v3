using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_PROJECT_RESOURCE")]
    public class ProjectResource
    {
        [Key]
        [Column("RESOURCE_ID")]
        public decimal ResourceId { get; set; }

        [Column("RESOURCE_CODE")]
        public string? ResourceCode { get; set; }

        [Column("PROJECT_ID")]
        public decimal ProjectId { get; set; }

        [Column("PROJECT_CODE")]
        public string? ProjectCode { get; set; }

        [Column("RESOURCE_NAME")]
        public string? ResourceName { get; set; }

        [Column("RESOURCE_DESIG")]
        public string? ResourceDesig { get; set; }

        [Column("RESOURCE_ROLE")]
        public string? ResourceRole { get; set; }

        [Column("RESOURCE_SKILLSET")]
        public string? ResourceSkillset { get; set; } // Mapped for CLOB

        [Column("RESOURCE_TYPE")]
        public string? ResourceType { get; set; }

        [Column("RESOURCE_REMARK")]
        public string? ResourceRemark { get; set; } // Mapped for CLOB

        [Column("RESOURCE_START_DATE")]
        public DateTime? ResourceStartDate { get; set; }

        [Column("RESOURCE_END_DATE")]
        public DateTime? ResourceEndDate { get; set; }
    }
}