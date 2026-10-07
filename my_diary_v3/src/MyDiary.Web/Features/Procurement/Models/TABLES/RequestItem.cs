using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.Procurement.Models.TABLES
{
    [Table("REQUEST_ITEM")]
    public class RequestItem
    {
        [Key]
        [Column("SR_NO")]
        public int SR_NO { get; set; }

        [Column("REQUEST_CODE")]
        [MaxLength(100)]
        public string? RequestCode { get; set; }

        [Column("ITEM_CODE")]
        [MaxLength(100)]
        public string? ItemCode { get; set; }

        [Column("QUANTITY")]
        public int? Quantity { get; set; }

        [Column("COST_BEFORE_TAX")]
        public decimal? CostBeforeTax { get; set; }

        [Column("COST_AFTER_TAX")]
        public decimal? CostAfterTax { get; set; }

        [Column("REMARKS", TypeName = "NCLOB")]
        public string? Remarks { get; set; }

        [Column("IS_PO_ISSUED")]
        public bool? IsPOIssued { get; set; }

        [Column("PO_ISSUED_BY")]
        [MaxLength(100)]
        public string? POIssuedBy { get; set; }

        [Column("PO_ISSUED_ON")]
        public DateTime? POIssuedOn { get; set; }

        [Column("IS_PO_ACCEPTED")]
        public bool? IsPoAccepted { get; set; }

        [MaxLength(100)]
        [Column("PO_ACCEPTED_BY")]
        public string? PoAcceptedBy { get; set; }

        [Column("PO_ACCEPTED_ON")]
        public DateTime? PoAcceptedOn { get; set; }

        // --- Ordering & Delivery ---
        [MaxLength(100)]
        [Column("ORDERED_BY")]
        public string? OrderedBy { get; set; }

        [Column("ORDERED_ON")]
        public DateTime? OrderedOn { get; set; }

        [Column("PRE_BOOKED_AMT")]
        public decimal? PreBookedAmt { get; set; }

        [MaxLength(100)]
        [Column("RECEIVED_BY")]
        public string? ReceivedBy { get; set; }

        [Column("DELIVERED_ON")]
        public DateTime? DeliveredOn { get; set; }

        [Column("POST_DELIVERED_AMT")]
        public decimal? PostDeliveredAmt { get; set; }

        [Column("INSTALLED_ON")]
        public DateTime? InstalledOn { get; set; }

        [Column("POST_INSTALLED_AMT")]
        public decimal? PostInstalledAmt { get; set; }

       
        [Column("IS_DELETED")]
        public bool? IsDeleted { get; set; } 

        [MaxLength(100)]
        [Column("DELETED_BY")]
        public string? DeletedBy { get; set; }

        [Column("DELETED_ON")]
        public DateTime? DeletedOn { get; set; }

        [ForeignKey("RequestCode")]
        public virtual ItemRequestMaster? RequestMaster { get; set; }

    }
}
