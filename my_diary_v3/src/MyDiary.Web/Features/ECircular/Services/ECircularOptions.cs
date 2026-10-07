namespace MyDiary.Web.Features.ECircular.Services;

/// <summary>
/// Configuration options for the ECircular feature.
/// Mapped from appsettings.json section "ECircular".
/// </summary>
public sealed class ECircularOptions
{
    public const string SectionName = "ECircular";

    /// <summary>Base URL of the DMS eCircular application (used for search and PDF download).</summary>
    public string BaseDmsUrl { get; set; } = "https://dms.unionbankofindia.co.in/webdesktop/CustomJobs/eCircular/";

    /// <summary>Whether to accept any SSL certificate from the DMS server (for self-signed certs in internal environments).</summary>
    public bool AcceptAnyServerCertificate { get; set; } = true;

    /// <summary>HTTP request timeout for DMS calls in seconds.</summary>
    public int HttpRequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Earliest year shown in the Publish Year dropdown filter.</summary>
    public int StartYear { get; set; } = 2013;
}
