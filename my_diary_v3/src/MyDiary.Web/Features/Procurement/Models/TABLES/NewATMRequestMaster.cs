using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.Procurement.Models.TABLES
{
    public class NewATMRequestMaster
    {
        [Column("SR_NO")]
        public int Sno { get; set; }

        [Key]
        [Column("REQUESTID")]
        [MaxLength(100)]
        public string? RequestId { get; set; }

        [Column("BRANCH_CODE")]
        [MaxLength(100)]
        public string? BranchCode { get; set; }

        [Column("BRANCH_NAME")]
        [MaxLength(100)]
        public string? BranchName { get; set; }

        [Column("REGION")]
        [MaxLength(100)]
        public string? Region { get; set; }

        [Column("FINACLE_ID")]
        [MaxLength(100)]
        public string? FinacleId { get; set; }

        [Column("ITEM_CATEGORY")]
        [MaxLength(100)]
        public string? ItemCategory { get; set; }

        [Column("RAISED_BY")]
        [MaxLength(100)]
        public string? RaisedBy { get; set; }

        [Column("RAISED_DATE")]
        public DateTime? RaisedDate { get; set; }

        [Column("PROPOSED_ATM_CENTER")]
        [MaxLength(100)]
        public string? ProposedAtmCenter { get; set; }

        [Column("LINK_BRANCH_IP")]
        [MaxLength(100)]
        public string? LinkBranchIp { get; set; }

        [Column("POPULATION_CATEGORY")]
        [MaxLength(100)]
        public string? PopulationCategory { get; set; }

        [Column("ATM_SITE_ADDRESS")]
        [MaxLength(500)]
        public string? AtmSiteAddress { get; set; }

        [Column("DISTANCE_FROM_BRANCH")]
        [MaxLength(100)]
        public string? DistanceFromBranch { get; set; }

        [Column("ATM_COUNT_1KM")]
        public int? AtmCount1Km { get; set; }

        [Column("JUSTIFICATION")]
        public string Justification { get; set; }

        [Column("RO_MAKER_RECOMMENDATION")]
        [MaxLength(100)]
        public string? RoMakerRecommendation { get; set; }
         
        [Column("RO_MAKER_NAME")]
        [MaxLength(200)]
        public string? RoMakerName { get; set; }

        [Column("BUSINESS_MIX_PARENT_BRANCH")]
        [MaxLength(100)]
        public string? BusinessMixParentBranch { get; set; }

        [Column("CASA_ACCOUNTS")]
        public int? CasaAccounts { get; set; }

        [Column("DEBIT_CARDS_ISSUED")]
        public int? DebitCardsIssued { get; set; }

        [Column("CASA_BUSINESS_VOLUME")]
        public int? CasaBusinessVolume { get; set; }

        [Column("AVG_CASH_WITHDRAWALS")]
        public int? AvgCashWithdrawals { get; set; }

        [Column("EXPECTED_ATM_TRANSACTIONS")]
        public int? ExpectedAtmTransactions { get; set; }

        [Column("PREMISES_ACQUIRED")]
        public bool? PremisesAcquired { get; set; }

        [Column("EXPECTED_RENT")]
        [MaxLength(100)]
        public string? ExpectedRent { get; set; }

        [Column("NEW_SB_ACCOUNTS_EXPECTED")]
        public int? NewSbAccountsExpected { get; set; }

        [Column("EXPECTED_ATM_CARDS")]
        public int? ExpectedAtmCards { get; set; }

        [Column("NEARBY_BANK_ATMS")]
        public int? NearbyBankAtms { get; set; }

        [Column("AVG_HITS")]
        public int? AvgHits { get; set; }

        [Column("VISIBILITY_PARKING_SPACE")]
        [MaxLength(100)]
        public string? VisibilityParkingSpace { get; set; }

        [Column("SALARY_ACCOUNTS")]
        public int? SalaryAccounts { get; set; }

        [Column("WORKING_HOURS")]
        public int? WorkingHours { get; set; }

        [Column("RO_MAKER_FORWARD_TO")]
        [MaxLength(100)]
        public string? RoMakerForwardTo { get; set; }

        [Column("RO_MAKER_MODIFIED_DATE")]
        public DateTime? RoMakerModifiedDate { get; set; }

        [Column("CHECKER_RECOMMENDATION")]
        [MaxLength(100)]
        public string? CheckerRecommendation { get; set; }

        [Column("CHECKER_REMARKS")]
        [MaxLength(100)]
        public string? CheckerRemarks { get; set; }

        [Column("CHECKER_FORWARD_TO")]
        [MaxLength(100)]
        public string? CheckerForwardTo { get; set; }

        [Column("CHECKER_NAME")]
        [MaxLength(100)]
        public string? CheckerName { get; set; }

        [Column("CHECKER_MODIFIED_DATE")]
        public DateTime? CheckerModifiedDate { get; set; }

        [Column("RO_CHECKER_RECOMMENDATION")]
        [MaxLength(100)]
        public string? RoCheckerRecommendation { get; set; }

        [Column("RO_CHECKER_REMARKS")]
        [MaxLength(100)]
        public string? RoCheckerRemarks { get; set; }

        [Column("RO_CHECKER_FORWARD_TO")]
        [MaxLength(100)]
        public string? RoCheckerForwardTo { get; set; }

        [Column("RO_CHECKER_NAME")]
        [MaxLength(100)]
        public string? RoCheckerName { get; set; }

        [Column("RO_CHECKER_MODIFIED_DATE")]
        public DateTime? RoCheckerModifiedDate { get; set; }

        [Column("ZO_RECOMMENDATION")]
        [MaxLength(100)]
        public string? ZoRecommendation { get; set; }

        [Column("ZO_REMARKS")]
        [MaxLength(100)]
        public string? ZoRemarks { get; set; }

        [Column("ZO_FORWARD_TO")]
        [MaxLength(100)]
        public string? ZoForwardTo { get; set; }

        [Column("ZO_CHECKER_NAME")]
        [MaxLength(100)]
        public string? ZoCheckerName { get; set; }

        [Column("ZO_MODIFIED_DATE")]
        public DateTime? ZoModifiedDate { get; set; }

        [Column("CO_RECOMMENDATION")]
        [MaxLength(100)]
        public string? CoRecommendation { get; set; }

        [Column("CO_REMARKS")]
        [MaxLength(100)]
        public string? CoRemarks { get; set; }

        [Column("CO_APPROVER_NAME")]
        [MaxLength(100)]
        public string? CoApproverName { get; set; }

        [Column("CO_MODIFIED_DATE")]
        public DateTime? CoModifiedDate { get; set; }
    }
}
