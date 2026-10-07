namespace RequestPortal.Core.Models;

// ---------------------------------------------------------------------------
// Recon Certificates — Annexure 3 (Vertical/CO-level Sundry/Suspense/POB/Inter-bank ageing certificate),
// exact port of the source app's ANNEXURE3 table + Annexure3.razor's HandleValidSubmit logic. Unlike
// Annexure 4, this has NO draft state, NO RO/ZO approval chain, and NO child tables — one Vertical
// submits once per month (STATUS is always '100' on save; a second submission for the same
// Vertical/month/year is simply blocked client-side, exactly as source does).
// ---------------------------------------------------------------------------

public sealed class ReconAnnexure3
{
    public long Id { get; set; }
    public string ReportId { get; set; } = "";
    public string ReportMonth { get; set; } = "";
    public string ReportYear { get; set; } = "";
    public string Vertical { get; set; } = "";

    public long? SunUpto3MEntries { get; set; }
    public decimal? SunUpto3MAmount { get; set; }
    public long? SunThree6MEntries { get; set; }
    public decimal? SunThree6MAmount { get; set; }
    public long? SunOver6MEntries { get; set; }
    public decimal? SunOver6MAmount { get; set; }
    public long? SunNewOver6MEntries { get; set; }
    public decimal? SunNewOver6MAmount { get; set; }
    public string? SunReason { get; set; }
    public string? SunEfforts { get; set; }

    public long? SusUpto3MEntries { get; set; }
    public decimal? SusUpto3MAmount { get; set; }
    public long? SusThree6MEntries { get; set; }
    public decimal? SusThree6MAmount { get; set; }
    public long? SusOver6MEntries { get; set; }
    public decimal? SusOver6MAmount { get; set; }
    public long? SusNewOver6MEntries { get; set; }
    public decimal? SusNewOver6MAmount { get; set; }
    public string? SusReason { get; set; }
    public string? SusEfforts { get; set; }

    public long? PobUpto3MEntries { get; set; }
    public decimal? PobUpto3MAmount { get; set; }
    public long? PobThree6MEntries { get; set; }
    public decimal? PobThree6MAmount { get; set; }
    public long? PobOver6MEntries { get; set; }
    public decimal? PobOver6MAmount { get; set; }
    public long? PobNewOver6MEntries { get; set; }
    public decimal? PobNewOver6MAmount { get; set; }
    public string? PobReason { get; set; }
    public string? PobEfforts { get; set; }

    public long? IbkUpto3MEntries { get; set; }
    public decimal? IbkUpto3MAmount { get; set; }
    public long? IbkThree6MEntries { get; set; }
    public decimal? IbkThree6MAmount { get; set; }
    public long? IbkOver6MEntries { get; set; }
    public decimal? IbkOver6MAmount { get; set; }
    public long? IbkNewOver6MEntries { get; set; }
    public decimal? IbkNewOver6MAmount { get; set; }
    public string? IbkReason { get; set; }
    public string? IbkEfforts { get; set; }

    public string DelFlag { get; set; } = "N";
    public string? EntryBy { get; set; }
    public DateTime? EntryDate { get; set; }
    /// <summary>Always '100' on save — exact literal used by the source app's HandleValidSubmit.</summary>
    public string? Status { get; set; }
    public string? ReconUserId { get; set; }
    public DateTime? ReconActionDate { get; set; }
}
