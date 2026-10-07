using MyDiary.Web.Features.Business.Models;

public class ChargePartBViewModel
{
    public string OfficerName { get; set; }
    public string EmployeeNo { get; set; }
    public string Designation { get; set; }
    public string BranchName { get; set; } = "";
    public string RegionName { get; set; } = "";
    public string ZoneName { get; set; } = "";


    public string? FirstKeyHolderName { get; set; }
    public string? SecondKeyHolderName { get; set; }

    public string? CustomerComplaintsPdfFileName { get; set; }
    public string? CustomerComplaintsPdfContentType { get; set; }
    public byte[]? CustomerComplaintsPdfData { get; set; }

    public string? ConsumerCasesPdfFileName { get; set; }
    public string? ConsumerCasesPdfContentType { get; set; }
    public byte[]? ConsumerCasesPdfData { get; set; }

    public string? TimeBarredAccountsPdfFileName { get; set; }
    public string? TimeBarredAccountsPdfContentType { get; set; }
    public byte[]? TimeBarredAccountsPdfData { get; set; }

    public string? CapitalAssetsPdfFileName { get; set; }
    public string? CapitalAssetsPdfContentType { get; set; }
    public byte[]? CapitalAssetsPdfData { get; set; }

    public string? SundryBalancesPdfFileName { get; set; }
    public string? SundryBalancesPdfContentType { get; set; }
    public byte[]? SundryBalancesPdfData { get; set; }

    public string? GodownKeysPdfFileName { get; set; }
    public string? GodownKeysPdfContentType { get; set; }
    public byte[]? GodownKeysPdfData { get; set; }

    public string? GoldDiscrepanciesPdfFileName { get; set; }
    public string? GoldDiscrepanciesPdfContentType { get; set; }
    public byte[]? GoldDiscrepanciesPdfData { get; set; }


    public DateTime? DateReportedForDuty { get; set; }
    public DateTime? DateChargeTaken { get; set; }
    public DateTime? LastInspectionDate { get; set; }
    public List<PartBDocumentVm> Documents { get; set; } = new();
    public List<PartBComplaintVm> CustomerComplaints { get; set; } = new();
    public bool SafeCustodyReceiptAvailable { get; set; }
    public string FirstKeyHolder { get; set; }
    public string SecondKeyHolder { get; set; }
    public string DuplicateKeysLodgedAt { get; set; }
    public DateTime? DuplicateKeysLodgedDate { get; set; }
    public List<PartBComplaintVm> ConsumerCases { get; set; } = new();
    public List<PartBAccountVm> TimeBarredAccounts { get; set; } = new();
    public List<PartBCapitalAssetVm> CapitalAssets { get; set; } = new();
    public List<PartBSundryVm> SundryBalances { get; set; } = new();
    public bool VehicleDocumentsInOrder { get; set; }
    public DateTime? InsuranceRenewalDueDate { get; set; }
    public List<PartBAccountVm> GodownKeysVerification { get; set; } = new();
    public bool GoldBagCountMatches { get; set; }
    public List<PartBAccountVm> GoldDiscrepancies { get; set; } = new();
    public int? RandomJLCheckedCount { get; set; }
    public List<PartBAccountVm> RandomJLDiscrepancies { get; set; } = new();
    public bool LockerKeysProperlyHeld { get; set; }
    public bool GunLicenseVerified { get; set; }
    public string GunDiscrepancies { get; set; }
    public bool CashTalliesWithRegister { get; set; }
    public bool PermanentFileUpdated { get; set; }
    public string OtherMatters { get; set; }
    public DateTime? DeclarationDate { get; set; }
    public string IncomingOfficerSignature { get; set; }

    public class PartBDocumentVm
    {
        public string DocumentName { get; set; }
        public bool IsAvailable { get; set; }
    }
}