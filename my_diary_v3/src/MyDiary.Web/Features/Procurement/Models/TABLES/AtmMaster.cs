using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.Procurement.Models.TABLES
{
    [Table("ATM_MASTER", Schema = "PRCRMNT")]
    public class AtmMaster
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("SR_NO")]
        public long SrNo { get; set; } // NUMBER(18,0)

        [Required]
        [StringLength(200)]
        [Column("ATM_ID")]
        public string AtmId { get; set; }

        [Required]
        [StringLength(100)]
        [Column("COL_1")]
        public string Col1 { get; set; }

        [StringLength(10)]
        [Column("COL_2")]
        public string? Col2 { get; set; }

        [Column("COL_3")]
        public decimal? Col3 { get; set; } // NUMBER(18,2)

        [StringLength(100)]
        [Column("TERMINAL_ID_8_DIGIT")]
        public string? TerminalId8Digit { get; set; }

        [StringLength(200)]
        [Column("PROJECT")]
        public string? Project { get; set; }

        [Column("INSTALLATION_DATE")]
        public DateTime? InstallationDate { get; set; }

        [StringLength(100)]
        [Column("ATM_MAKE")]
        public string? AtmMake { get; set; }



        [StringLength(100)]
        [Column("ATM_CRM")]
        public string? AtmCrm { get; set; }

        [StringLength(100)]
        [Column("CAPEX_OPEX")]
        public string? CapexOpex { get; set; }

        [StringLength(100)]
        [Column("CAPEX_OPEX_ONLY")]
        public string? CapexOpexOnly { get; set; }

        [StringLength(100)]
        [Column("SOL_ID")]
        public string? SolId { get; set; }

        [StringLength(100)]
        [Column("BRANCH_CODE")]
        public string? BranchCode { get; set; }

        [StringLength(200)]
        [Column("BRANCH_NAME")]
        public string? BranchName { get; set; }

        [Column("TIER")]
        public decimal? Tier { get; set; }

        [StringLength(100)]
        [Column("OFFSITE_ONSITE")]
        public string? OffsiteOnsite { get; set; }

        [StringLength(100)]
        [Column("POPULATION_CATEGORY")]
        public string? PopulationCategory { get; set; }

        [StringLength(1000)]
        [Column("ADDRESS")]
        public string? Address { get; set; }

        [StringLength(200)]
        [Column("DISTRICT")]
        public string? District { get; set; }

        [StringLength(200)]
        [Column("STATE")]
        public string? State { get; set; }

        [StringLength(200)]
        [Column("GEOGRAPHY_WISE_REGION")]
        public string? GeographyWiseRegion { get; set; }

        [StringLength(100)]
        [Column("REGION_CODE")]
        public string? RegionCode { get; set; }

        [StringLength(200)]
        [Column("REGION_DESC")]
        public string? RegionDesc { get; set; }

        [StringLength(100)]
        [Column("ZONE_CODE")]
        public string? ZoneCode { get; set; }

        [StringLength(200)]
        [Column("ZONE_DESC")]
        public string? ZoneDesc { get; set; }

        [StringLength(200)]
        [Column("RBI_ISSUED_DEPARTMENT")]
        public string? RbiIssuedDepartment { get; set; }

        [StringLength(100)]
        [Column("MSP_FINAL")]
        public string? MspFinal { get; set; }

        [StringLength(100)]
        [Column("CASH_VENDOR")]
        public string? CashVendor { get; set; }

        [Column("AVG_UPTIME")]
        public decimal? AvgUptime { get; set; }

        [Column("AVG_HITS")]
        public decimal? AvgHits { get; set; }
    }
}
