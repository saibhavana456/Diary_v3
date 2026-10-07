// FOCUS 360: Financial Operational and Compliance Unit Snapshot
// Data source: BUSINESS_360_DATA × BUSINESS_360_PARAMETER_MASTER (join) + F360_CATEGORY_COLOR
// Values in BUSINESS_360_DATA are raw rupees; TOTAL-type params ÷ 10,000,000 → Crore.
using MyDiary.Core.Services;

namespace MyDiary.Web.Features.Focus360.Models;

/// <summary>
/// One row from BUSINESS_360_DATA joined with BUSINESS_360_PARAMETER_MASTER.
/// </summary>
public class GapPerformanceRow
{
    public int      ParameterId    { get; set; }
    public string   ParameterName  { get; set; } = string.Empty;  // MD_DISPLAY_NAME
    public string   Category       { get; set; } = string.Empty;
    public string   SubCategory    { get; set; } = string.Empty;
    public string   DataType       { get; set; } = "TOTAL";       // TOTAL / COUNT / PERCENTAGE
    public string   UiType         { get; set; } = "CARD";        // CHART / CARD
    public string   DateType       { get; set; } = "AS_ON_DATE";  // AS_ON_DATE / REPORT_DATE
    public int      SortOrder      { get; set; }

    public decimal? BaseLastFy     { get; set; }   // BASE_LAST_FY
    public decimal? BaseCurrentFy  { get; set; }   // BASE_CURRENT_FY
    public decimal? ActualAsOn     { get; set; }   // ACTUALS_AS_ON
    public decimal? Target         { get; set; }   // TARGET
    public decimal? TargetQuarter  { get; set; }
    public DateTime? AsOnDate      { get; set; }

    private decimal Div             => DataType switch
    {
        "TOTAL"    => 10_000_000m,
        "STANDARD" => 10_000_000m,
        "LAKHS"    => 100_000m,
        _          => 1m
    };
    public decimal? BaseLastFyCr    => BaseLastFy.HasValue    ? Math.Round(BaseLastFy.Value / Div, 2)    : null;
    public decimal? BaseCurrentFyCr => BaseCurrentFy.HasValue ? Math.Round(BaseCurrentFy.Value / Div, 2) : null;
    public decimal? ActualCr        => ActualAsOn.HasValue    ? Math.Round(ActualAsOn.Value / Div, 2)    : null;
    public decimal? TargetCr        => Target.HasValue        ? Math.Round(Target.Value / Div, 2)        : null;
    public decimal? Variance        => ActualCr.HasValue && TargetCr.HasValue && TargetCr != 0
                                        ? Math.Round(ActualCr.Value - TargetCr.Value, 2) : null;
    public decimal? VariancePct     => TargetCr.HasValue && TargetCr != 0
                                        ? Math.Round((Variance ?? 0) / TargetCr.Value * 100, 2) : null;
    public string   UnitLabel       => DataType switch
    {
        "TOTAL"      => "Cr",
        "LAKHS"      => "L",
        "PERCENTAGE" => "%",
        _            => "Cr"
    };
    public string ActualDisplay
    {
        get
        {
            // Locker Rent Overdue is maintained in Crores in the source data, so it
            // must be displayed in Crores (Cr) rather than as a raw COUNT value.
            // This mirrors Focus360.razor's SimpleValue (the UI path) exactly so the
            // Excel / CSV / PDF exports — which all render lockers via ActualDisplay —
            // show the same "X.XX Cr" as the on-screen table.
            if (ParameterName.Contains("RENT", StringComparison.OrdinalIgnoreCase)
                && ParameterName.Contains("OVERDUE", StringComparison.OrdinalIgnoreCase))
            {
                return ActualCr.HasValue ? $"{Math.Abs(ActualCr.Value):N2} Cr" : "—";
            }

            return DataType switch
            {
                "TOTAL"      => ActualCr?.ToString("N2") ?? "—",
                "LAKHS"      => ActualCr?.ToString("N2") ?? "—",
                "PERCENTAGE" => ActualAsOn.HasValue ? $"{ActualAsOn.Value:F2}%" : "—",
                _            => ActualAsOn?.ToString("N0") ?? "—"
            };
        }
    }
}

public class F360CategoryColor
{
    public string Category    { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public string ColorHex    { get; set; } = "#37474F";
    public string SectionLabel{ get; set; } = string.Empty;
    public int    SortOrder   { get; set; }
}

public class GapBranchSummary
{
    public string       BranchId              { get; set; }
    public string    BranchCode            { get; set; } = string.Empty;
    public string    BranchName            { get; set; } = string.Empty;
    public string    ZoneName              { get; set; } = string.Empty;
    public string    RegionName            { get; set; } = string.Empty;
    public string    ZMBMName              { get; set; } = string.Empty;
    public string    ZMBMCode              { get; set; } = string.Empty;
    public string    Scale                 { get; set; } = string.Empty;

    public DateTime? WorkingSince { get; set; }
    public DateTime? BranchOpenDate        { get; set; }
    public string License                   { get; set; } = string.Empty;

    //public DateTime? Lease { get; set; }
   
    public string    GuardianExec          { get; set; } = string.Empty;
    public TimeSpan? BranchOpenTime        { get; set; }
    public TimeSpan? BranchCloseTime       { get; set; }
    public string?   StaffMixSummary       { get; set; }
    public int?      AbbreviatedStaffTotal { get; set; }

    public string StrongKeys { get; set; } = string.Empty;

    public string CashSafeKeys { get; set; } = string.Empty;

    public string ATMRoomKeys { get; set; } = string.Empty;

    public string AlarmSystem { get; set; } = string.Empty;

    public string BranchTimeDisplay =>
        BranchOpenTime.HasValue && BranchCloseTime.HasValue
            ? $"{BranchOpenTime:hh\\:mm} – {BranchCloseTime:hh\\:mm}"
            : "—";
}

public class GapStaffDetail
{
    public string GradeCode { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public int    HeadCount { get; set; }
    public int    SortOrder { get; set; }
}

// ── Paired-row value objects ─────────────────────────────────────────────────

/// <summary>Loan Sanctions + Disbursements per segment (paired by param ID).</summary>
public record LoanActivityRow(
    string Segment,
    GapPerformanceRow? SanctionedRow,
    GapPerformanceRow? DisbursedRow)
{
    public string Sanctioned => SanctionedRow?.ActualDisplay ?? "—";
    public string Disbursed  => DisbursedRow?.ActualDisplay  ?? "—";
    public bool   HasData    => SanctionedRow?.ActualAsOn > 0 || DisbursedRow?.ActualAsOn > 0;
}

/// <summary>Deposit account activity per deposit type (paired by param ID).</summary>
public record DepositActivityRow(
    string Segment,
    GapPerformanceRow? AccountsRow,
    GapPerformanceRow? AmountRow)
{
    public string Accounts => AccountsRow?.ActualDisplay ?? "—";
    public string Amount   => AmountRow?.ActualDisplay   ?? "—";
    public bool   HasData  => AccountsRow?.ActualAsOn > 0 || AmountRow?.ActualAsOn > 0;
}

/// <summary>Digital Banking — Registered + Eligible per product.</summary>
public record DigitalBankingRow(
    string Product,
    GapPerformanceRow? RegisteredRow,
    GapPerformanceRow? EligibleRow)
{
    public string Registered => RegisteredRow?.ActualDisplay ?? "—";
    public string Eligible   => EligibleRow?.ActualDisplay   ?? "—";
}

/// <summary>JanSamarth — 5 columns per scheme (Total / Sanctioned / Disbursed / Rejected / Pending).</summary>
public record JanSamarthRow(
    string Scheme,
    GapPerformanceRow? TotalRow,
    GapPerformanceRow? SanctionedRow,
    GapPerformanceRow? DisbursedRow,
    GapPerformanceRow? RejectedRow,
    GapPerformanceRow? PendingRow)
{
    public string Total      => TotalRow?.ActualDisplay      ?? "—";
    public string Sanctioned => SanctionedRow?.ActualDisplay ?? "—";
    public string Disbursed  => DisbursedRow?.ActualDisplay  ?? "—";
    public string Rejected   => RejectedRow?.ActualDisplay   ?? "—";
    public string Pending    => PendingRow?.ActualDisplay    ?? "—";
    public bool   HasData    => TotalRow?.ActualAsOn > 0 || SanctionedRow?.ActualAsOn > 0
                             || DisbursedRow?.ActualAsOn > 0 || PendingRow?.ActualAsOn > 0;
}

// ── Main ViewModel ───────────────────────────────────────────────────────────

public class GapReportViewModel
{
    public GapBranchSummary        Branch              { get; set; } = new();
    public List<GapStaffDetail>    Staff               { get; set; } = [];
    public int                     TotalStaff          { get; set; }
    public decimal                 PerEmployeeBusiness { get; set; }

    /// <summary>All rows from BUSINESS_360_DATA × BUSINESS_360_PARAMETER_MASTER for this branch.</summary>
    public List<GapPerformanceRow> AllData             { get; set; } = [];

    public string    FinancialYear { get; set; } = string.Empty;
    public DateTime  GeneratedAt   { get; set; } = AppTime.Now;
    public DateTime? AsOnDate      { get; set; }
    public List<F360CategoryColor> CategoryColors { get; set; } = [];

    // ── Lookup ────────────────────────────────────────────────────────────────
    public GapPerformanceRow? ById(int id) =>
        AllData.FirstOrDefault(p => p.ParameterId == id);

    // ── 1. Performance pivot table ────────────────────────────────────────────
    // Shows: TOTAL_BUSINESS, DEPOSITS, ADVANCES, PRIORITY SECTOR
    // ASSET QUALITY (NPA + SMA) shown separately — no duplication.
    // REPORT_DATE params never in this table.
    public List<GapPerformanceRow> Performance => AllData
        .Where(p => p.DateType != "REPORT_DATE" &&
                    p.Category is "DEPOSITS" or "ADVANCES"
                                or "PRIORITY SECTOR" or "TOTAL_BUSINESS")
        .OrderBy(p => p.SortOrder).ToList();

    // ── 2. Asset Quality (dedicated section, not repeated in performance) ─────
    public List<GapPerformanceRow> NpaRows => By("ASSET QUALITY", "GROSS_NPA");
    public List<GapPerformanceRow> SmaRows => By("ASSET QUALITY", "STRESS");

    

    // ── 3. Business activity (REPORT_DATE) ────────────────────────────────────
    // Loan Sanctions & Disbursements — paired by segment
    public List<LoanActivityRow> LoanActivity =>
    [
        new("Total",  ById(70), ById(71)),
        new("Retail", ById(72), ById(73)),
        new("MSME",   ById(74), ById(75)),
        new("Agri",   ById(76), ById(77)),
    ];

    // Deposit Account Activity — paired by deposit type
    public List<DepositActivityRow> DepositActivity =>
    [
        new("Total Deposits", ById(78), ById(79)),
        new("SB Accounts",    ById(80), ById(81)),
        new("CA Accounts",    ById(82), ById(83)),
        new("Term Deposits",  ById(84), ById(85)),
    ];

    // ── 4. Operations ─────────────────────────────────────────────────────────
    public List<GapPerformanceRow> IncomeRows  => By("OPERATIONS", "OPERATIONS");

    /// <summary>
    /// Locker rows with Occupied Lockers calculated dynamically as (Total - Vacant).
    /// </summary>
    public List<GapPerformanceRow> LockerRows
    {
        get
        {
            var rows = By("OPERATIONS", "LOCKERS");
            var totalRow = rows.FirstOrDefault(r =>
                r.ParameterName.Contains("TOTAL", StringComparison.OrdinalIgnoreCase)
                && !r.ParameterName.Contains("RENT", StringComparison.OrdinalIgnoreCase));
            var vacantRow = rows.FirstOrDefault(r =>
                r.ParameterName.Contains("VACANT", StringComparison.OrdinalIgnoreCase));
            var occupiedRow = rows.FirstOrDefault(r =>
                r.ParameterName.Contains("OCCUPIED", StringComparison.OrdinalIgnoreCase));

            if (occupiedRow != null && totalRow?.ActualAsOn != null && vacantRow?.ActualAsOn != null)
            {
                occupiedRow.ActualAsOn = totalRow.ActualAsOn.Value - vacantRow.ActualAsOn.Value;
            }

            return rows;
        }
    }
    public List<GapPerformanceRow> ChannelRows => AllData
        .Where(p => p.Category == "OPERATIONS" && p.SubCategory is "CHANNELS" or "CASH")
        .Concat(ByCat("PENDING_POSITION"))
        .OrderBy(p => p.SortOrder).ToList();

    // ── 5. Digital Banking (paired: Registered + Eligible) ────────────────────
    public List<DigitalBankingRow> DigitalBankingTable =>
    [
        new("Mobile Banking",        ById(43), ById(55)),
        new("Internet Banking",      ById(45), ById(57)),
        new("Debit Card",            ById(44), ById(56)),
        new("Any One Digi Facility", ById(46), ById(58)),
    ];

    // ── 6. Digital Loans ──────────────────────────────────────────────────────
    /// <summary>
    /// Digital Loan rows. Use GetDigitalLoans(isBranchLevel) to get values in
    /// Lakhs (branch) or Crores (ZO/RO).
    /// </summary>
    public List<GapPerformanceRow> DigitalLoans => ByCat("DIGITAL_BUSINESS");

    /// <summary>
    /// Returns Digital Loan rows with values in Lakhs (for branch) or Crores (for ZO/RO).
    /// </summary>
    public List<GapPerformanceRow> GetDigitalLoans(bool isBranchLevel)
    {
        var rows = ByCat("DIGITAL_BUSINESS");
        if (isBranchLevel)
        {
            // At branch level, values are in lakhs range — convert using ÷1L instead of ÷1Cr
            foreach (var row in rows.Where(r => r.DataType == "TOTAL"))
            {
                row.DataType = "LAKHS";
            }
        }
        return rows;
    }

    // ── 7. Financial Inclusion ────────────────────────────────────────────────
    public List<GapPerformanceRow> FinancialInclusion => ByCat("FINANCIAL_INCLUSION");

    // ── 8. Third Party Products ───────────────────────────────────────────────
    public List<GapPerformanceRow> ThirdParty => ByCat("THIRD_PARTY_PRODUCTS");

    // ── 9. JanSamarth (5 columns per scheme) ─────────────────────────────────
    // Param ID mapping:
    //  scheme:  (Total  , Sanctioned, Disbursed, Rejected, Pending)
    //  URTS:    (1001   , 1002      , 1003     , 1004    , 135)
    //  PMMY:    (1005   , 1006      , 1007     , 1008    , 136)
    //  ECLGS:   (1009   , 1010      , 1011     , 1012    , 137)
    //  ACABS:   (1013   , 1014      , 1015     , 1016    , 138)
    //  CCME:    (1017   , 1018      , 1019     , 1020    , 139)
    //  KCC:     (1021   , 1022      , 1023     , 1024    , 140)
    //  WMS:     (1025   , 1026      , 1027     , 1028    , 141)
    //  NRLM:    (1029   , 1030      , 1031     , 1032    , 142)
    //  HL-U:    (1033   , 1034      , 1035     , 1036    , 143)
    //  NAMASTE: (1037   , 1038      , 1039     , 1040    , 144)
    //  KCC-OTH: (1041   , 1042      , 1043     , 1044    , 145)
    //  AIF:     (1045   , 1046      , 1047     , 1048    , 146)
    //  START:   (1049   , 1050      , 1051     , 1052    , 147)
    //  E-KUN:   (1053   , 1054      , 1055     , 1056    , 148)
    //  SHGL:    (1057   , 1058      , 1059     , 1060    , 149)
    public List<JanSamarthRow> JanSamarthTable =>
    [
        new("URTS",       ById(1001),ById(1002),ById(1003),ById(1004),ById(135)),
        new("PMMY",       ById(1005),ById(1006),ById(1007),ById(1008),ById(136)),
        new("ECLGS",      ById(1009),ById(1010),ById(1011),ById(1012),ById(137)),
        new("ACABS",      ById(1013),ById(1014),ById(1015),ById(1016),ById(138)),
        new("CCME",       ById(1017),ById(1018),ById(1019),ById(1020),ById(139)),
        new("KCC",        ById(1021),ById(1022),ById(1023),ById(1024),ById(140)),
        new("WMS",        ById(1025),ById(1026),ById(1027),ById(1028),ById(141)),
        new("NRLM",       ById(1029),ById(1030),ById(1031),ById(1032),ById(142)),
        new("HL-U",       ById(1033),ById(1034),ById(1035),ById(1036),ById(143)),
        new("NAMASTE",    ById(1037),ById(1038),ById(1039),ById(1040),ById(144)),
        new("KCC-OTHERS", ById(1041),ById(1042),ById(1043),ById(1044),ById(145)),
        new("AIF",        ById(1045),ById(1046),ById(1047),ById(1048),ById(146)),
        new("START",      ById(1049),ById(1050),ById(1051),ById(1052),ById(147)),
        new("E-KUN",      ById(1053),ById(1054),ById(1055),ById(1056),ById(148)),
        new("SHGL",       ById(1057),ById(1058),ById(1059),ById(1060),ById(149)),
    ];

    // ── Colour helpers ────────────────────────────────────────────────────────
    public string GetCategoryColor(string category) =>
        CategoryColors.FirstOrDefault(c =>
            string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(c.SubCategory))?.ColorHex
        ?? _defaultColors.GetValueOrDefault(category.ToUpperInvariant(), "#37474F");

    // ── Private helpers ───────────────────────────────────────────────────────
    private List<GapPerformanceRow> ByCat(string cat) =>
        AllData.Where(p => string.Equals(p.Category, cat, StringComparison.OrdinalIgnoreCase))
               .OrderBy(p => p.SortOrder).ToList();

    private List<GapPerformanceRow> By(string cat, string sub) =>
        AllData.Where(p => string.Equals(p.Category, cat, StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(p.SubCategory, sub, StringComparison.OrdinalIgnoreCase))
               .OrderBy(p => p.SortOrder).ToList();

    private static readonly Dictionary<string, string> _defaultColors = new()
    {
        ["DEPOSITS"]            = "#1565C0",
        ["ADVANCES"]            = "#2E7D32",
        ["ASSET QUALITY"]       = "#B71C1C",
        ["PRIORITY SECTOR"]     = "#E65100",
        ["TOTAL_BUSINESS"]      = "#004D40",
        ["OPERATIONS"]          = "#37474F",
        ["DIGITAL_BUSINESS"]    = "#1A237E",
        ["FINANCIAL_INCLUSION"] = "#33691E",
        ["THIRD_PARTY_PRODUCTS"]= "#4A148C",
        ["DIGITAL_BANKING"]     = "#006064",
        ["JANSAMARTH"]          = "#880E4F",
        ["PENDING_POSITION"]    = "#3E2723",
    };
}

public class GapBranchListItem
{
    public int    BranchId   { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public string ZoneName   { get; set; } = string.Empty;
    public string ZMBMName   { get; set; } = string.Empty;
}

public class GapZoneListItem
{
    public int ZoneId { get; set; }
    public string ZoneCode { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;   
}

public class GapRegionListItem
{
    public int RegionId { get; set; }
    public string RegionCode { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;    
    //public string ZoneName { get; set; } = string.Empty;    
}
