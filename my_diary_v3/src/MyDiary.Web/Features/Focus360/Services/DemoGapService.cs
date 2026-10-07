//using MyDiary.Web.Features.Focus360.Models;

//namespace MyDiary.Web.Features.Focus360.Services;

///// <summary>
///// Demo implementation of IGapService.
///// Seed values are derived from real Oracle sample data (branch 56680, business_360_data_56680.xlsx)
///// and scaled per demo branch for variety.
/////
///// Data column mapping (BUSINESS_360_DATA):
/////   BASE_LAST_FY     → Previous FY end value (raw rupees)
/////   BASE_CURRENT_FY  → Current FY opening balance
/////   ACTUALS_AS_ON    → Current actual as on date
/////   TARGET           → Annual target
/////   TARGET_QUARTER   → Quarterly target
///// TOTAL-type params are divided by 10,000,000 → displayed in Crore.
///// </summary>
//public sealed class DemoGapService : IGapService
//{
//    // ── Branch master ──────────────────────────────────────────────────────────
//    private static readonly List<GapBranchListItem> _branches =
//    [
//        new() { BranchId=56680, BranchCode="MUM001", BranchName="Fort Main Branch",       ZoneName="Mumbai Zone",     RegionName="South Mumbai",  ZMBMName="Rajesh Kumar"  },
//        new() { BranchId=56681, BranchCode="MUM002", BranchName="Bandra West Branch",     ZoneName="Mumbai Zone",     RegionName="West Mumbai",   ZMBMName="Priya Sharma"  },
//        new() { BranchId=56682, BranchCode="MUM003", BranchName="Andheri East Branch",    ZoneName="Mumbai Zone",     RegionName="North Mumbai",  ZMBMName="Vikram Patel"  },
//        new() { BranchId=56683, BranchCode="PNE001", BranchName="Pune Main Branch",       ZoneName="Pune Zone",       RegionName="Central Pune",  ZMBMName="Anita Desai"   },
//        new() { BranchId=56684, BranchCode="PNE002", BranchName="Koregaon Park Branch",   ZoneName="Pune Zone",       RegionName="East Pune",     ZMBMName="Sanjay Mehta"  },
//        new() { BranchId=56685, BranchCode="NGP001", BranchName="Nagpur Central Branch",  ZoneName="Vidarbha Zone",   RegionName="Nagpur",        ZMBMName="Deepak Joshi"  },
//        new() { BranchId=56686, BranchCode="AUR001", BranchName="Aurangabad Main Branch", ZoneName="Marathwada Zone", RegionName="Aurangabad",    ZMBMName="Kavita Rao"    },
//        new() { BranchId=56687, BranchCode="NAS001", BranchName="Nashik Road Branch",     ZoneName="North Zone",      RegionName="Nashik",        ZMBMName="Ramesh Gupta"  },
//    ];

//    private static GapBranchSummary BranchSummary(int branchId)
//    {
//        var b = _branches.FirstOrDefault(x => x.BranchId == branchId)
//                ?? _branches[0];
//        return new()
//        {
//            BranchId   = b.BranchId,   BranchCode  = b.BranchCode,
//            BranchName = b.BranchName, ZoneName     = b.ZoneName,
//            RegionName = b.RegionName, ZMBMName     = b.ZMBMName,
//            ZMBMCode   = $"{b.ZMBMName[..2].ToUpper()}{b.BranchId % 1000:D3}",
//            Scale      = branchId % 3 == 0 ? "E-IV" : "E-III",
//            BranchOpenDate  = new DateTime(2005 + (branchId % 15), 4, 1),
//            WorkingSince    = new DateTime(2021 + (branchId % 4), 6, 1),
//            GuardianExec    = "B.P. Singh",
//            BranchOpenTime  = new TimeSpan(9, 30, 0),
//            BranchCloseTime = new TimeSpan(17, 30, 0),
//            StaffMixSummary = "DGM-1/AGM-2/CM-3/SM-4/OF-3/CL-2",
//            AbbreviatedStaffTotal = 15,
//        };
//    }

//    private static List<GapStaffDetail> Staff(int branchId) =>
//    [
//        new(){GradeCode="CGM",GradeName="Chief General Manager",   HeadCount=0, SortOrder=1},
//        new(){GradeCode="GM", GradeName="General Manager",         HeadCount=0, SortOrder=2},
//        new(){GradeCode="DGM",GradeName="Deputy General Manager",  HeadCount=1, SortOrder=3},
//        new(){GradeCode="AGM",GradeName="Assistant General Manager",HeadCount=2, SortOrder=4},
//        new(){GradeCode="CM", GradeName="Chief Manager",           HeadCount=3, SortOrder=5},
//        new(){GradeCode="SM", GradeName="Senior Manager",          HeadCount=4, SortOrder=6},
//        new(){GradeCode="MGR",GradeName="Manager",                 HeadCount=2, SortOrder=7},
//        new(){GradeCode="AM", GradeName="Assistant Manager",       HeadCount=1, SortOrder=8},
//        new(){GradeCode="CSA",GradeName="Clerical",                HeadCount=2, SortOrder=9},
//        new(){GradeCode="SS", GradeName="Sub-Staff",               HeadCount=1, SortOrder=10},
//    ];

//    // ── Seed scale factor (branch variety) ───────────────────────────────────
//    private static decimal F(int branchId) => branchId switch
//    {
//        56680 => 1.0m, 56681 => 1.4m, 56682 => 1.1m, 56683 => 1.8m,
//        56684 => 0.7m, 56685 => 0.9m, 56686 => 0.5m, _      => 0.6m
//    };

//    /// <summary>
//    /// Builds a GapPerformanceRow from raw rupee values (same structure as BUSINESS_360_DATA).
//    /// Pass 0 for missing values (null stored as 0 in demo).
//    /// </summary>
//    private static GapPerformanceRow R(
//        int id, string cat, string sub, string name, string dtype, string utype,
//        decimal baseLastFy, decimal baseCurrFy, decimal actual, decimal target,
//        int sort, decimal f, string dateType = "AS_ON_DATE") => new()
//    {
//        ParameterId   = id,
//        ParameterName = name,
//        Category      = cat,
//        SubCategory   = sub,
//        DataType      = dtype,
//        UiType        = utype,
//        DateType      = dateType,
//        SortOrder     = sort,
//        BaseLastFy    = baseLastFy   * f,
//        BaseCurrentFy = baseCurrFy   * f,
//        ActualAsOn    = actual       * f,
//        Target        = target       * f,
//        AsOnDate      = DateTime.Today,
//    };

//    private static List<GapPerformanceRow> AllData(int branchId)
//    {
//        decimal f = F(branchId);
//        // Raw rupee values from branch 56680 (business_360_data_56680.xlsx) as seed.
//        // Multiply by f for branch variety. TOTAL-type values are in rupees; /10Mn → Cr at display.
//        return
//        [
//            // ── TOTAL BUSINESS ────────────────────────────────────────────────
//            R(51,"TOTAL_BUSINESS","TOTAL_BUSINESS","TOTAL BUSINESS (DOMESTIC)","TOTAL","CHART",
//              0,15_069_925_836m,13_559_551_796m,0, 10, f),

//            // ── DEPOSITS ─────────────────────────────────────────────────────
//            R(1,"DEPOSITS","TOTAL_DEPOSITS","TOTAL DEPOSITS","TOTAL","CHART",
//              10_762_009_844m,10_613_882_861m,10_296_161_514m,9_418_810_972m, 110, f),
//            R(2,"DEPOSITS","CASA_DEPOSITS","CASA DEPOSITS","TOTAL","CHART",
//              1_445_581_982m,2_295_011_042m,1_703_683_449m,951_393_595m, 120, f),
//            R(3,"DEPOSITS","CASA_DEPOSITS","SAVINGS DEPOSITS","TOTAL","CARD",
//              611_363_860m,652_745_970m,1_425_589_062m,657_264_673m, 121, f),
//            R(6,"DEPOSITS","CASA_DEPOSITS","CURRENT DEPOSITS","TOTAL","CARD",
//              834_218_122m,1_642_265_071m,278_094_387m,294_128_922m, 122, f),
//            R(4,"DEPOSITS","TERM_DEPOSITS","RETAIL TD","TOTAL","CARD",
//              2_159_064_072m,1_843_100_629m,2_015_706_374m,1_991_646_187m, 130, f),
//            R(5,"DEPOSITS","TERM_DEPOSITS","TOTAL TERM DEPOSITS","TOTAL","CHART",
//              9_316_427_862m,8_318_871_820m,8_592_478_065m,8_467_417_378m, 140, f),

//            // Deposit activity (REPORT_DATE)
//            R(78,"DEPOSITS","TOTAL_DEPOSITS","DEPOSIT ACCOUNTS OPENED (No.)","COUNT","CARD",
//              0,0,27m,0, 150, f, "REPORT_DATE"),
//            R(79,"DEPOSITS","TOTAL_DEPOSITS","DEPOSIT AMOUNT OPENED (Cr)","TOTAL","CARD",
//              0,0,41_769_000m,0, 151, f, "REPORT_DATE"),
//            R(80,"DEPOSITS","CASA_DEPOSITS","SBA ACCOUNTS OPENED (No.)","COUNT","CARD",
//              0,0,0m,0, 152, f, "REPORT_DATE"),
//            R(81,"DEPOSITS","CASA_DEPOSITS","SBA OPENED AMOUNT (Cr)","TOTAL","CARD",
//              0,0,0m,0, 153, f, "REPORT_DATE"),
//            R(82,"DEPOSITS","CASA_DEPOSITS","CA ACCOUNTS OPENED (No.)","COUNT","CARD",
//              0,0,0m,0, 154, f, "REPORT_DATE"),
//            R(84,"DEPOSITS","CASA_DEPOSITS","TD ACCOUNTS OPENED (No.)","COUNT","CARD",
//              0,0,27m,0, 155, f, "REPORT_DATE"),
//            R(85,"DEPOSITS","CASA_DEPOSITS","TD OPENED AMOUNT (Cr)","TOTAL","CARD",
//              0,0,41_769_000m,0, 156, f, "REPORT_DATE"),

//            // ── ADVANCES ─────────────────────────────────────────────────────
//            R(50,"ADVANCES","TOTAL_ADVANCES","TOTAL ADVANCES","TOTAL","CHART",
//              0,4_491_763_685m,3_310_398_017m,0, 210, f),
//            R(7,"ADVANCES","TOTAL_ADVANCES","TOTAL ADVANCES (STANDARD)","TOTAL","CHART",
//              3_469_789_705m,4_456_042_975m,3_263_390_282m,5_482_826_366m, 220, f),
//            R(8,"ADVANCES","MSME_ADVANCES","MSME ADVANCES (STANDARD)","TOTAL","CHART",
//              666_045_473m,2_567_296_489m,2_566_782_714m,3_444_128_187m, 230, f),
//            R(9,"ADVANCES","MSME_ADVANCES","MICRO ADVANCES (STANDARD)","TOTAL","CARD",
//              229_206_613m,1_536_381_405m,1_465_163_785m,1_893_021_790m, 231, f),
//            R(10,"ADVANCES","MSME_ADVANCES","SMALL ADVANCES (STANDARD)","TOTAL","CARD",
//              0,366_409_297m,422_342_489m,0, 232, f),
//            R(11,"ADVANCES","MSME_ADVANCES","MEDIUM ADVANCES (STANDARD)","TOTAL","CARD",
//              204_453_960m,664_505_787m,679_276_440m,1_012_180_865m, 233, f),
//            R(13,"ADVANCES","AGRI_ADVANCES","AGRI ADVANCES (STANDARD)","TOTAL","CHART",
//              9_430_075m,31_903_644m,31_579_884m,37_192_027m, 240, f),
//            R(14,"ADVANCES","AGRI_ADVANCES","AGRI ADVANCES (TOTAL)","TOTAL","CARD",
//              0,40_893_913m,40_570_153m,0, 241, f),
//            R(15,"ADVANCES","AGRI_ADVANCES","SMALL MARGINAL FARMERS (STD)","TOTAL","CARD",
//              0,124_858m,117_111m,0, 242, f),
//            R(16,"ADVANCES","RETAIL_ADVANCES","TOTAL RETAIL (STANDARD)","TOTAL","CHART",
//              331_145_783m,323_213_174m,323_680_771m,341_193_774m, 250, f),
//            R(17,"ADVANCES","RETAIL_ADVANCES","UNION MILES (STANDARD)","TOTAL","CARD",
//              16_270_313m,24_607_214m,32_370_411m,26_433_780m, 251, f),
//            R(18,"ADVANCES","RETAIL_ADVANCES","UNION MORTGAGE (STANDARD)","TOTAL","CARD",
//              66_820_018m,63_469_610m,61_828_168m,67_560_602m, 252, f),
//            R(19,"ADVANCES","RETAIL_ADVANCES","UNION HOME (STANDARD)","TOTAL","CARD",
//              197_446_434m,185_894_272m,181_297_674m,198_906_893m, 253, f),
//            R(86,"ADVANCES","RETAIL_ADVANCES","UNION EDUCATION (STD)","TOTAL","CARD",
//              44_163_824m,43_637_467m,42_974_172m,45_552_866m, 254, f),
//            R(87,"ADVANCES","RETAIL_ADVANCES","UNION PERSONAL (STD)","TOTAL","CARD",
//              5_421_183m,2_462_448m,2_086_575m,2_693_709m, 255, f),
//            R(21,"ADVANCES","CORPORATE_ADVANCES","STANDARD CORPORATE ADVANCES","TOTAL","CHART",
//              2_307_752_917m,1_407_892_971m,190_956_376m,1_521_932_302m, 260, f),
//            R(20,"ADVANCES","PRIORITY SECTOR ADVANCES","TOTAL PRIORITY (STANDARD)","TOTAL","CARD",
//              0,2_618_962_387m,2_617_165_718m,0, 270, f),

//            // Sanctions / Disbursements (REPORT_DATE)
//            R(70,"ADVANCES","TOTAL_ADVANCES","TOTAL LOANS SANCTIONED (No.)","COUNT","CARD",
//              0,0,0,0, 310, f, "REPORT_DATE"),
//            R(71,"ADVANCES","TOTAL_ADVANCES","TOTAL LOANS DISBURSED (Cr)","TOTAL","CARD",
//              0,0,74_985_735m,0, 311, f, "REPORT_DATE"),
//            R(72,"ADVANCES","RETAIL_ADVANCES","RETAIL LOANS SANCTIONED (No.)","COUNT","CARD",
//              0,0,0,0, 312, f, "REPORT_DATE"),
//            R(73,"ADVANCES","RETAIL_ADVANCES","RETAIL LOANS DISBURSED (Cr)","TOTAL","CARD",
//              0,0,0,0, 313, f, "REPORT_DATE"),
//            R(74,"ADVANCES","MSME_ADVANCES","MSME LOANS SANCTIONED (No.)","COUNT","CARD",
//              0,0,0,0, 314, f, "REPORT_DATE"),
//            R(75,"ADVANCES","MSME_ADVANCES","MSME LOANS DISBURSED (Cr)","TOTAL","CARD",
//              0,0,72_427_948m,0, 315, f, "REPORT_DATE"),
//            R(76,"ADVANCES","AGRI_ADVANCES","AGRI LOANS SANCTIONED (No.)","COUNT","CARD",
//              0,0,0,0, 316, f, "REPORT_DATE"),
//            R(77,"ADVANCES","AGRI_ADVANCES","AGRI LOANS DISBURSED (Cr)","TOTAL","CARD",
//              0,0,71_606m,0, 317, f, "REPORT_DATE"),

//            // ── ASSET QUALITY ─────────────────────────────────────────────────
//            R(22,"ASSET QUALITY","GROSS_NPA","GROSS NPA","TOTAL","CHART",
//              0,35_725_136m,47_007_574m,0, 410, f),
//            R(23,"ASSET QUALITY","GROSS_NPA","GROSS NPA — MSME","TOTAL","CARD",
//              0,16_552_750m,27_824_417m,0, 411, f),
//            R(24,"ASSET QUALITY","GROSS_NPA","GROSS NPA — AGRI","TOTAL","CARD",
//              0,8_990_270m,8_990_270m,0, 412, f),
//            R(25,"ASSET QUALITY","GROSS_NPA","GROSS NPA — RETAIL","TOTAL","CARD",
//              0,8_531_114m,8_539_813m,0, 413, f),
//            R(26,"ASSET QUALITY","GROSS_NPA","GROSS NPA — CORPORATE","TOTAL","CARD",
//              0,0,0,0, 414, f),
//            // SMA
//            R(129,"ASSET QUALITY","STRESS","SMA-0 AMOUNT","TOTAL","CARD",
//              0,42_000_000m,38_500_000m,0, 420, f),
//            R(130,"ASSET QUALITY","STRESS","SMA-1 AMOUNT","TOTAL","CARD",
//              0,28_000_000m,25_000_000m,0, 421, f),
//            R(131,"ASSET QUALITY","STRESS","SMA-2 AMOUNT","TOTAL","CARD",
//              0,19_000_000m,17_500_000m,0, 422, f),
//            R(132,"ASSET QUALITY","STRESS","SMA-0 % OF ADVANCES","PERCENTAGE","CARD",
//              0,0.40m,0.37m,0, 423, 1m),
//            R(133,"ASSET QUALITY","STRESS","SMA-1 % OF ADVANCES","PERCENTAGE","CARD",
//              0,0.27m,0.24m,0, 424, 1m),
//            R(134,"ASSET QUALITY","STRESS","SMA-2 % OF ADVANCES","PERCENTAGE","CARD",
//              0,0.18m,0.17m,0, 425, 1m),

//            // ── PRIORITY SECTOR ───────────────────────────────────────────────
//            R(117,"PRIORITY SECTOR","PSL TOTAL","PSL TOTAL","TOTAL","CHART",
//              0,2_618_962_387m,2_617_165_718m,0, 510, f),
//            R(118,"PRIORITY SECTOR","AGRI DIRECT","AGRI DIRECT","TOTAL","CHART",
//              0,31_903_644m,31_579_884m,37_192_027m, 511, f),
//            R(119,"PRIORITY SECTOR","MSME","MSME (PSL)","TOTAL","CHART",
//              0,2_567_296_489m,2_566_782_714m,3_444_128_187m, 512, f),
//            R(120,"PRIORITY SECTOR","WEAKER SECTION","WEAKER SECTION","TOTAL","CHART",
//              0,280_000_000m,265_000_000m,300_000_000m, 513, f),

//            // ── OPERATIONS ───────────────────────────────────────────────────
//            R(28,"OPERATIONS","OPERATIONS","INTEREST INCOME","TOTAL","CARD",
//              0,0,48_047_732m,0, 610, f),
//            R(29,"OPERATIONS","OPERATIONS","FEE INCOME","TOTAL","CARD",
//              0,0,3_567_230m,0, 611, f),
//            R(30,"OPERATIONS","OPERATIONS","EXPENSE","TOTAL","CARD",
//              0,0,-43_479_038m,0, 612, f),
//            // Lockers
//            R(59,"OPERATIONS","LOCKERS","TOTAL LOCKERS","COUNT","CARD",
//              0,0,641m,0, 620, 1m),
//            R(60,"OPERATIONS","LOCKERS","OCCUPIED LOCKERS","COUNT","CARD",
//              0,0,449m,0, 621, 1m),
//            R(65,"OPERATIONS","LOCKERS","VACANT LOCKERS","COUNT","CARD",
//              0,0,192m,0, 622, 1m),
//            R(68,"OPERATIONS","LOCKERS","LOCKER RENT OVERDUE (₹)","COUNT","CARD",
//              0,0,343_660m,0, 623, 1m),
//            // Channels / ATMs
//            R(61,"OPERATIONS","CHANNELS","TOTAL ATMs","COUNT","CARD",
//              0,0,2m,0, 630, 1m),
//            R(62,"OPERATIONS","CHANNELS","LIVE ATMs","COUNT","CARD",
//              0,0,2m,0, 631, 1m),
//            R(63,"OPERATIONS","CASH","ATM CASH POSITION (₹)","COUNT","CARD",
//              0,0,1_646_400m,0, 632, 1m),

//            // ── PENDING POSITION ─────────────────────────────────────────────
//            R(66,"PENDING_POSITION","KYC","C-KYC PENDING","COUNT","CARD",
//              0,0,1_250m,0, 710, 1m),
//            R(67,"PENDING_POSITION","KYC","RE-KYC PENDING","COUNT","CARD",
//              0,0,10_779m,0, 711, 1m),

//            // ── DIGITAL BANKING ──────────────────────────────────────────────
//            R(43,"DIGITAL_BANKING","REGISTRATION","MOBILE BANKING REGISTERED","COUNT","CARD",
//              0,0,18_450m,0, 810, f),
//            R(55,"DIGITAL_BANKING","REGISTRATION","MOBILE BANKING ELIGIBLE","COUNT","CARD",
//              0,0,22_100m,0, 811, f),
//            R(45,"DIGITAL_BANKING","REGISTRATION","INTERNET BANKING REGISTERED","COUNT","CARD",
//              0,0,12_380m,0, 812, f),
//            R(57,"DIGITAL_BANKING","REGISTRATION","INTERNET BANKING ELIGIBLE","COUNT","CARD",
//              0,0,19_700m,0, 813, f),
//            R(44,"DIGITAL_BANKING","REGISTRATION","DEBIT CARD REGISTERED","COUNT","CARD",
//              0,0,29_640m,0, 814, f),
//            R(56,"DIGITAL_BANKING","REGISTRATION","DEBIT CARD ELIGIBLE","COUNT","CARD",
//              0,0,32_100m,0, 815, f),
//            R(46,"DIGITAL_BANKING","USAGE","ANY ONE DIGI FACILITY REG","COUNT","CARD",
//              0,0,31_200m,0, 816, f),
//            R(58,"DIGITAL_BANKING","USAGE","ANY ONE DIGI FACILITY ELIGIBLE","COUNT","CARD",
//              0,0,35_800m,0, 817, f),

//            // ── DIGITAL BUSINESS (Digital Loans) ─────────────────────────────
//            R(31,"DIGITAL_BUSINESS","DIGITAL_LOANS","SHISHU MUDRA DIGITAL","TOTAL","CARD",
//              0,0,0,0, 910, f),
//            R(32,"DIGITAL_BUSINESS","DIGITAL_LOANS","MUDRA TARUN KISHORE DIGITAL","TOTAL","CARD",
//              0,0,0,0, 911, f),
//            R(33,"DIGITAL_BUSINESS","DIGITAL_LOANS","NARI SHAKTI DIGITAL","TOTAL","CARD",
//              0,0,0,0, 912, f),
//            R(34,"DIGITAL_BUSINESS","DIGITAL_LOANS","UNION EDUCATION DIGITAL","TOTAL","CARD",
//              0,0,0,0, 913, f),
//            R(123,"DIGITAL_BUSINESS","DIGITAL_LOANS","FRESH KCC UPTO 1.60L DIGITAL","TOTAL","CARD",
//              0,0,0,0, 914, f),
//            R(124,"DIGITAL_BUSINESS","DIGITAL_LOANS","GOLD LOAN DIGITAL","TOTAL","CARD",
//              0,0,0,0, 915, f),
//            R(125,"DIGITAL_BUSINESS","DIGITAL_LOANS","GST GAIN DIGITAL","TOTAL","CARD",
//              0,0,0,0, 916, f),
//            R(126,"DIGITAL_BUSINESS","DIGITAL_LOANS","LOAN AGAINST DEPOSIT DIGITAL","TOTAL","CARD",
//              0,0,0,0, 917, f),
//            R(127,"DIGITAL_BUSINESS","DIGITAL_LOANS","PAPL DIGITAL LOAN","TOTAL","CARD",
//              0,0,0,0, 918, f),
//            R(128,"DIGITAL_BUSINESS","DIGITAL_LOANS","UNION CASH DIGITAL","TOTAL","CARD",
//              0,0,0,0, 919, f),

//            // ── FINANCIAL INCLUSION ──────────────────────────────────────────
//            R(35,"FINANCIAL_INCLUSION","SCHEMES","PMJDY","COUNT","CARD",
//              0,0,0,0, 1010, f),
//            R(36,"FINANCIAL_INCLUSION","SCHEMES","PMJJBY","COUNT","CARD",
//              0,0,0,0, 1011, f),
//            R(37,"FINANCIAL_INCLUSION","SCHEMES","PMSBY","COUNT","CARD",
//              0,0,0,0, 1012, f),
//            R(38,"FINANCIAL_INCLUSION","SCHEMES","APY","COUNT","CARD",
//              0,0,5m,0, 1013, 1m),
//            R(121,"FINANCIAL_INCLUSION","SCHEMES","PM SVANIDHI","COUNT","CARD",
//              0,0,0,0, 1014, f),
//            R(122,"FINANCIAL_INCLUSION","SCHEMES","PM VISHWAKARMA YOJANA","COUNT","CARD",
//              0,0,0,0, 1015, f),

//            // ── THIRD PARTY PRODUCTS ─────────────────────────────────────────
//            R(39,"THIRD_PARTY_PRODUCTS","INSURANCE","GENERAL INSURANCE","TOTAL","CARD",
//              0,0,0,0, 1110, f),
//            R(40,"THIRD_PARTY_PRODUCTS","INSURANCE","HEALTH INSURANCE","TOTAL","CARD",
//              0,0,0,0, 1111, f),
//            R(41,"THIRD_PARTY_PRODUCTS","INSURANCE","LIFE INSURANCE","TOTAL","CARD",
//              0,0,0,0, 1112, f),
//            R(42,"THIRD_PARTY_PRODUCTS","INVESTMENTS","MUTUAL FUNDS","TOTAL","CARD",
//              0,0,0,0, 1113, f),

//            // ── JANSAMARTH — 5 cols per scheme: Total / Sanctioned / Disbursed / Rejected / Pending ──
//            // Pending = existing params 135-149 (from BUSINESS_360_PARAMETER_MASTER)
//            // Total/Sanctioned/Disbursed/Rejected = new params 1001-1060 (see F360_JANSAMARTH_PARAMS.sql)
//            // Format: R(id, cat, sub, name, dtype, utype, baseLast, baseCurr, actual, target, sort, scale)
//            // --- URTS ---
//            R(1001,"JANSAMARTH","URTS","URTS TOTAL",     "COUNT","CARD",0,0, 45m,0,1201,1m),
//            R(1002,"JANSAMARTH","URTS","URTS SANCTIONED","COUNT","CARD",0,0, 38m,0,1202,1m),
//            R(1003,"JANSAMARTH","URTS","URTS DISBURSED", "COUNT","CARD",0,0, 30m,0,1203,1m),
//            R(1004,"JANSAMARTH","URTS","URTS REJECTED",  "COUNT","CARD",0,0,  3m,0,1204,1m),
//            R(135, "JANSAMARTH","URTS","URTS PENDING",   "COUNT","CARD",0,0, 12m,0,1205,1m),
//            // --- PMMY ---
//            R(1005,"JANSAMARTH","PMMY","PMMY TOTAL",     "COUNT","CARD",0,0, 28m,0,1211,1m),
//            R(1006,"JANSAMARTH","PMMY","PMMY SANCTIONED","COUNT","CARD",0,0, 22m,0,1212,1m),
//            R(1007,"JANSAMARTH","PMMY","PMMY DISBURSED", "COUNT","CARD",0,0, 18m,0,1213,1m),
//            R(1008,"JANSAMARTH","PMMY","PMMY REJECTED",  "COUNT","CARD",0,0,  2m,0,1214,1m),
//            R(136, "JANSAMARTH","PMMY","PMMY PENDING",   "COUNT","CARD",0,0,  8m,0,1215,1m),
//            // --- ECLGS ---
//            R(1009,"JANSAMARTH","ECLGS","ECLGS TOTAL",     "COUNT","CARD",0,0, 12m,0,1221,1m),
//            R(1010,"JANSAMARTH","ECLGS","ECLGS SANCTIONED","COUNT","CARD",0,0,  9m,0,1222,1m),
//            R(1011,"JANSAMARTH","ECLGS","ECLGS DISBURSED", "COUNT","CARD",0,0,  7m,0,1223,1m),
//            R(1012,"JANSAMARTH","ECLGS","ECLGS REJECTED",  "COUNT","CARD",0,0,  0m,0,1224,1m),
//            R(137, "JANSAMARTH","ECLGS","ECLGS PENDING",   "COUNT","CARD",0,0,  3m,0,1225,1m),
//            // --- ACABS ---
//            R(1013,"JANSAMARTH","ACABS","ACABS TOTAL",     "COUNT","CARD",0,0,  8m,0,1231,1m),
//            R(1014,"JANSAMARTH","ACABS","ACABS SANCTIONED","COUNT","CARD",0,0,  6m,0,1232,1m),
//            R(1015,"JANSAMARTH","ACABS","ACABS DISBURSED", "COUNT","CARD",0,0,  5m,0,1233,1m),
//            R(1016,"JANSAMARTH","ACABS","ACABS REJECTED",  "COUNT","CARD",0,0,  1m,0,1234,1m),
//            R(138, "JANSAMARTH","ACABS","ACABS PENDING",   "COUNT","CARD",0,0,  1m,0,1235,1m),
//            // --- CCME ---
//            R(1017,"JANSAMARTH","CCME","CCME TOTAL",     "COUNT","CARD",0,0,  5m,0,1241,1m),
//            R(1018,"JANSAMARTH","CCME","CCME SANCTIONED","COUNT","CARD",0,0,  4m,0,1242,1m),
//            R(1019,"JANSAMARTH","CCME","CCME DISBURSED", "COUNT","CARD",0,0,  4m,0,1243,1m),
//            R(1020,"JANSAMARTH","CCME","CCME REJECTED",  "COUNT","CARD",0,0,  1m,0,1244,1m),
//            R(139, "JANSAMARTH","CCME","CCME PENDING",   "COUNT","CARD",0,0,  0m,0,1245,1m),
//            // --- KCC ---
//            R(1021,"JANSAMARTH","KCC","KCC TOTAL",     "COUNT","CARD",0,0, 32m,0,1251,1m),
//            R(1022,"JANSAMARTH","KCC","KCC SANCTIONED","COUNT","CARD",0,0, 25m,0,1252,1m),
//            R(1023,"JANSAMARTH","KCC","KCC DISBURSED", "COUNT","CARD",0,0, 20m,0,1253,1m),
//            R(1024,"JANSAMARTH","KCC","KCC REJECTED",  "COUNT","CARD",0,0,  2m,0,1254,1m),
//            R(140, "JANSAMARTH","KCC","KCC PENDING",   "COUNT","CARD",0,0,  5m,0,1255,1m),
//            // --- WMS ---
//            R(1025,"JANSAMARTH","WMS","WMS TOTAL",     "COUNT","CARD",0,0, 15m,0,1261,1m),
//            R(1026,"JANSAMARTH","WMS","WMS SANCTIONED","COUNT","CARD",0,0, 12m,0,1262,1m),
//            R(1027,"JANSAMARTH","WMS","WMS DISBURSED", "COUNT","CARD",0,0, 10m,0,1263,1m),
//            R(1028,"JANSAMARTH","WMS","WMS REJECTED",  "COUNT","CARD",0,0,  1m,0,1264,1m),
//            R(141, "JANSAMARTH","WMS","WMS PENDING",   "COUNT","CARD",0,0,  2m,0,1265,1m),
//            // --- NRLM ---
//            R(1029,"JANSAMARTH","NRLM","NRLM TOTAL",     "COUNT","CARD",0,0,  6m,0,1271,1m),
//            R(1030,"JANSAMARTH","NRLM","NRLM SANCTIONED","COUNT","CARD",0,0,  5m,0,1272,1m),
//            R(1031,"JANSAMARTH","NRLM","NRLM DISBURSED", "COUNT","CARD",0,0,  5m,0,1273,1m),
//            R(1032,"JANSAMARTH","NRLM","NRLM REJECTED",  "COUNT","CARD",0,0,  1m,0,1274,1m),
//            R(142, "JANSAMARTH","NRLM","NRLM PENDING",   "COUNT","CARD",0,0,  0m,0,1275,1m),
//            // --- HL-U ---
//            R(1033,"JANSAMARTH","HL-U","HL-U TOTAL",     "COUNT","CARD",0,0, 18m,0,1281,1m),
//            R(1034,"JANSAMARTH","HL-U","HL-U SANCTIONED","COUNT","CARD",0,0, 14m,0,1282,1m),
//            R(1035,"JANSAMARTH","HL-U","HL-U DISBURSED", "COUNT","CARD",0,0, 10m,0,1283,1m),
//            R(1036,"JANSAMARTH","HL-U","HL-U REJECTED",  "COUNT","CARD",0,0,  0m,0,1284,1m),
//            R(143, "JANSAMARTH","HL-U","HL-U PENDING",   "COUNT","CARD",0,0,  4m,0,1285,1m),
//            // --- NAMASTE ---
//            R(1037,"JANSAMARTH","NAMASTE","NAMASTE TOTAL",     "COUNT","CARD",0,0,  4m,0,1291,1m),
//            R(1038,"JANSAMARTH","NAMASTE","NAMASTE SANCTIONED","COUNT","CARD",0,0,  3m,0,1292,1m),
//            R(1039,"JANSAMARTH","NAMASTE","NAMASTE DISBURSED", "COUNT","CARD",0,0,  2m,0,1293,1m),
//            R(1040,"JANSAMARTH","NAMASTE","NAMASTE REJECTED",  "COUNT","CARD",0,0,  0m,0,1294,1m),
//            R(144, "JANSAMARTH","NAMASTE","NAMASTE PENDING",   "COUNT","CARD",0,0,  1m,0,1295,1m),
//            // --- KCC-OTHERS ---
//            R(1041,"JANSAMARTH","KCC-OTHERS","KCC-OTHERS TOTAL",     "COUNT","CARD",0,0, 10m,0,1301,1m),
//            R(1042,"JANSAMARTH","KCC-OTHERS","KCC-OTHERS SANCTIONED","COUNT","CARD",0,0,  8m,0,1302,1m),
//            R(1043,"JANSAMARTH","KCC-OTHERS","KCC-OTHERS DISBURSED", "COUNT","CARD",0,0,  5m,0,1303,1m),
//            R(1044,"JANSAMARTH","KCC-OTHERS","KCC-OTHERS REJECTED",  "COUNT","CARD",0,0,  0m,0,1304,1m),
//            R(145, "JANSAMARTH","KCC-OTHERS","KCC-OTHERS PENDING",   "COUNT","CARD",0,0,  3m,0,1305,1m),
//            // --- AIF ---
//            R(1045,"JANSAMARTH","AIF","AIF TOTAL",     "COUNT","CARD",0,0,  3m,0,1311,1m),
//            R(1046,"JANSAMARTH","AIF","AIF SANCTIONED","COUNT","CARD",0,0,  3m,0,1312,1m),
//            R(1047,"JANSAMARTH","AIF","AIF DISBURSED", "COUNT","CARD",0,0,  3m,0,1313,1m),
//            R(1048,"JANSAMARTH","AIF","AIF REJECTED",  "COUNT","CARD",0,0,  0m,0,1314,1m),
//            R(146, "JANSAMARTH","AIF","AIF PENDING",   "COUNT","CARD",0,0,  0m,0,1315,1m),
//            // --- START ---
//            R(1049,"JANSAMARTH","START","START TOTAL",     "COUNT","CARD",0,0,  8m,0,1321,1m),
//            R(1050,"JANSAMARTH","START","START SANCTIONED","COUNT","CARD",0,0,  6m,0,1322,1m),
//            R(1051,"JANSAMARTH","START","START DISBURSED", "COUNT","CARD",0,0,  5m,0,1323,1m),
//            R(1052,"JANSAMARTH","START","START REJECTED",  "COUNT","CARD",0,0,  1m,0,1324,1m),
//            R(147, "JANSAMARTH","START","START PENDING",   "COUNT","CARD",0,0,  2m,0,1325,1m),
//            // --- E-KUN ---
//            R(1053,"JANSAMARTH","E-KUN","E-KUN TOTAL",     "COUNT","CARD",0,0,  5m,0,1331,1m),
//            R(1054,"JANSAMARTH","E-KUN","E-KUN SANCTIONED","COUNT","CARD",0,0,  4m,0,1332,1m),
//            R(1055,"JANSAMARTH","E-KUN","E-KUN DISBURSED", "COUNT","CARD",0,0,  3m,0,1333,1m),
//            R(1056,"JANSAMARTH","E-KUN","E-KUN REJECTED",  "COUNT","CARD",0,0,  0m,0,1334,1m),
//            R(148, "JANSAMARTH","E-KUN","E-KUN PENDING",   "COUNT","CARD",0,0,  1m,0,1335,1m),
//            // --- SHGL ---
//            R(1057,"JANSAMARTH","SHGL","SHGL TOTAL",     "COUNT","CARD",0,0,  2m,0,1341,1m),
//            R(1058,"JANSAMARTH","SHGL","SHGL SANCTIONED","COUNT","CARD",0,0,  2m,0,1342,1m),
//            R(1059,"JANSAMARTH","SHGL","SHGL DISBURSED", "COUNT","CARD",0,0,  2m,0,1343,1m),
//            R(1060,"JANSAMARTH","SHGL","SHGL REJECTED",  "COUNT","CARD",0,0,  0m,0,1344,1m),
//            R(149, "JANSAMARTH","SHGL","SHGL PENDING",   "COUNT","CARD",0,0,  0m,0,1345,1m),
//        ];
//    }

//    // ── Category colour defaults (loaded from seed SQL in prod) ───────────────
//    private static readonly List<F360CategoryColor> _colors =
//    [
//        new(){Category="DEPOSITS",           SubCategory="",         ColorHex="#1565C0",SectionLabel="Deposits",            SortOrder=1},
//        new(){Category="ADVANCES",           SubCategory="",         ColorHex="#2E7D32",SectionLabel="Advances",            SortOrder=2},
//        new(){Category="ASSET QUALITY",      SubCategory="GROSS_NPA",ColorHex="#B71C1C",SectionLabel="NPA / Asset Quality", SortOrder=3},
//        new(){Category="ASSET QUALITY",      SubCategory="STRESS",   ColorHex="#C62828",SectionLabel="SMA / Stress",        SortOrder=4},
//        new(){Category="PRIORITY SECTOR",    SubCategory="",         ColorHex="#E65100",SectionLabel="Priority Sector",     SortOrder=5},
//        new(){Category="TOTAL_BUSINESS",     SubCategory="",         ColorHex="#004D40",SectionLabel="Total Business",      SortOrder=6},
//        new(){Category="OPERATIONS",         SubCategory="OPERATIONS",ColorHex="#37474F",SectionLabel="Income / Expense",   SortOrder=7},
//        new(){Category="OPERATIONS",         SubCategory="LOCKERS",  ColorHex="#455A64",SectionLabel="Lockers",             SortOrder=8},
//        new(){Category="OPERATIONS",         SubCategory="CHANNELS", ColorHex="#546E7A",SectionLabel="ATMs / Channels",     SortOrder=9},
//        new(){Category="PENDING_POSITION",   SubCategory="",         ColorHex="#3E2723",SectionLabel="Pending Position",    SortOrder=10},
//        new(){Category="DIGITAL_BANKING",    SubCategory="",         ColorHex="#006064",SectionLabel="Digital Banking",     SortOrder=11},
//        new(){Category="DIGITAL_BUSINESS",   SubCategory="",         ColorHex="#1A237E",SectionLabel="Digital Loans",       SortOrder=12},
//        new(){Category="FINANCIAL_INCLUSION",SubCategory="",         ColorHex="#33691E",SectionLabel="Financial Inclusion", SortOrder=13},
//        new(){Category="THIRD_PARTY_PRODUCTS",SubCategory="",        ColorHex="#4A148C",SectionLabel="Third Party Products",SortOrder=14},
//        new(){Category="JANSAMARTH",         SubCategory="",         ColorHex="#880E4F",SectionLabel="JanSamarth Pendency", SortOrder=15},
//    ];

//    // ── IGapService ───────────────────────────────────────────────────────────
//    public Task<List<GapBranchListItem>> GetBranchesAsync(string? search = null)
//    {
//        var list = string.IsNullOrWhiteSpace(search)
//            ? _branches
//            : _branches.Where(b =>
//                b.BranchName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
//                b.BranchCode.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
//        return Task.FromResult(list);
//    }

//    public Task<GapBranchSummary?> GetBranchSummaryAsync(int branchId, DateTime? snapshotDate = null)
//        => Task.FromResult<GapBranchSummary?>(BranchSummary(branchId));

//    public Task<List<GapStaffDetail>> GetStaffDetailsAsync(int branchId, DateTime? snapshotDate = null)
//        => Task.FromResult(Staff(branchId));

//    public Task<(int TotalStaff, decimal PerEmployeeBusiness)> GetStaffTotalsAsync(int branchId, DateTime? snapshotDate = null)
//    {
//        var staff = Staff(branchId);
//        int total = staff.Sum(s => s.HeadCount);
//        var data  = AllData(branchId);
//        // PEB = Total Business (Cr) / Total Staff
//        var biz   = data.FirstOrDefault(p => p.ParameterId == 51)?.ActualCr ?? 0;
//        decimal peb = total > 0 ? Math.Round(biz / total, 2) : 0;
//        return Task.FromResult((total, peb));
//    }

//    public Task<List<GapPerformanceRow>> GetAllDataAsync(int branchId, DateTime? asOnDate = null)
//        => Task.FromResult(AllData(branchId));

//    public Task<List<F360CategoryColor>> GetCategoryColorsAsync()
//        => Task.FromResult(_colors);

//    public async Task<GapReportViewModel> BuildReportAsync(int branchId, string financialYear, DateTime? snapshotDate = null)
//    {
//        var branch     = await GetBranchSummaryAsync(branchId) ?? new GapBranchSummary();
//        var staff      = await GetStaffDetailsAsync(branchId);
//        var (tot, peb) = await GetStaffTotalsAsync(branchId);
//        var data       = await GetAllDataAsync(branchId);
//        var colors     = await GetCategoryColorsAsync();

//        return new GapReportViewModel
//        {
//            Branch              = branch,
//            Staff               = staff,
//            TotalStaff          = tot,
//            PerEmployeeBusiness = peb,
//            AllData             = data,
//            CategoryColors      = colors,
//            FinancialYear       = financialYear,
//            GeneratedAt         = DateTime.Now,
//            AsOnDate            = snapshotDate ?? DateTime.Today,
//        };
//    }
//}
