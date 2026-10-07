using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_DEPARTMENT")]
    public class Department
    {
        [Key]
        public long DEPT_ID { get; set; }

        [Required]
        [StringLength(100)]
        public string DEPT_NAME { get; set; } = string.Empty;

        [StringLength(100)]
        public string? DEPT_HEAD { get; set; }

        [Column("DEPT_CODE")]
        [StringLength(50)]
        public string? DEPT_CODE { get; set; }

        [Column("DESCRIPTION")]
        [StringLength(500)]
        public string? DESCRIPTION { get; set; }

        [Column("STATUS")]
        [StringLength(20)]
        public string STATUS { get; set; } = "Active";

        [Column("SORT_ORDER")]
        public int? SORT_ORDER { get; set; }

        [Column("HAS_REVENUE")]
        [StringLength(1)]
        public string HAS_REVENUE { get; set; } = "N";
        [Column("OFFICE_TYPE")]
        [StringLength(20)]
        public string? OFFICE_TYPE { get; set; }

        [Column("ZONE_CODE")]
        [StringLength(6)]
        public string? ZONE_CODE { get; set; }

        [Column("REGION_CODE")]
        [StringLength(6)]
        public string? REGION_CODE { get; set; }

        [Column("BRANCH_CODE")]
        [StringLength(50)]
        public string? BRANCH_CODE { get; set; }

        public DateTime CREATED_AT { get; set; } = DateTime.Now;

        [Column("UPDATED_AT")]
        public DateTime? UPDATED_AT { get; set; }

        [Column("ICON_CLASS")]
        [StringLength(100)]
        public string? ICON_CLASS { get; set; }

        // One Department has many Sections
        public virtual ICollection<Section> Sections { get; set; } = new List<Section>();
    }
}