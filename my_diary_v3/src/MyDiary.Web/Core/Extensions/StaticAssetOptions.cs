namespace MyDiary.Web.Core.Extensions;

/// <summary>
/// Configuration for serving static assets with optional NFS support.
/// 
/// In the current deployment model, NFS folders (Downloads, Images, UBINET) are
/// mounted INSIDE wwwroot under a subfolder (e.g., wwwroot/MyDiary/).
/// This means standard UseStaticFiles() serves them without any custom
/// PhysicalFileProvider — the OS mount/symlink is transparent to ASP.NET Core.
///
/// The SubFolder setting tells server-side code (PDF generation, file downloads)
/// where to find assets on disk relative to wwwroot.
/// </summary>
public sealed class StaticAssetOptions
{
    public const string SectionName = "StaticAssets";

    /// <summary>
    /// When true, assets are served from the NFS-mounted subfolder within wwwroot.
    /// When false (Development/local), assets are served from wwwroot directly.
    /// </summary>
    public bool UseNfs { get; set; }

    /// <summary>
    /// The subfolder inside wwwroot where NFS content is mounted.
    /// Example: "MyDiary" → assets live at wwwroot/MyDiary/Images/, wwwroot/MyDiary/Downloads/, etc.
    /// URL path becomes /MyDiary/Images/logo.png
    /// Leave empty if NFS folders are mounted directly at wwwroot root level.
    /// </summary>
    public string SubFolder { get; set; } = string.Empty;

    /// <summary>
    /// The NFS-managed folders within <see cref="SubFolder"/>.
    /// Example: ["Downloads", "Images", "UBINET"]
    ///
    /// INFORMATIONAL ONLY in the current (SubFolder) deployment model: a single
    /// UseStaticFiles() over wwwroot serves everything under wwwroot/{SubFolder}/
    /// automatically, so a new folder does NOT need to be listed here to be
    /// served. This array is used only for a startup log line (and, in the
    /// legacy external-NfsPath mode, to register per-folder providers).
    /// </summary>
    public string[] NfsFolders { get; set; } = new[]
    {
        "downloads",
        "UBINET"
    };

    /// <summary>
    /// Absolute path to an external NFS mount (legacy/alternative mode).
    /// Only used if NFS is NOT mounted inside wwwroot.
    /// Leave empty when NFS is inside wwwroot (the current deployment model).
    /// </summary>
    public string NfsPath { get; set; } = string.Empty;

    /// <summary>
    /// EXPLICIT URL path rewrite overrides for backward compatibility. In most
    /// cases you no longer need to add anything here — see <see cref="AutoAliasSubFolders"/>,
    /// which auto-aliases every real subfolder of wwwroot/{SubFolder} at its bare
    /// legacy URL. Add an entry here only when the legacy URL prefix differs from
    /// the physical folder name (e.g. the old URL was "/download" but the folder
    /// is "Downloads"): { "download": "MyDiary/Downloads" }.
    ///
    /// Keys are the old URL prefix (without leading slash).
    /// Values are the new URL prefix (without leading slash).
    /// Matching is case-insensitive on the first path segment. Explicit entries
    /// take precedence over auto-aliasing.
    /// </summary>
    public Dictionary<string, string> PathMappings { get; set; } = new();

    /// <summary>
    /// When true (default) and NFS SubFolder mode is active, every immediate
    /// subfolder of wwwroot/{SubFolder} is automatically served at its bare
    /// legacy URL as well — e.g. a request to "/images/logo.png" is rewritten to
    /// "/{SubFolder}/images/logo.png" — WITHOUT needing a <see cref="PathMappings"/>
    /// entry. This means newly created folders (in code or directly on the NFS
    /// mount) are reachable at both "/{SubFolder}/&lt;folder&gt;/..." and the bare
    /// "/&lt;folder&gt;/..." with zero configuration changes.
    ///
    /// Segments in <see cref="ExcludedAliasSegments"/> (framework / CSS / JS /
    /// front-end asset roots) are never aliased, so pod-served static assets keep
    /// resolving from wwwroot as normal.
    /// </summary>
    public bool AutoAliasSubFolders { get; set; } = true;

    /// <summary>
    /// First-path segments that must never be auto-aliased to the NFS subfolder
    /// (front-end / framework assets that are served from the pod's wwwroot).
    /// Case-insensitive. Only consulted when <see cref="AutoAliasSubFolders"/> is
    /// true. Explicit <see cref="PathMappings"/> are unaffected by this list.
    /// </summary>
    public string[] ExcludedAliasSegments { get; set; } = new[]
    {
        // Blazor framework folders
        "_framework",
        "_content",
        "_blazor",

        // Static asset folders
        "assets",
        "bootstrap",
        "charts",
        "Components",
        "css",
        "js",
        "lib",
        "maps",

        // Application/content folders
        "MD_Sir_Intro",
        "rates",
        "tables",

        // Static files
        "app.css",
        "favicon.png",
        "MyDiary.Web.styles.css"
    };
}
