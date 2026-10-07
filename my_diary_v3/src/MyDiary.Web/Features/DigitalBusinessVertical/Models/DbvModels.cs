namespace MyDiary.Web.Features.DigitalBusinessVertical.Models;

/// <summary>A DBV campaign (BRD: name, product, description, target segment, duration, remarks, outcomes) with one or
/// more communication rows (BRD: "multiple Modes, Templates, Execution Dates and Creatives").</summary>
public sealed class DbvCampaign
{
    public long Id { get; set; }
    public string CampaignName { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string TargetSegment { get; set; } = "BOTH";       // ETB | NTB | BOTH | OTHER
    public string? Description { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }                      // optional when IsOngoing
    public bool IsOngoing { get; set; }
    public string? Remarks { get; set; }
    public string? OutcomeSummary { get; set; }                 // outcomes / achievements
    public bool IsArchived { get; set; }
    public string CreatedByEmp { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? UpdatedByEmp { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<DbvComm> Comms { get; set; } = new();

    public string StatusLabel => IsOngoing ? "Ongoing" : (EndDate.HasValue && EndDate.Value.Date < DateTime.Today ? "Completed" : "Ongoing");
    public string ModesText => string.Join(", ", Comms.Select(c => DbvLookups.ModeLabel(c.Mode)).Distinct());

    public static readonly string[] Segments = { "ETB", "NTB", "BOTH", "OTHER" };
}

public class DbvDownload
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    public string? FileName { get; set; }
    public string? FilePath { get; set; }

    public bool IsArchived { get; set; }

    public string CreatedByEmp { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public string? UpdatedByEmp { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class DbvOtherInitiative
{
    public long Id { get; set; }

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public string? FileName { get; set; }

    public string? FilePath { get; set; }

    public bool IsArchived { get; set; }

    public string CreatedByEmp { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public string? UpdatedByEmp { get; set; }

    public DateTime? UpdatedAt { get; set; }
}


public class DbvInitiative
{
    public long Id { get; set; }

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public string? FileName { get; set; }

    public string? FilePath { get; set; }

    public bool IsArchived { get; set; }

    public string CreatedByEmp { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public string? UpdatedByEmp { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public sealed class DbvComm
{
    public long Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string Mode { get; set; } = "SMS";
    public string? TemplateNo { get; set; }
    public string? Message { get; set; }
    public string? CreativeFileName { get; set; }
    public string? CreativeFilePath { get; set; }
}

public sealed class DbvCorrespondence
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime CorrDate { get; set; } = DateTime.Today;
    public string? Description { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFilePath { get; set; }
    public bool IsArchived { get; set; }
    public string CreatedByEmp { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? UpdatedByEmp { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Search / export criteria. Dates filter campaigns whose duration overlaps [From, To] (ongoing = open-ended).</summary>
public sealed class DbvFilter
{
    public string? Product { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string Status { get; set; } = "ALL";                 // ALL | ONGOING | COMPLETED
    public string? Mode { get; set; }                           // any communication row uses this mode
    public string? FinancialYear { get; set; }                  // any communication dated in this FY, e.g. "2025-26"
    public string? Quarter { get; set; }                        // Q1..Q4 (with FY, or any)
    public bool ArchivedOnly { get; set; }
    public bool IncludeArchived { get; set; }                   // admin: active + archived
}

public sealed record DbvSavedCriteria(long Id, string Name, string Json);

public sealed class DbvUploadResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public int Rows, Campaigns, NewCampaigns, CommsAdded, Skipped;
    public bool Applied;
    public bool IsValid => Errors.Count == 0;
}

public static class DbvLookups
{
    public static readonly (string Code, string Label)[] Modes =
    {
        ("SMS", "SMS"), ("WHATSAPP", "WhatsApp"), ("EMAIL", "Email"), ("GOOGLE_ADS", "Google Ads"), ("OTHER", "Other Digital Channel")
    };

    public static string ModeLabel(string code) => Modes.FirstOrDefault(m => m.Code == code).Label ?? code;

    public static string? ModeCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().ToUpperInvariant().Replace(" ", "").Replace("_", "");
        return t switch
        {
            "SMS" => "SMS", "WHATSAPP" => "WHATSAPP", "EMAIL" => "EMAIL", "GOOGLEADS" => "GOOGLE_ADS",
            "OTHER" or "OTHERDIGITALCHANNEL" or "OTHERDIGITALMODE" => "OTHER", _ => null
        };
    }

    public static string SegmentLabel(string code) => code switch { "ETB" => "Existing to Bank (ETB)", "NTB" => "New to Bank (NTB)", "BOTH" => "ETB + NTB", _ => "Other" };

    // Indian financial year: April - March. Quarter: Q1 Apr-Jun, Q2 Jul-Sep, Q3 Oct-Dec, Q4 Jan-Mar.
    public static string FinancialYear(DateTime d) => d.Month >= 4 ? $"{d.Year}-{(d.Year + 1) % 100:00}" : $"{d.Year - 1}-{d.Year % 100:00}";
    public static string Quarter(DateTime d) => d.Month switch { >= 4 and <= 6 => "Q1", >= 7 and <= 9 => "Q2", >= 10 and <= 12 => "Q3", _ => "Q4" };
    public static string MonthName(DateTime d) => d.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture);
}
