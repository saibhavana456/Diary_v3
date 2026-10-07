using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MyDiary.Web.Features.Procurement.Models.TABLES
{
    [Table("USER_MASTER")]
    public class UserMaster
    {

        [Column("SR_NO")]
        public int SR_NO { get; set; }

        [Key]
        [Column("PF_NO")]
        public string? PF_NO { get; set; }

        [Column("OFFICE_TYPE")]
        public string? OfficeType { get; set; }

        [Column("ROLE")]
        public string? Role { get; set; }

        [Column("ACCESS_LEVEL")]
        public string? AccessLevel { get; set; }

        [Column("EFFECTIVE_DATE")]
        public DateTime? EffectiveDate { get; set; }

        [Column("REMARKS")]
        public string? REMARKS { get; set; }

        [Column("REPORTING_OFFICER_PF_NO")]
        public string? ReportingOfficerPFNo { get; set; }

        [Column("REPORTING_OFFICER_NAME")]
        public string? ReportingOfficerName { get; set; }

        [Column("CREATED_BY")]
        public string? CreatedBy { get; set; }

        [Column("CREATED_DATE")]
        public DateTime? CreatedDate { get; set; }

        [Column("IS_DELETED")]
        public bool? ISDELETED { get; set; }

        [Column("DELETED_BY")]
        public string? DeletedBy { get; set; }

        [Column("DELETED_ON")]
        public DateTime? DeletedOn { get; set; }
    }
}
