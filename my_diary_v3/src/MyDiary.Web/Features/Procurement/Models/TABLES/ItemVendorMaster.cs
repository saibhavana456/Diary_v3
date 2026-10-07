using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.Procurement.Models.TABLES
{
    [Table("ITEM_VENDOR_MASTER", Schema = "PRCRMNT")]
    public class ItemVendorMaster
    {
        [Key]
        [Column("SR_NO")]
        public long SrNo { get; set; }

        [Required]
        [Column("ITEM_CODE")]
        [MaxLength(100)]
        public string? ItemCode { get; set; } 

        [Column("ITEM_NAME")]
        [MaxLength(200)]
        public string? ItemName { get; set; }

        [Column("ITEM_CATEGORY")]
        [MaxLength(100)]
        public string? ItemCategory { get; set; }

        [Column("ITEM_COST", TypeName = "NUMBER(18,2)")]
        public decimal? ItemCost { get; set; }

        [Column("ITEM_GST", TypeName = "NUMBER(18,2)")]
        public decimal? ItemGST { get; set; }

        [Column("SPECIFICATION")]
        public string? Specification { get; set; }   

        [Column("VENDOR_NAME")]
        [MaxLength(200)]
        public string? VendorName { get; set; }

        [Column("VENDOR_GST")]
        [MaxLength(50)]
        public string? VendorGst { get; set; }

        [Column("PIN_CODE")]
        [MaxLength(20)]
        public string? PinCode { get; set; }

        [Column("BUDGET_HEAD")]
        [MaxLength(100)]
        public string? BudgetHead { get; set; }

        [Column("IS_DELETED")]
        public bool? IsDeleted { get; set; } 
    }

}
