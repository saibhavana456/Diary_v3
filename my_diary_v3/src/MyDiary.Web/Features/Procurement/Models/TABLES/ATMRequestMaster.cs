using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.Procurement.Models.TABLES
{
    [Table("ATM_REQUEST_MASTER")]
    public class ATMRequestMaster
    {

        [Column("SR_NO")]
        public int SR_NO { get; set; }

        [Key]
        [Column("REQUEST_CODE")]
        [MaxLength(100)]
        public string? RequestCode { get; set; }

        [Column("ATM_ID")]
        [MaxLength(100)]
        public string? ATMId { get; set; }

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


        [Column("REGION_FORWARD_TO", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? RegionForwardTo { get; set; }

        [Column("REGION_APPROVER_PF_NO", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? RegionApproverPfNo { get; set; }

        [Column("REGION_APPROVER_NAME", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? RegionApproverName { get; set; }

        [Column("REGION_CHECKER_NAME", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? RegionCheckerName { get; set; }

        [Column("REGION_APPROVER_RECOMMENDATION", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? RegionApproverRecommendation { get; set; }

        [Column("REGION_APPROVER_REMARKS", TypeName = "NCLOB")]
        public string? RegionApproverRemarks { get; set; }
        [Column("REGION_APPROVER_DATE", TypeName = "DATE")]
        public DateTime? RegionApproverDate { get; set; }

        //[Column("ZONE_RECOMMENDATION", TypeName = "NVARCHAR2(100)")]
        //[MaxLength(100)]
        //public string? ZoneRecommendation { get; set; }

        //// NCLOB
        //[Column("ZONE_REMARKS", TypeName = "NCLOB")]
        //public string? ZoneRemarks { get; set; }

        //[Column("ZONE_STATUS_DATE", TypeName = "DATE")]
        //public DateTime? ZoneStatusDate { get; set; }

        //[Column("ZONE_APPROVED_QUANTITY")]
        //public long? ZoneApprovedQuantity { get; set; }

        //[Column("ZONE_FORWARD_TO", TypeName = "NVARCHAR2(100)")]
        //[MaxLength(100)]
        //public string? ZoneForwardTo { get; set; }

        //[Column("ZONE_APPROVER_PF_NO", TypeName = "NVARCHAR2(100)")]
        //[MaxLength(100)]
        //public string? ZoneApproverPfNo { get; set; }

        //[Column("ZONE_APPROVER_NAME", TypeName = "NVARCHAR2(100)")]
        //[MaxLength(100)]
        //public string? ZoneApproverName { get; set; }

        [Column("CO_RECOMMENDATION", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? CoRecommendation { get; set; }

        // NCLOB
        [Column("CO_REMARKS", TypeName = "NCLOB")]
        public string? CoRemarks { get; set; }

        [Column("CO_STATUS_DATE", TypeName = "DATE")]
        public DateTime? CoStatusDate { get; set; }


        [Column("CO_CHECKER_RECOMMENDATION", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? CoCheckerRecommendation { get; set; }

        // NCLOB
        [Column("CO_CHECKER_REMARKS", TypeName = "NCLOB")]
        public string? CoCheckerRemarks { get; set; }

        [Column("CO_APPROVER_DATE", TypeName = "DATE")]
        public DateTime? CoApproverDate { get; set; }

        [Column("CO_CHECKER_NAME", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? CoCheckerName { get; set; }

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

        [Column("IS_PO_ISSUED")]
        public bool? IsPoIssued { get; set; }

        [Column("PO_ISSUED_BY", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? PoIssuedBy { get; set; }
        [Column("PO_ISSUED_ON")]
        public DateTime? PoIssuedOn { get; set; }

        [Column("IS_PO_ACCEPTED")]
        public bool? IsPoAccepted { get; set; }

        [Column("PO_ACCEPTED_BY", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? PoAcceptedBy { get; set; }
        [Column("PO_ACCEPTED_ON")]
        public DateTime? PoAcceptedOn { get; set; }
        [Column("PO_ACCEPTED_DOC", TypeName = "BLOB")]
        public byte[]? PoAcceptedDoc { get; set; }

        [Column("PO_ACCEPTED_DOC_NAME", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? PoAcceptedDocName { get; set; }

        [Column("PO_REF_NO", TypeName = "NVARCHAR2(100)")]
        [MaxLength(100)]
        public string? PoRefno { get; set; }

        [Column("IS_DELETED")]
        public bool? ISDELETED { get; set; }

        [Column("DELETED_BY")]
        public string? DeletedBy { get; set; }

        [Column("DELETED_ON")]
        public DateTime? DeletedOn { get; set; }


        [Column("FEEDBACK", TypeName = "NVARCHAR2(255)")]
        [MaxLength(255)]
        public string? Feedback { get; set; }

        [Column("FEEDBACKBY", TypeName = "NVARCHAR2(10)")]
        [MaxLength(10)]
        public string? FeedbackBy { get; set; }
        [Column("FEEDBACKDATE")]
        public DateTime? FeedbackDate { get; set; }
        [NotMapped]
        public decimal? TotalProcurementCost { get; set; }
        [NotMapped]
        public int? TotalItemsCount { get; set; }
        [NotMapped]
        public int? PaidItemsCount { get; set; }

        [NotMapped]
        public bool? IsPaymentCompleted => TotalItemsCount > 0 && TotalItemsCount == PaidItemsCount;

        public ICollection<RequestATM>? RequestATM { get; set; }
    }
}
