using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_PROJECT")]
    public class ProjectsMaster
    {
        [Key]
        [Column("PROJECT_ID")]
        public int ProjectId { get; set; }

        [Column("PROJECT_CODE")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string? ProjectCode { get; set; }

        [Column("PROJECT_NAME")]
        public string? ProjectName { get; set; }

        [Column("PROJECT_BRIEF")]
        public string? ProjectBrief { get; set; }

        [Column("PROJECT_DESC")]
        public string? ProjectDesc { get; set; }

        [Column("PROJECT_NEED")]
        public string? ProjectNeed { get; set; }

        [Column("PROJECT_BENEFITS_BANK")]
        public string? ProjectBenefitsBank { get; set; }

        [Column("PROJECT_TYPE")]
        public string? ProjectType { get; set; }

        [Column("PROJECT_CATEGORY")]
        public string? ProjectCategory { get; set; }

        [Column("PROJECT_NATURE")]
        public string? ProjectNature { get; set; }

        [Column("IS_IT_BUSINESS_SAME")]
        [MaxLength(1)] // This prevents values longer than 1 character from being processed
        public string? IS_IT_BUSINESS_SAME { get; set; }



        // Ownership - Business
        public string? BUSINESS_OWNER_DEPT_CODE { get; set; }
        public string? BUSINESS_OWNER_DEPT_NAME { get; set; }

        public string? BUSINESS_OWNER_SEC_CODE { get; set; }
        public string? BUSINESS_OWNER_SEC_NAME { get; set; }

        public string? BUSINESS_OWNER_NAME { get; set; }
        public string? BUSINESS_OWNER_DESIG { get; set; }
        public string? BUSINESS_OWNER_IP_NO { get; set; }
        public string? BUSINESS_OWNER_MOBILE { get; set; }
        public string? BUSINESS_OWNER_EMAIL { get; set; }



        // Ownership - IT
        public string? IT_OWNER_DEPT_CODE { get; set; }

        public string? IT_OWNER_DEPT_NAME { get; set; }
        public string? IT_OWNER_SEC_CODE { get; set; }
        public string? IT_OWNER_SEC_NAME { get; set; }
        public string? IT_OWNER_NAME { get; set; }
        public string? IT_OWNER_DESIG { get; set; }
        public string? IT_OWNER_IP_NO { get; set; }
        public string? IT_OWNER_MOBILE { get; set; }
        public string? IT_OWNER_EMAIL { get; set; }
        [Column("DEPT_ID")]
        public long? DEPT_ID { get; set; }

        [Column("SECTION_ID")]
        public long? SECTION_ID { get; set; }

        //SUPPORTING_OWNER

        [Column("SUPPORTING_OWNER_DEPT_CODE")]
        public string? SupportingOwnerDeptCode { get; set; }

        [Column("SUPPORTING_OWNER_DEPT_NAME")]
        public string? SupportingOwnerDeptName { get; set; }

        [Column("SUPPORTING_OWNER_SEC_CODE")]
        public string? SupportingOwnerSecCode { get; set; }

        [Column("SUPPORTING_OWNER_SEC_NAME")]
        public string? SupportingOwnerSecName { get; set; }

        [Column("SUPPORTING_OWNER_NAME")]
        public string? SupportingOwnerName { get; set; }

        [Column("SUPPORTING_OWNER_DESIG")]
        public string? SupportingOwnerDesig { get; set; }

        [Column("SUPPORTING_OWNER_IP_NO")]
        public string? SupportingOwnerIpNo { get; set; }

        [Column("SUPPORTING_OWNER_MOBILE")]
        public string? SupportingOwnerMobile { get; set; }

        [Column("SUPPORTING_OWNER_EMAIL")]
        public string? SupportingOwnerEmail { get; set; }



        // Admin Approval Section
        [Column("ADMIN_APPROVAL_AUTHORITY")]
        public string? AdminApprovalAuthority { get; set; }

        [Column("ADMIN_APPROVAL_DATE")]
        public DateTime? AdminApprovalDate { get; set; }

        [Column("ADMIN_APPROVAL_FIELD1")]
        public string? AdminApprovalField1 { get; set; }

        [Column("ADMIN_APPROVAL_FIELD2")]
        public string? AdminApprovalField2 { get; set; }

        [Column("ADMIN_APPROVAL_FIELD3")]
        public string? AdminApprovalField3 { get; set; }



        // Project Financials & Status
        [Column("ESTIMATED_COST_PROJECT")]
        public decimal? EstimatedCostProject { get; set; }

        [Column("PROJECT_START_DATE")]
        public DateTime? ProjectStartDate { get; set; }

        [Column("PROJECT_END_DATE")]
        public DateTime? ProjectEndDate { get; set; }

        [Column("ACTUAL_START_DATE")]
        public DateTime? ActualStartDate { get; set; }

        [Column("ACTUAL_END_DATE")]
        public DateTime? ActualEndDate { get; set; }

        [Column("CURRENT_PHASE")]
        public string? CurrentPhase { get; set; }

        [Column("COMPLETE_PERC")]
        public decimal? CompletePerc { get; set; }

        [Column("OVERALL_STATUS")]
        public string? OverallStatus { get; set; }

        [Column("BASELINE_TIMELINE")]
        public DateTime? BaselineTimeline { get; set; }

        [Column("REVISED_TIMELINE")]
        public DateTime? RevisedTimeline { get; set; }

        [Column("LAST_REVIEW_DATE")]
        public DateTime? LastReviewDate { get; set; }

        [Column("REMARKS")]
        public string? Remarks { get; set; }





        // Procurement Section
        [Column("PROJECT_PROCURE_TYPE")]
        public string? ProjectProcureType { get; set; }

       

        [Column("RFP_REF_NO")]
        public string? RfpRefNo { get; set; }

        [Column("RFP_NAME")]
        public string? RfpName { get; set; }

        [Column("RFP_APPROVED_DATE")]
        public DateTime? RfpApprovedDate { get; set; }

        [Column("RFP_PUBLISH_DATE")]
        public DateTime? RfpPublishDate { get; set; }

        [Column("RFP_SUBMISSION_DATE")]
        public DateTime? RfpSubmissionDate { get; set; }

        [Column("PRE_BID_MEETING_DATE")]
        public DateTime? PreBidMeetingDate { get; set; }

        [Column("RFP_COMPLETION_DATE")]
        public DateTime? RfpCompletionDate { get; set; }

        [Column("RFP_STATUS")]
        public string? RfpStatus { get; set; }

        [Column("RFP_DOC")]
        public byte[]? RfpDoc { get; set; }

        // Finance and PO Section
        [Column("FIN_APPROVAL_AUTHORITY")]
        public string? FinApprovalAuthority { get; set; }

        [Column("FIN_APPROVAL_DATE")]
        public DateTime? FinApprovalDate { get; set; }

        [Column("FIN_APPROVAL_DOC")]
        public byte[]? FinApprovalDoc { get; set; }

        [Column("IS_ADDITIONAL_APPVAL_AVAILABLE")]
        public string? IsAdditionalAppvalAvailable { get; set; }

        [Column("IS_PO_ISSUED")]
        public string? IsPoIssued { get; set; }

        [Column("PO_DATE")]
        public DateTime? PoDate { get; set; }

        [Column("PO_DOC")]
        public byte[]? PoDoc { get; set; }

        [Column("PO_ACCEPTANCE_DATE")]
        public DateTime? PoAcceptanceDate { get; set; }

        [Column("PO_ACCEPTANCE_DOC")]
        public byte[]? PoAcceptanceDoc { get; set; }

        [Column("IS_ADDITIONAL_PO_ISSUED")]
        public string? IsAdditionalPoIssued { get; set; }

        // Service Provider (SP) Section
        [Column("SP_TYPE")]
        public string? SpType { get; set; }

        [Column("SP_NAME")]
        public string? SpName { get; set; }

        [Column("SP_ADDRESS")]
        public string? SpAddress { get; set; }

        [Column("SP_PM_NAME")]
        public string? SpPmName { get; set; }

        [Column("SP_PM_DESIG")]
        public string? SpPmDesig { get; set; }

        [Column("SP_PM_MOBILE")]
        public string? SpPmMobile { get; set; }

        [Column("SP_PM_EMAIL")]
        public string? SpPmEmail { get; set; }

        [Column("SP_SPOC_NAME")]
        public string? SpSpocName { get; set; }

        [Column("SP_SPOC_DESIG")]
        public string? SpSpocDesig { get; set; }

        [Column("SP_SPOC_MOBILE")]
        public string? SpSpocMobile { get; set; }

        [Column("SP_SPOC_EMAIL")]
        public string? SpSpocEmail { get; set; }

        [Column("PAN_NO")]
        public string? PanNo { get; set; }

        [Column("TIN_GST_NO")]
        public string? TinGstNo { get; set; }

        [Column("CIN_NO")]
        public string? CinNo { get; set; }

        [Column("DIN_NO")]
        public string? DinNo { get; set; }

        [Column("SLA_EXECUTION_DATE")]
        public DateTime? SlaExecutionDate { get; set; }

        [Column("SLA_EFFECTIVE_FROM")]
        public DateTime? SlaEffectiveFrom { get; set; }

        [Column("SLA_EFFECTIVE_UPTO")]
        public DateTime? SlaEffectiveUpto { get; set; }

        [Column("SLA_RENEWAL_DATE")]
        public DateTime? SlaRenewalDate { get; set; }

        [Column("SLA_LEGAL_VETTING")]
        public string? SlaLegalVetting { get; set; }

        [Column("SP_ONBOARDING_DATE")]
        public DateTime? SpOnboardingDate { get; set; }

        [Column("SP_AGREEMENT_DATE")]
        public DateTime? SpAgreementDate { get; set; }

        [Column("BUDGET_TYPE")]
        public string? BUDGET_TYPE { get; set; }

    }
}