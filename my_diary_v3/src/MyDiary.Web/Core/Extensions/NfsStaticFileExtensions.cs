using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace MyDiary.Web.Core.Extensions;

/// <summary>
/// Extension methods to configure static file serving with NFS support.
///
/// Current deployment model: NFS folders (Downloads, Images, UBINET) are mounted
/// inside wwwroot under a subfolder (wwwroot/MyDiary/). ASP.NET Core's standard
/// UseStaticFiles() serves them transparently — no custom PhysicalFileProvider needed.
///
/// For the NFS subfolder, we apply aggressive caching (7 days, immutable) since
/// images and PDFs rarely change. Other wwwroot content (CSS/JS) gets 1-day caching.
///
/// Backward-compatible URL rewriting lets bare legacy paths (e.g.
/// /download/report.pdf or /images/logo.png) transparently resolve to the NFS
/// location under the subfolder (e.g. /MyDiary/download/report.pdf). By default
/// this is CONVENTION-BASED (StaticAssetOptions.AutoAliasSubFolders): any real
/// subfolder of wwwroot/{SubFolder} is aliased automatically, so newly added
/// folders need no config change. StaticAssetOptions.PathMappings remains as an
/// explicit override for cases where the legacy URL differs from the folder name.
/// Front-end/framework roots (css, js, _framework, ...) are excluded so they
/// keep serving from the pod's wwwroot.
/// </summary>
public static class NfsStaticFileExtensions
{
    /// <summary>
    /// Registers the <see cref="StaticAssetOptions"/> configuration
    /// and the <see cref="IStaticAssetPathResolver"/> service.
    /// Call in the service registration phase (before Build).
    /// </summary>
    public static IServiceCollection AddStaticAssetConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StaticAssetOptions>(
            configuration.GetSection(StaticAssetOptions.SectionName));
        services.AddSingleton<IStaticAssetPathResolver, StaticAssetPathResolver>();
        return services;
    }

    /// <summary>
    /// Adds URL path rewrite middleware for legacy static asset paths.
    /// Must be called BEFORE UseStaticFilesWithNfs() in the pipeline.
    ///
    /// By default (AutoAliasSubFolders=true) any real subfolder of
    /// wwwroot/{SubFolder} is aliased at its bare URL automatically: a request to
    /// /images/logo.png is rewritten to /{SubFolder}/images/logo.png before
    /// reaching the static-file middleware — no PathMappings entry needed. An
    /// explicit PathMappings entry (e.g. "download" → "MyDiary/Downloads") still
    /// works and takes precedence, for cases where the legacy URL prefix differs
    /// from the physical folder name. Excluded segments (css/js/_framework/...)
    /// and non-existent folders are left untouched. The browser URL does not
    /// change (server-side rewrite, not a redirect).
    /// </summary>
    public static WebApplication UseStaticAssetPathRewrite(this WebApplication app)
    {
        var options = app.Services
            .GetRequiredService<IOptions<StaticAssetOptions>>().Value;

        var autoAlias = options.UseNfs
                        && options.AutoAliasSubFolders
                        && !string.IsNullOrWhiteSpace(options.SubFolder);

        // Nothing to do if NFS is off, or there are neither explicit mappings nor
        // convention-based auto-aliasing to apply.
        if (!options.UseNfs || (options.PathMappings.Count == 0 && !autoAlias))
            return app;

        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("StaticAssets");

        var subFolder = options.SubFolder.Trim('/');
        var excluded = new HashSet<string>(
            options.ExcludedAliasSegments ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        // Explicit overrides (legacy URL prefix -> new URL prefix). These always
        // win over convention-based auto-aliasing.
        var explicitMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in options.PathMappings)
            explicitMap[kv.Key.Trim('/')] = kv.Value.TrimStart('/');

        // Physical root that auto-aliasing probes (wwwroot/{SubFolder}). Folders
        // are looked up dynamically per request so a folder added on the NFS
        // mount after startup is aliased without an app restart. Results are
        // cached to keep the hot path off the disk.
        var env = app.Services.GetRequiredService<IWebHostEnvironment>();
        var subFolderRoot = Path.Combine(env.WebRootPath, subFolder);
        var aliasCache = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(
            StringComparer.OrdinalIgnoreCase);

        logger.LogInformation(
            "Static asset path rewrite enabled. Explicit mappings: [{Mappings}]. " +
            "Auto-alias subfolders of wwwroot/{SubFolder}: {AutoAlias} (excluding [{Excluded}]).",
            string.Join(", ", explicitMap.Select(kv => $"/{kv.Key} → /{kv.Value}")),
            subFolder, autoAlias, string.Join(", ", excluded));

        // Cached check: does wwwroot/{SubFolder}/<seg> exist as a directory?
        bool IsAliasableFolder(string seg) =>
            aliasCache.GetOrAdd(seg, s =>
            {
                try
                {
                    // Guard against traversal / invalid names before touching disk.
                    if (s.Contains("..") || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        return false;
                    return Directory.Exists(Path.Combine(subFolderRoot, s));
                }
                catch
                {
                    return false;
                }
            });

        app.Use(async (context, next) =>
        {
            var requestPath = context.Request.Path.Value;
            if (!string.IsNullOrEmpty(requestPath) && requestPath.Length > 1 && requestPath[0] == '/')
            {
                // First path segment (case-insensitive), e.g. "/images/x.png" -> "images".
                var slash = requestPath.IndexOf('/', 1);
                var seg = slash < 0 ? requestPath[1..] : requestPath[1..slash];
                var remainder = slash < 0 ? string.Empty : requestPath[slash..];

                string? newPrefix = null;

                // 1) Explicit mapping wins.
                if (explicitMap.TryGetValue(seg, out var mapped))
                {
                    newPrefix = mapped;
                }
                // 2) Convention: alias a bare segment to the NFS subfolder when a
                //    matching physical folder exists and it isn't already the
                //    subfolder itself or an excluded (front-end/framework) segment.
                else if (autoAlias
                         && !excluded.Contains(seg)
                         && !seg.Equals(subFolder, StringComparison.OrdinalIgnoreCase)
                         && IsAliasableFolder(seg))
                {
                    newPrefix = $"{subFolder}/{seg}";
                }

                if (newPrefix is not null)
                    context.Request.Path = "/" + newPrefix.TrimStart('/') + remainder;
            }
            await next();
        });

        return app;
    }

    /// <summary>
    /// Adds static file middleware with NFS-aware caching.
    /// <list type="bullet">
    ///   <item>When NFS is mounted inside wwwroot (SubFolder mode): applies aggressive
    ///         7-day immutable caching for the NFS subfolder, 1-day for everything else.</item>
    ///   <item>When NFS is disabled: standard 1-day caching for all static files.</item>
    ///   <item>Legacy mode (external NfsPath): creates PhysicalFileProviders for each folder.</item>
    /// </list>
    /// </summary>
    public static WebApplication UseStaticFilesWithNfs(this WebApplication app)
    {
        var options = app.Services
            .GetRequiredService<IOptions<StaticAssetOptions>>().Value;
        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("StaticAssets");

        // ── Mode 1: NFS disabled (Development / IIS without NFS) ──
        if (!options.UseNfs)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.CacheControl = "public,max-age=86400";
                }
            });
            return app;
        }

        // ── Mode 2: NFS mounted inside wwwroot (SubFolder mode — current deployment) ──
        if (!string.IsNullOrWhiteSpace(options.SubFolder))
        {
            var env = app.Services.GetRequiredService<IWebHostEnvironment>();
            var nfsSubPath = Path.Combine(env.WebRootPath, options.SubFolder);

            if (Directory.Exists(nfsSubPath))
            {
                logger.LogInformation(
                    "NFS mode (SubFolder): Serving assets from wwwroot/{SubFolder}/ " +
                    "with aggressive caching. Folders: [{Folders}]",
                    options.SubFolder, string.Join(", ", options.NfsFolders));
            }
            else
            {
                logger.LogWarning(
                    "StaticAssets:SubFolder '{SubFolder}' does not exist at {Path}. " +
                    "NFS assets will return 404 until the folder is created/mounted.",
                    options.SubFolder, nfsSubPath);
            }

            // Single UseStaticFiles call serves everything from wwwroot (including the
            // NFS-mounted subfolder). We differentiate caching based on the request path.
            var subFolderPrefix = "/" + options.SubFolder.TrimStart('/');

            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    var requestPath = ctx.Context.Request.Path.Value ?? "";
                    if (requestPath.StartsWith(subFolderPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        // NFS assets: aggressive caching (images/PDFs/documents rarely change)
                        ctx.Context.Response.Headers.CacheControl = "public, max-age=604800, immutable";
                    }
                    else
                    {
                        // CSS/JS/fonts: standard 1-day caching
                        ctx.Context.Response.Headers.CacheControl = "public,max-age=86400";
                    }
                }
            });
            return app;
        }

        // ── Mode 3: Legacy — external NFS mount path (not inside wwwroot) ──
        if (!string.IsNullOrWhiteSpace(options.NfsPath) && Directory.Exists(options.NfsPath))
        {
            logger.LogInformation(
                "NFS mode (external): Serving folders [{Folders}] from {NfsPath}.",
                string.Join(", ", options.NfsFolders), options.NfsPath);

            foreach (var folder in options.NfsFolders)
            {
                var nfsFolderPath = Path.Combine(options.NfsPath, folder.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(nfsFolderPath))
                {
                    logger.LogDebug("NFS subfolder '{Folder}' not found at {Path}; skipping.", folder, nfsFolderPath);
                    continue;
                }

                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(nfsFolderPath),
                    RequestPath = "/" + folder.TrimStart('/'),
                    ServeUnknownFileTypes = false,
                    OnPrepareResponse = ctx =>
                    {
                        ctx.Context.Response.Headers.CacheControl = "public, max-age=604800, immutable";
                    }
                });
            }

            // wwwroot for everything else
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.CacheControl = "public,max-age=86400";
                }
            });
            return app;
        }

        // ── Fallback: UseNfs=true but neither SubFolder nor NfsPath configured ──
        logger.LogWarning(
            "StaticAssets:UseNfs is true but neither SubFolder nor NfsPath is configured. " +
            "Falling back to standard wwwroot serving.");
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = "public,max-age=86400";
            }
        });
        return app;
    }
}
