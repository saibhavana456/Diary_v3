namespace MyDiary.Web.Features.Focus360.Models;

/// <summary>Bound from appsettings.json "Focus360" section.</summary>
public class GapReportSettings
{
    /// <summary>App / portal logo shown on the LEFT of the PDF header.
    /// Path is relative to the static-asset root resolved by IStaticAssetPathResolver
    /// (wwwroot/MyDiary when NFS is enabled, wwwroot otherwise).
    /// Default: images/app-logo.png</summary>
    public string AppLogoPath { get; set; } = "images/app-logo.png";

    /// <summary>Company / bank logo shown on the RIGHT of the PDF header.
    /// Path is relative to the static-asset root (see AppLogoPath).
    /// Default: images/logo.png</summary>
    public string LogoPath    { get; set; } = "images/logo.png";

    /// <summary>UBI logo used by some report headers. Path is relative to the
    /// static-asset root (see AppLogoPath). Default: images/UBI_Logo.jpg</summary>
    public string UBILogoPath { get; set; } = "images/UBI_Logo.png";
    /// <summary>Full report name displayed on PDF, Excel header and page title.</summary>
    public string ReportName  { get; set; } = "FOCUS 360: Financial Operational and Compliance Unit Snapshot";
    public string BankName    { get; set; } = string.Empty;
    public string ReportFooter{ get; set; } = "Confidential | Internal Use Only";

    // ── Assurance Snapshot (V3-only) — restored after the V2 merge overwrote Focus360 ──
    /// <summary>Report name for the Assurance Snapshot page/exports.</summary>
    public string AssuranceReportName { get; set; } = "FOCUS 360: Assurance Snapshot";

    /// <summary>Categories that make up the Assurance snapshot; empty = render all.</summary>
    public List<string> AssuranceCategories { get; set; } = new();
}
