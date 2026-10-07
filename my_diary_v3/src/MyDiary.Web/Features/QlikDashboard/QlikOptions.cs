namespace MyDiary.Web.Features.QlikDashboard;

/// <summary>
/// Configuration for embedding Qlik Sense Enterprise (on-prem) content into the
/// portal via the <c>qlik-embed</c> web components with QPS ticket authentication.
///
/// Bound from the "Qlik" configuration section. In production the certificate
/// path/password and host live in the encrypted config / secret store.
/// </summary>
public sealed class QlikOptions
{
    /// <summary>"Embed" = live Qlik via qlik-embed web components (no iframe);
    /// "Mock" = local sample dashboard drawn natively (dev only).</summary>
    public string Mode { get; set; } = "Mock";

    public string Title { get; set; } = "Qlik Dashboard";

    /// <summary>Qlik proxy host, scheme + host(+port) only, e.g. https://qlik.ubi.local</summary>
    public string Host { get; set; } = "";

    /// <summary>Virtual proxy prefix configured in the QMC (blank = default proxy).</summary>
    public string VirtualProxyPrefix { get; set; } = "";

    /// <summary>QPS ticket API host:port (defaults to {Host} on 4243 if blank).</summary>
    public string QpsUri { get; set; } = "";

    /// <summary>User directory Qlik expects for the ticket (e.g. UBI / DOMAIN).</summary>
    public string UserDirectory { get; set; } = "UBI";

    /// <summary>App GUID to open.</summary>
    public string AppId { get; set; } = "";

    /// <summary>Sheet id (for reference / "open in Qlik" deep link).</summary>
    public string SheetId { get; set; } = "";

    /// <summary>Object/visualization GUIDs to render as individual charts when
    /// <see cref="Render"/> = "objects". Ignored when rendering a whole sheet.</summary>
    public List<string> ObjectIds { get; set; } = new();

    // ── qlik-embed settings ──────────────────────────────────────────────────

    /// <summary>What to embed: "sheet" = the whole sheet as-is (Qlik's own toolbar
    /// + export), or "objects" = one chart per <see cref="ObjectIds"/> entry.</summary>
    public string Render { get; set; } = "sheet";

    /// <summary>URL of the qlik-embed web-components library. On-prem this should be
    /// self-hosted / served from the Qlik host; CDN only if the browser can reach it.
    /// e.g. https://qlik.ubi.local/portal/resources/qlik-embed/index.js</summary>
    public string EmbedScriptUrl { get; set; } = "";

    /// <summary>qlik-embed auth type. QSEoW cookie session established by the QPS
    /// ticket ⇒ "qsefe"; Qlik Cloud ⇒ "Oauth2".</summary>
    public string AuthType { get; set; } = "qsefe";

    /// <summary>OAuth2 client id / redirect (only used when AuthType = "Oauth2").</summary>
    public string ClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "";

    /// <summary>Client certificate (PFX exported from the QMC) used for the mutual-TLS
    /// call to the QPS ticket endpoint.</summary>
    public string ClientCertPath { get; set; } = "";
    public string ClientCertPassword { get; set; } = "";

    /// <summary>Skip server-cert validation on the QPS call (dev only — Qlik uses a
    /// self-signed cert out of the box). MUST be false in production.</summary>
    public bool AllowInvalidServerCert { get; set; } = false;

    public bool IsMock => string.Equals(Mode, "Mock", StringComparison.OrdinalIgnoreCase);
}
