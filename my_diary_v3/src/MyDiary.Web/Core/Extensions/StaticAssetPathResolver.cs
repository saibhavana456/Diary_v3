using Microsoft.Extensions.Options;

namespace MyDiary.Web.Core.Extensions;

/// <summary>
/// Resolves the physical root path for static assets (images, PDFs, documents).
///
/// Deployment model:
///   - IIS (UAT/Prod without K8s): Assets in wwwroot/ directly → AssetsRoot = wwwroot
///   - Kubernetes: Assets mounted at wwwroot/MyDiary/ → AssetsRoot = wwwroot/MyDiary
///   - Development: Assets in wwwroot/ directly → AssetsRoot = wwwroot
///
/// Use this instead of IWebHostEnvironment.WebRootPath when accessing images, PDFs,
/// or documents from server-side code (e.g., PDF generation, file downloads).
/// CSS/JS files always live in wwwroot root — use WebRootPath directly for those.
/// </summary>
public interface IStaticAssetPathResolver
{
    /// <summary>
    /// Returns the physical root folder for static assets.
    /// In K8s with SubFolder="MyDiary": wwwroot/MyDiary
    /// In IIS/Dev without SubFolder: wwwroot
    /// </summary>
    string AssetsRoot { get; }

    /// <summary>
    /// The URL-path prefix for asset references in Razor pages.
    /// In K8s: "MyDiary" (so URLs become /MyDiary/Images/logo.png)
    /// In IIS/Dev: "" (URLs are /images/logo.png)
    /// </summary>
    string UrlPrefix { get; }

    /// <summary>
    /// Resolves a relative asset path (e.g., "Images/logo.png")
    /// to its full physical path on disk.
    /// </summary>
    string Resolve(string relativePath);

    /// <summary>
    /// Resolves a relative path from multiple segments.
    /// Example: Resolve("Images", "logos", "ubi.png")
    /// </summary>
    string Resolve(params string[] segments);

    /// <summary>
    /// Resolves a relative image/asset path to a browser-facing URL.
    /// Prepends the NFS subfolder prefix when NFS is enabled.
    /// 
    /// Example:
    ///   Dev:  ResolveUrl("images/banner.jpg") → "images/banner.jpg"
    ///   Prod: ResolveUrl("images/banner.jpg") → "MyDiary/images/banner.jpg"
    /// 
    /// Use this in Razor pages for any image src, background-image, or asset href
    /// that needs to work in both local and NFS environments.
    /// </summary>
    string ResolveUrl(string relativePath);
}

public sealed class StaticAssetPathResolver : IStaticAssetPathResolver
{
    private readonly string _assetsRoot;
    private readonly string _urlPrefix;

    public StaticAssetPathResolver(
        IWebHostEnvironment env,
        IOptions<StaticAssetOptions> options)
    {
        var opt = options.Value;

        if (opt.UseNfs && !string.IsNullOrWhiteSpace(opt.SubFolder))
        {
            // NFS is mounted inside wwwroot under the subfolder
            _assetsRoot = Path.Combine(env.WebRootPath, opt.SubFolder);
            _urlPrefix = opt.SubFolder;
        }
        else if (opt.UseNfs && !string.IsNullOrWhiteSpace(opt.NfsPath) && Directory.Exists(opt.NfsPath))
        {
            // Legacy mode: external NFS mount path
            _assetsRoot = opt.NfsPath;
            _urlPrefix = string.Empty;
        }
        else
        {
            // No NFS — assets in wwwroot directly (IIS/Development)
            _assetsRoot = env.WebRootPath;
            _urlPrefix = string.Empty;
        }
    }

    public string AssetsRoot => _assetsRoot;

    public string UrlPrefix => _urlPrefix;

    public string Resolve(string relativePath)
        => Path.Combine(_assetsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public string Resolve(params string[] segments)
        => Path.Combine(new[] { _assetsRoot }.Concat(segments).ToArray());

    public string ResolveUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_urlPrefix))
            return relativePath;
        return $"{_urlPrefix}/{relativePath.TrimStart('/')}";
    }
}
