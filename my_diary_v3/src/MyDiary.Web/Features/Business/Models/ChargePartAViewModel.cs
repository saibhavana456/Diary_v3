using System;
using System.Collections.Generic;

namespace MyDiary.Web.Features.Business.Models
{
    public class ChargePartAViewModel
    {
        public string OfficerName { get; set; } = "";
        public string EmployeeNo { get; set; } = "";
        public string Designation { get; set; } = "";


        public byte[]? AdhocPdfData { get; set; }
        public string? AdhocPdfFileName { get; set; }
        public string? AdhocPdfContentType { get; set; }

        public byte[]? ReviewPendingPdfData { get; set; }
        public string? ReviewPendingPdfFileName { get; set; }
        public string? ReviewPendingPdfContentType { get; set; }
        public byte[]? SeizedDocsPdfData { get; set; }
        public string? SeizedDocsPdfFileName { get; set; }
        public string? SeizedDocsPdfContentType { get; set; }


        public byte[]? ServiceChargePdfData { get; set; }
        public string? ServiceChargePdfFileName { get; set; }
        public string? ServiceChargePdfContentType { get; set; }


        public byte[]? KycPendingPdfData { get; set; }
        public string? KycPendingPdfFileName { get; set; }
        public string? KycPendingPdfContentType { get; set; }


        public byte[]? TimeBarredDebtsPdfData { get; set; }
        public string? TimeBarredDebtsPdfFileName { get; set; }
        public string? TimeBarredDebtsPdfContentType { get; set; }

        public string? IncomingOfficerName { get; set; }

        public string? FirstKeyHolderName { get; set; }
        public string? SecondKeyHolderName { get; set; }

        public string? RelievedByName { get; set; }

        public string BranchName { get; set; } = "";
        public string RegionName { get; set; } = "";
        public string ZoneName { get; set; } = "";

        public bool IsJointCustodian { get; set; }
        public DateTime? DeclarationDate { get; set; }
        public string? OutgoingOfficerSignature { get; set; }
        public DateTime? ChargeTakenDate { get; set; }
        public DateTime? RelievedDate { get; set; }
        public string? IncomingEmployeeNo { get; set; } = "";
        public string? RelievedBy { get; set; }
        public DateTime? LastInspectionDate { get; set; }
        public List<PartADocumentVm> Documents { get; set; } = new();
        public bool SafeCustodyReceiptAvailable { get; set; }
        public string? FirstKeyHolder { get; set; }
        public string? SecondKeyHolder { get; set; }
        public string? DuplicateKeysLodgedAt { get; set; }
        public DateTime? DuplicateKeysLodgedDate { get; set; }
        public List<AdhocAccountVm> AdhocAccounts { get; set; } = new();
        public List<AdhocAccountVm> ReviewPendingAccounts { get; set; } = new();
        public List<AdhocAccountVm> SeizedDocuments { get; set; } = new();
        public List<ServiceChargeConcessionVm> ServiceChargeConcessions { get; set; } = new();
        public string? KycPendingComments { get; set; }
        public string? LoanDocumentException { get; set; }
        public string? TimeBarredDebts { get; set; }
        public string? AuditPendingStatus { get; set; }
        public bool PermanentFileUpdated { get; set; }
        public string? OtherMatters { get; set; }
    }

    public class PartADocumentVm
    {
        public string DocumentName { get; set; } = "";
        public bool IsAvailable { get; set; }
    }

    public class AdhocAccountVm
    {
        public string? AccountNo { get; set; }
        public string? BorrowerName { get; set; }
        public string? Remarks { get; set; }
    }

    public class ServiceChargeConcessionVm
    {
        public string? PartyName { get; set; }
        public string? ConcessionDetails { get; set; }
        public string? Remarks { get; set; }
    }

}
