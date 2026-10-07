namespace MyDiary.Web.Features.BcpDrill.Models;

public static class BcpTypes
{
    public const string Bcp = "BCP";
    public const string Dr = "DR";
    public static readonly string[] All = { Bcp, Dr };
    public static string Label(string? t) => t == Dr ? "DR Drill" : "BCP Testing";
    public static bool IsValid(string? t) => t is not null && All.Contains(t, StringComparer.Ordinal);
}

public static class BcpStatus
{
    public const string Submitted = "SUBMITTED";
    public const string Acknowledged = "ACKNOWLEDGED";
    public const string Returned = "RETURNED";
}

/// <summary>Annexure-I "Particulars of Operations" rows, in the order printed on the form.</summary>
public static class BcpMetrics
{
    public sealed record Def(string Code, string Label, bool HasAmount);

    public static readonly Def[] All =
    {
        new("CASH",     "No & Amt of cash transactions", true),
        new("REMIT",    "No of remittances (NEFT/RTGS/IMPS)", true),
        new("DDPO",     "No of DD/PO issued", true),
        new("ACCOPEN",  "No of accounts opened", false),
        new("ATMCARD",  "No of ATM cards issued", false),
        new("CHQBOOK",  "No of cheque books issued", false),
        new("CTS",      "No of cheques received for collection (CTS)", true),
        new("OTHER",    "Any other operations", false),
    };
}

public enum BcpLevel { Branch, Region, Zone, Central }

/// <summary>Server-derived permissions for one user. Pages use it only to decide what to render.</summary>
public sealed class BcpScope
{
    public BcpLevel Level { get; init; }
    public bool IsAdmin { get; init; }
    public bool CanViewAll { get; init; }
    public IReadOnlyList<string> ZoneCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RegionCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BranchCodes { get; init; } = Array.Empty<string>();
    public string EmplId { get; init; } = "";

    /// <summary>Branch users (and admins) submit Annexure-I.</summary>
    public bool CanSubmitDrill => IsAdmin || Level == BcpLevel.Branch;
    /// <summary>Regional Offices (and admins) record Annexure-II and review branch submissions.</summary>
    public bool CanCertifyRegion => IsAdmin || Level == BcpLevel.Region;
    public bool CanReview => IsAdmin || Level == BcpLevel.Region;
    public bool CanManageMapping => IsAdmin;
    public bool CanViewAudit => IsAdmin || CanViewAll;

    public string DisplayLevel => Level switch
    {
        BcpLevel.Branch => "Branch",
        BcpLevel.Region => "Regional Office",
        BcpLevel.Zone => "Zonal Office",
        _ => "Central Office"
    };

    public bool OwnsOffice(string? zone, string? region, string? sol)
    {
        if (IsAdmin) return true;
        return Level switch
        {
            BcpLevel.Central => CanViewAll,
            BcpLevel.Zone => zone is not null && ZoneCodes.Contains(zone, StringComparer.OrdinalIgnoreCase),
            BcpLevel.Region => region is not null && RegionCodes.Contains(region, StringComparer.OrdinalIgnoreCase),
            BcpLevel.Branch => sol is not null && BranchCodes.Contains(sol, StringComparer.OrdinalIgnoreCase),
            _ => false
        };
    }
}

public sealed record OrgOption(string Code, string Name, string? ParentCode = null);

public sealed class BcpBranch
{
    public string SolId { get; set; } = "";
    public string? BranchCode { get; set; }
    public string BranchName { get; set; } = "";
    public string RegionCode { get; set; } = "";
    public string? RegionName { get; set; }
    public string ZoneCode { get; set; } = "";
    public string? ZoneName { get; set; }
    public string? Category { get; set; }
    public string? Link1SolId { get; set; }
    public string? Link2SolId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class BcpMetricValue
{
    public string Code { get; set; } = "";
    public long? NormalCount { get; set; }
    public decimal? NormalAmount { get; set; }
    public long? AltCount { get; set; }
    public decimal? AltAmount { get; set; }
}

/// <summary>One Annexure-I submission (header + metrics). The supporting document BLOB is fetched separately.</summary>
public sealed class BcpDrillEntry
{
    public long Id { get; set; }
    public string DrillType { get; set; } = BcpTypes.Bcp;
    public string SolId { get; set; } = "";
    public string? BranchCode { get; set; }
    public string BranchName { get; set; } = "";
    public string RegionCode { get; set; } = "";
    public string? RegionName { get; set; }
    public string ZoneCode { get; set; } = "";
    public string? ZoneName { get; set; }
    public string? Category { get; set; }
    public int? StaffNormal { get; set; }
    public DateTime DrillDate { get; set; } = DateTime.Today;
    public string? AltSolId { get; set; }
    public string? AltBranchCode { get; set; }
    public string? AltBranchName { get; set; }
    public string? AltRegion { get; set; }
    public string? AltZone { get; set; }
    public string? AltCategory { get; set; }
    public int? StaffAlt { get; set; }
    public string? Remarks { get; set; }
    public bool IsCompliant { get; set; } = true;
    public string? FinYear { get; set; }
    public string Status { get; set; } = BcpStatus.Submitted;
    public string? ReviewBy { get; set; }
    public DateTime? ReviewAt { get; set; }
    public string? ReviewRemarks { get; set; }
    public string? DocName { get; set; }
    public long? DocSize { get; set; }
    public string? EntryBy { get; set; }
    public DateTime? EntryAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<BcpMetricValue> Metrics { get; set; } = new();

    public string RefNo => $"BCP/{SolId}/{Id}";
}

public sealed class BcpCertLine
{
    public int SrNo { get; set; }
    public string SolId { get; set; } = "";
    public string? BranchName { get; set; }
    public string? DisasterName { get; set; }
    public DateTime? DrillDate { get; set; }
    public string? Map1SolId { get; set; }
    public string? Map1Name { get; set; }
    public string? Map2SolId { get; set; }
    public string? Map2Name { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>Annexure-II: Regional Office certificate of BCP drill and linking/mapping of all branches.</summary>
public sealed class BcpRoCert
{
    public long Id { get; set; }
    public string RegionCode { get; set; } = "";
    public string? RegionName { get; set; }
    public string ZoneCode { get; set; } = "";
    public string? ZoneName { get; set; }
    public DateTime CertDate { get; set; } = DateTime.Today;
    public string? SignatoryName { get; set; }
    public string? FinYear { get; set; }
    public string? EntryBy { get; set; }
    public DateTime? EntryAt { get; set; }
    public List<BcpCertLine> Lines { get; set; } = new();
}

public sealed class BcpDrillFilter
{
    public string? ZoneCode { get; set; }
    public string? RegionCode { get; set; }
    public string? SolId { get; set; }
    public string? DrillType { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Search { get; set; }
}

/// <summary>Branch-wise MIS line (BRD: branch name/code, RO, ZO, drill date, BCP branch, SOL, status).</summary>
public sealed class BcpBranchMisRow
{
    public string SolId { get; set; } = "";
    public string? BranchCode { get; set; }
    public string BranchName { get; set; } = "";
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public string? ZoneCode { get; set; }
    public string? ZoneName { get; set; }
    public string? DrillType { get; set; }
    public DateTime? DrillDate { get; set; }
    public string? AltBranchName { get; set; }
    public string? AltSolId { get; set; }
    public string? DrillStatus { get; set; }
    public bool Submitted => DrillDate.HasValue;
    public string ComplianceLabel => Submitted ? "Submitted" : "Not Submitted";
}

public sealed class BcpRegionMisRow
{
    public string ZoneCode { get; set; } = "";
    public string? ZoneName { get; set; }
    public string RegionCode { get; set; } = "";
    public string? RegionName { get; set; }
    public int MappedBranches { get; set; }
    public int Submitted { get; set; }
    public int Acknowledged { get; set; }
    public int RoCertificates { get; set; }
    public int Pending => MappedBranches - Submitted;
    public double CompliancePct => MappedBranches == 0 ? 0d : Math.Round(Submitted * 100d / MappedBranches, 1);
}

public sealed class BcpZoneMisRow
{
    public string ZoneCode { get; set; } = "";
    public string? ZoneName { get; set; }
    public List<BcpRegionMisRow> Regions { get; set; } = new();
    public int MappedBranches => Regions.Sum(r => r.MappedBranches);
    public int Submitted => Regions.Sum(r => r.Submitted);
    public int Pending => MappedBranches - Submitted;
    public double CompliancePct => MappedBranches == 0 ? 0d : Math.Round(Submitted * 100d / MappedBranches, 1);
}

public sealed class BcpPeriodRow
{
    public string FinYear { get; set; } = "";
    public string Quarter { get; set; } = "";
    public string DrillType { get; set; } = "";
    public int Drills { get; set; }
    public int Branches { get; set; }
}

public sealed class BcpAuditRow
{
    public long Id { get; set; }
    public string Entity { get; set; } = "";
    public string? EntityId { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public string? Actor { get; set; }
    public string? ActorLevel { get; set; }
    public DateTime At { get; set; }
}

public static class BcpPeriod
{
    /// <summary>Indian financial year label, e.g. 2026-27 for 05-Aug-2026.</summary>
    public static string FinYear(DateTime d)
    {
        var start = d.Month >= 4 ? d.Year : d.Year - 1;
        return $"{start}-{(start + 1) % 100:00}";
    }

    /// <summary>Q1 = Apr-Jun ... Q4 = Jan-Mar.</summary>
    public static string Quarter(DateTime d) => d.Month switch
    {
        >= 4 and <= 6 => "Q1",
        >= 7 and <= 9 => "Q2",
        >= 10 and <= 12 => "Q3",
        _ => "Q4"
    };

    public static (DateTime From, DateTime To) FinYearRange(int startYear) =>
        (new DateTime(startYear, 4, 1), new DateTime(startYear + 1, 3, 31));
}
