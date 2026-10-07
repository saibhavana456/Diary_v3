using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_PROJECT_LEGACY")]
    public class ProjectMaster
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("SNO")]
        public decimal SNO { get; set; }

        [Column("ID")]
        public string? ID { get; set; }

        [Column("NAME")]
        public string NAME { get; set; } = string.Empty;

        public string? ACTION { get; set; }

        public string? PROJECTCATEGORY { get; set; }

        public string? PROJECTTYPE { get; set; }

        public string? DEPARTMENT { get; set; }

        public string? PROJECTLEAD { get; set; }

        public string? BANKLEAD { get; set; }

        public string? TEAMLEADER { get; set; }

        public string? TEAMNAME { get; set; }

        public string? DGM { get; set; }

        public string? DGMID { get; set; }

        public string? MAKER { get; set; }

        public string? MAKERNAME { get; set; }

        public string? CHECKER { get; set; }

        public string? ROLES { get; set; }

        public string? APPROVINGAUTH { get; set; }

        public string? ADMINAPPROVALAUTH { get; set; }

        public DateTime? ADMINAPPROVALDATE { get; set; }

        public string? FINANCIALAPPROVALAUTH { get; set; }

        public DateTime? FINANCIALAPPROVALDATE { get; set; }

        public DateTime? DATEOFACCEPTANCE { get; set; }

        public string? NEED { get; set; }

        public string? BRIEF { get; set; }

        public string? BENEFITS { get; set; }

        public string? REMARKS { get; set; }

        public string? PMOREMARKS { get; set; }

        public DateTime? ENTRYDATE { get; set; }

        public DateTime? PROJECTSTARTDATE { get; set; }

        public DateTime? ITSTRDATE { get; set; }

        public DateTime? SCHEDULEDCOMPLETIONDATE { get; set; }

        public DateTime? EXPECTEDCOMPLETIONDATE { get; set; }

        public DateTime? PROJECTCOMPLETIONDATE { get; set; }

        public DateTime? COMPLETION { get; set; }

        public DateTime? ISSUEDATE { get; set; }

        public DateTime? UPDATEDATE { get; set; }

        public DateTime? REVIEWDATE { get; set; }

        public string? TIMELINE { get; set; }

        // Mapped as string because table definition uses VARCHAR2
        public string? BASECOST { get; set; }

        public string? TAX { get; set; }

        public string? COST { get; set; }

        public string? AMOUNTPAIDTOVENDOR { get; set; }

        public string? ESTIMATEDCOSTOVERRUN { get; set; }

        public string? REVISEDCOSTAFTERECO { get; set; }

        public string? VENDORNAME { get; set; }

        public string? VENDORCONTACT { get; set; }

        public string? RFPFLOATED { get; set; }

        public DateTime? RFPFLOATINGDATE { get; set; }

        public DateTime? RFPAPPDATE { get; set; }

        public DateTime? RFPCOMPLETIONDATE { get; set; }

        public string? RFPSTATUS { get; set; }

        public string? PURCHASEORDERISSUE { get; set; }

        public string? MILESTONE_1 { get; set; }

        public DateTime? MILESTONE_DATE_1 { get; set; }

        public string? MILESTONE_2 { get; set; }

        public DateTime? MILESTONE_DATE_2 { get; set; }

        public string? MILESTONE_3 { get; set; }

        public DateTime? MILESTONE_DATE_3 { get; set; }

        public string? MILESTONE_4 { get; set; }

        public DateTime? MILESTONE_DATE_4 { get; set; }

        public string? MILESTONE_5 { get; set; }

        public DateTime? MILESTONE_DATE_5 { get; set; }

        public string? SUPPVERT { get; set; }

        public string? SUPPORTING_DEPARTMENT { get; set; }

        public string? STATUS { get; set; }

        public string? PREVSTATUS { get; set; }

        public string? TICKETSTATUS { get; set; }

        public int? PENDING { get; set; }

        public int? CHECKERVERIFIED { get; set; }

        public int? FLAG { get; set; }

        public string? REASONDELAY { get; set; }

        public string? REJECTREASON { get; set; }

        public string? FILENAME { get; set; }

        public int? CONSULTREQ { get; set; }

        public string? IPPHONE { get; set; }

        public string? LATESTMEETING { get; set; }

        public string? UPDATEDDEPARTMENT { get; set; }

        public string? UPDATEDDGM { get; set; }

        public string? UPDATEDDGMID { get; set; }

        public string? UPDATEDMAKER { get; set; }

        public string? SECTION { get; set; }


    }
}