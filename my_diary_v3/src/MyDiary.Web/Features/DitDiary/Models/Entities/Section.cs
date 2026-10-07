using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_SECTION")]
    public class Section
    {
        [Key]
        public long SECTION_ID { get; set; }

        [Required]
        [StringLength(100)]
        public string NAME { get; set; } = string.Empty;

        [Column("SECTION_CODE")]
        [StringLength(50)]
        public string? SECTION_CODE { get; set; }

        [StringLength(500)]
        public string? DESCRIPTION { get; set; }

        [StringLength(100)]
        public string? TEAM_LEAD { get; set; }

        [StringLength(100)]
        public string? AGM { get; set; }

        [StringLength(100)]
        public string? DGM { get; set; }

        [StringLength(100)]
        public string? GM { get; set; }

        [StringLength(100)]
        public string? CGM { get; set; }

        [StringLength(100)]
        public string? CTO { get; set; }

        public long? DEPT_ID { get; set; }

        [Column("STATUS")]
        [StringLength(20)]
        public string STATUS { get; set; } = "Active";

        [Column("SORT_ORDER")]
        public int? SORT_ORDER { get; set; }

        public DateTime CREATED_AT { get; set; } = DateTime.Now;

        public DateTime? UPDATED_AT { get; set; }

        [ForeignKey("DEPT_ID")]
        public virtual Department? Department { get; set; }

        [Column("APPLIES_TO")]
        public string APPLIES_TO { get; set; } = "CAPITAL";
        [Column("ICON_CLASS")]
        [StringLength(100)]
        public string? ICON_CLASS { get; set; }
        [Column("DEACTIVATED_BY_PARENT")]
        [StringLength(1)]
        public string DEACTIVATED_BY_PARENT { get; set; } = "N";
        [Column("CM")]
        [StringLength(100)]
        public string? CM { get; set; }
        [Column("CM_PF")][StringLength(50)] public string? CM_PF { get; set; }
        [Column("AGM_PF")][StringLength(50)] public string? AGM_PF { get; set; }
        [Column("DGM_PF")][StringLength(50)] public string? DGM_PF { get; set; }
        [Column("GM_PF")][StringLength(50)] public string? GM_PF { get; set; }
        [Column("CGM_PF")][StringLength(50)] public string? CGM_PF { get; set; }

    }
}