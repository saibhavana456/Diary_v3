using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_SECTION_TEAM")]
    public class SectionTeamMember
    {
        [Key]
        [Column("MEMBER_ID")]
        public long MEMBER_ID { get; set; }

        [Column("SECTION_ID")] public long SECTION_ID { get; set; }
        [Column("PF_NUMBER")] public string PF_NUMBER { get; set; } = "";
        [Column("MEMBER_NAME")] public string? MEMBER_NAME { get; set; }
        //[Column("ROLE")] public string? ROLE { get; set; }
        [Column("MEMBER_ROLE")]
        public string? MEMBER_ROLE { get; set; }
        [Column("STATUS")] public string? STATUS { get; set; }
        [Column("CREATED_BY")] public string? CREATED_BY { get; set; }
        [Column("CREATED_AT")] public DateTime? CREATED_AT { get; set; }
    }
}