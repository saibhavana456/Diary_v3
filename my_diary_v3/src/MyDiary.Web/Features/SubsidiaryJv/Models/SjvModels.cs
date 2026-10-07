namespace MyDiary.Web.Features.SubsidiaryJv.Models;

public sealed class SjvEntity
{
    public long EntityId { get; set; }
    public string EntityCode { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string? ShortName { get; set; }
    public string EntityType { get; set; } = "SUBSIDIARY";      // SUBSIDIARY | JOINT_VENTURE | OTHER
    public string? Tagline { get; set; }
    public int? YearOfIncorp { get; set; }
    public decimal? OwnershipPct { get; set; }
    public string? OwnershipNote { get; set; }
    public decimal? PaidupCapitalCr { get; set; }
    public string? PaidupAsOn { get; set; }
    public int? Branches { get; set; }
    public string? BranchesNote { get; set; }
    public int? Employees { get; set; }
    public string? EmployeesAsOn { get; set; }
    public string? CeoName { get; set; }
    public string? Status { get; set; }
    public string? Description { get; set; }
    public string? RegisteredOffice { get; set; }
    public string? Cin { get; set; }
    public string? Website { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Logo is stored as a BLOB on the entity row itself (RP_SJV_ENTITY.LOGO_BLOB) -- with only a
    /// handful of entities that's simpler than managing files on disk. Listing queries only fetch whether one
    /// exists (this flag); the bytes are fetched on demand via ISubsidiaryJvService.GetLogoAsync, streamed by
    /// the /sjv/logo/{code} endpoint, and referenced from &lt;img&gt; tags by URL rather than embedded.</summary>
    public bool HasLogo { get; set; }

    public string? EntryBy { get; set; }
    public DateTime? EntryAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Latest-FY summary (landing cards) - filled by GetEntitiesAsync.
    public string? LatestFy { get; set; }
    public decimal? LatestNetProfit { get; set; }
    public decimal? NetProfitGrowthPct { get; set; }
    public decimal? LatestNetWorth { get; set; }

    public string TypeLabel => EntityType switch
    {
        "SUBSIDIARY" => "Subsidiary",
        "JOINT_VENTURE" => "Joint Venture",
        "ASSOCIATE" => "Associate",
        "OTHER" => "Others",
        _ => "Others"
    };
}

public sealed record SjvOffice(string LocationType, int Number);
public sealed record SjvHolder(string HolderName, decimal Pct);
public sealed record SjvDirector(string Name, string? Designation, string? Category, string? Representing, DateTime? AppointedOn)
{
    public string Tenure
    {
        get
        {
            if (AppointedOn is null) return "-";
            var months = (int)((DateTime.Today - AppointedOn.Value).TotalDays / 30.4375);
            if (months < 1) return "< 1 Month";
            var y = months / 12; var m = months % 12;
            return y > 0 ? $"{y} Year{(y > 1 ? "s" : "")}" + (m > 0 ? $" {m} M" : "") : $"{m} Month{(m > 1 ? "s" : "")}";
        }
    }
}
public sealed record SjvKmp(string Name, string? Designation, string? Email);
public sealed record SjvUpdate(string Text, DateTime? Date);
public sealed record SjvFinancial(string FinYear, decimal? TotalIncome, decimal? NetProfit, decimal? NetWorth, decimal? TotalAssets);

/// <summary>Optional quarter-wise figures inside a financial year (Q1 = Apr-Jun ... Q4 = Jan-Mar). Any amount may be negative (a loss).</summary>
public sealed record SjvQuarterly(string FinYear, string Quarter, decimal? TotalIncome, decimal? NetProfit, decimal? NetWorth, decimal? TotalAssets)
{
    public static readonly string[] Quarters = { "Q1", "Q2", "Q3", "Q4" };
    public static string Months(string q) => q switch { "Q1" => "Apr-Jun", "Q2" => "Jul-Sep", "Q3" => "Oct-Dec", "Q4" => "Jan-Mar", _ => "" };

    /// <summary>Short axis / column label, e.g. "Q1 24-25".</summary>
    public string Label => $"{Quarter} {FinYear.Replace("FY 20", "")}";

    public SjvFinancial AsFinancial() => new(Label, TotalIncome, NetProfit, NetWorth, TotalAssets);
}

public sealed class SjvEntityDetail
{
    public SjvEntity Entity { get; set; } = new();
    public List<SjvOffice> Offices { get; set; } = new();
    public List<SjvHolder> Shareholding { get; set; } = new();
    public List<string> Activities { get; set; } = new();
    public List<SjvDirector> Board { get; set; } = new();
    public List<SjvKmp> Kmp { get; set; } = new();
    public List<SjvFinancial> Financials { get; set; } = new();   // oldest first
    public List<SjvQuarterly> Quarterly { get; set; } = new();    // oldest first; optional
    public List<SjvUpdate> Updates { get; set; } = new();
}

public sealed class SjvUploadResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public int Entities, Offices, Holders, Activities, Board, Kmp, Financials, Quarterly, Updates;
    public bool Applied;
    public bool IsValid => Errors.Count == 0;
    public int TotalRows => Entities + Offices + Holders + Activities + Board + Kmp + Financials + Quarterly + Updates;
}
