using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.Procurement.Models.TABLES
{
    [Table("REQUEST_ATM", Schema = "PRCRMNT")]
    public class RequestATM
    {
        [Key]
        [Column("SR_NO")]
        public int SR_NO { get; set; }

        [MaxLength(100)]
        [Column("REQUEST_CODE")]
        public string? RequestCode { get; set; }

        [MaxLength(100)]
        [Column("ATM_CODE")]
        public string? AtmCode { get; set; }

        [Column("QUANTITY")]
        public int? Quantity { get; set; }

        [Column("COST_BEFORE_TAX")]
        public decimal? CostBeforeTax { get; set; }

        [Column("APPROVED_QUANTITY")]
        public int? ApprovedQuantity { get; set; }

        [Column("REMARKS", TypeName = "NCLOB")]
        public string? Remarks { get; set; }


        [Column("IS_PO_ISSUED")]
        public bool? IsPOIssued { get; set; }

        [MaxLength(100)]
        [Column("PO_ISSUED_BY")]
        public string? POIssuedBy { get; set; }

        [Column("PO_ISSUED_ON")]
        public DateTime? POIssuedOn { get; set; }
        [Column("PO_GENERATED_DOC")]
        public byte[]? PoGeneratedDoc { get; set; }
        [Column("PO_GENERATED_DOC_NAME")]
        [MaxLength(100)]
        public string? PoGeneratedDocName { get; set; }


        [Column("IS_PO_ACCEPTED")]
        public bool? IsPoAccepted { get; set; }

        [MaxLength(100)]
        [Column("PO_ACCEPTED_BY")]
        public string? PoAcceptedBy { get; set; }

        [Column("PO_ACCEPTED_ON")]
        public DateTime? PoAcceptedOn { get; set; }
        [Column("PO_ACCEPTED_DOC")]
        public byte[]? PoAcceptedDoc { get; set; }
        [MaxLength(100)]
        [Column("PO_ACCEPTED_DOC_NAME")]
        public string? PoAcceptedDocName { get; set; }

        // --- Order & Installation Progress ---
        [MaxLength(100)]
        [Column("ORDERED_BY")]
        public string? OrderedBy { get; set; }

        [Column("ORDERED_ON")]
        public DateTime? OrderedOn { get; set; }

        [Column("PRE_BOOKED_AMT")]
        public decimal? PreBookedAmt { get; set; }
        [Column("PRE_BOOKED_DOC")]
        public byte[]? PreBookedDoc { get; set; }
        [Column("PRE_BOOKED_DOC_NAME")]
        [MaxLength(100)]
        public string? PreBookedDocName { get; set; }

        [MaxLength(100)]
        [Column("RECEIVED_BY")]
        public string? ReceivedBy { get; set; }

        [Column("DELIVERED_ON")]
        public DateTime? DeliveredOn { get; set; }

        [Column("POST_DELIVERED_AMT")]
        public decimal? PostDeliveredAmt { get; set; }
        [Column("ORDER_RECIVED_DOC")]
        public byte[]? OrderRecivedDoc { get; set; }
        [Column("ORDER_RECIVED_DOC_NAME")]
        [MaxLength(100)]
        public string? OrderRecivedDocName { get; set; }

        [Column("INSTALLED_ON")]
        public DateTime? InstalledOn { get; set; }

        [Column("POST_INSTALLED_AMT")]
        public decimal? PostInstalledAmt { get; set; }
        [Column("POST_INSTALLATION_DOC")]
        public byte[]? PostInstallationDoc { get; set; }
        [Column("POST_INSTALLATION_DOC_NAME")]
        [MaxLength(100)]
        public string? PostInstallationDocName { get; set; }

        //Payment
        [Column("PAYMENT_DATE")]
        public DateTime? PaymentDate { get; set; }

        [Column("INVOICE_NUMBER")]
        [MaxLength(100)]
        public string? InvoiceNumber { get; set; }

        [Column("PAYMENT_BY")]
        [MaxLength(100)]
        public string? PaymentBy { get; set; }

        [Column("INVOICE_DOC")]
        public byte[]? InvoiceDoc { get; set; }

        [Column("INVOICE_DOC_NAME")]
        [MaxLength(100)]
        public string? InvoiceDocName { get; set; }

        // --- Audit / Soft Delete ---

        [Column("IS_DELETED")]
        public bool? IsDeleted { get; set; }

        [MaxLength(100)]
        [Column("DELETED_BY")]
        public string? DeletedBy { get; set; }

        [Column("DELETED_ON")]
        public DateTime? DeletedOn { get; set; }

        // Navigation Property back to the ItemRequestMaster
        [ForeignKey("RequestCode")]
        public virtual ATMRequestMaster? RequestMaster { get; set; }
    }
}
