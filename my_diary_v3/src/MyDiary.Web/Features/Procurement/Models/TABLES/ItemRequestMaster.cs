using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.Procurement.Models.TABLES
{
    [Table("ITEM_REQUEST_MASTER")]
    public class ItemRequestMaster
    {
        [Column("SR_NO")]
        public int SR_NO { get; set; }

        [Key]
        [Column("REQUEST_CODE")]
        [MaxLength(100)]
        public string? RequestCode { get; set; }

        [Column("BRANCH_CODE")]
        [MaxLength(50)]
        public string? BranchCode { get; set; }

        [Column("FIN_YEAR", TypeName = "NVARCHAR2(50)")]
        [MaxLength(50)]
        public string? FinYear { get; set; }

        [Column("MONTH", TypeName = "NVARCHAR2(50)")]
        [MaxLength(50)]
        public string? Month { get; set; }

        [Column("FINACLE_ID", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? FinacleId { get; set; }

        

        // NCLOB
        [Column("JUSTIFICATION", TypeName = "NCLOB")]
        public string? Justification { get; set; }

        [Column("URGENCY_LEVEL", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? UrgencyLevel { get; set; }

        // BLOB
        [Column("ATTACHMENT", TypeName = "BLOB")]
        public byte[]? Attachment { get; set; }

        [Column("ATTACHMENT_NAME")]
        public string? AttachmentName { get; set; }

        [Column("RAISED_BY", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? RaisedBy { get; set; }

        // DATE
        [Column("RAISED_ON", TypeName = "DATE")]
        public DateTime? RaisedOn { get; set; }

        [Column("BRANCH_RECOMMENDATION", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? BranchRecommendation { get; set; }

        // NCLOB
        [Column("BRANCH_REMARKS", TypeName = "NCLOB")]
        public string? BranchRemarks { get; set; }

        [Column("BRANCH_STATUS_DATE", TypeName = "DATE")]
        public DateTime? BranchStatusDate { get; set; }
        [Column("CHECKER_APPROVER_NAME", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? CheckerApproverName { get; set; }

        [Column("REGION_RECOMMENDATION", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? RegionRecommendation { get; set; }

        // NCLOB
        [Column("REGION_REMARKS", TypeName = "NCLOB")]
        public string? RegionRemarks { get; set; }

        [Column("REGION_STATUS_DATE", TypeName = "DATE")]
        public DateTime? RegionStatusDate { get; set; }

        [Column("REGION_APPROVED_QUANTITY")]
        public long? RegionApprovedQuantity { get; set; }

        [Column("REGION_FORWARD_TO", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? RegionForwardTo { get; set; }

        [Column("REGION_APPROVER_PF_NO", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? RegionApproverPfNo { get; set; }

        [Column("REGION_APPROVER_NAME", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? RegionApproverName { get; set; }

        [Column("ZONE_RECOMMENDATION", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? ZoneRecommendation { get; set; }

        // NCLOB
        [Column("ZONE_REMARKS", TypeName = "NCLOB")]
        public string? ZoneRemarks { get; set; }

        [Column("ZONE_STATUS_DATE", TypeName = "DATE")]
        public DateTime? ZoneStatusDate { get; set; }

        [Column("ZONE_APPROVED_QUANTITY")]
        public long? ZoneApprovedQuantity { get; set; }

        [Column("ZONE_FORWARD_TO", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? ZoneForwardTo { get; set; }

        [Column("ZONE_APPROVER_PF_NO", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? ZoneApproverPfNo { get; set; }

        [Column("ZONE_APPROVER_NAME", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? ZoneApproverName { get; set; }

        [Column("CO_RECOMMENDATION", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? CoRecommendation { get; set; }

        // NCLOB
        [Column("CO_REMARKS", TypeName = "NCLOB")]
        public string? CoRemarks { get; set; }

        [Column("CO_STATUS_DATE", TypeName = "DATE")]
        public DateTime? CoStatusDate { get; set; }

        [Column("CO_APPROVED_QUANTITY")]
        public long? CoApprovedQuantity { get; set; }

        [Column("CO_FORWARD_TO", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? CoForwardTo { get; set; }

        [Column("CO_APPROVER_PF_NO", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? CoApproverPfNo { get; set; }

        [Column("CO_APPROVER_NAME", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? CoApproverName { get; set; }

        [Column("IS_DELETED")]
        public bool? ISDELETED { get; set; }

        [Column("DELETED_BY")]
        public string? DeletedBy { get; set; }

        [Column("DELETED_ON")]
        public DateTime? DeletedOn { get; set; }

        public ICollection<RequestItem>? RequestItem { get; set; }
    }
}
