using ApexCharts;
using Hangfire;
using Hangfire.Oracle.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor.Services;
using MyDiary.Core.Abstractions;
using MyDiary.Data.Oracle;
using MyDiary.Data.SqlServer;
using MyDiary.Web.Auth;
using MyDiary.Web.Components;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Core.Services.Storage;
using MyDiary.Web.Data;
using MyDiary.Web.Extensions;
using MyDiary.Web.Features.Focus360.Models;
using MyDiary.Web.Features.Focus360.Services;
using MyDiary.Web.Features.Shared.Models;
using MyDiary.Web.Services;
using RequestPortal.Core;
using RequestPortal.Data;
using Serilog;
using MyDiary.Web.Features.Dashboards.Services;
using RequestPortal.Core.Abstractions;
using RequestPortal.Data.Repositories;
using MyDiary.Web.RequestPortal;
using MyDiary.Web.Features.Assurance.Services;
using MyDiary.Web.Features.Reports.Services;
using MyDiary.Web.Features.ITKonnect.Services;
using MyDiary.Web.Features.StrikeCorner.Services;
using MyDiary.Web.Features.DigitalBusinessVertical.Services;
using RequestPortal.Core.Services;
using MyDiary.Web.Features.BranchFoundation.Services;

using MyDiary.Web.Features.DeploymentPulse.Services;
using MyDiary.Web.Features.ProjectMuskaan.Services;
// ── Bootstrap Serilog ─────────────────────────────────────────────────────────
// Resolve the log file path. When NFS is enabled (StaticAssets:UseNfs=true),
// logs go to a centralized NFS folder (Logging:NfsLogPath, e.g. "MyDiary/Logs")
// so they persist across pod restarts/rescheduling and all pods share one location.
// When NFS is disabled, the existing pod-level "Logs/" folder is used unchanged.
//
// Because the SAME NFS share is mounted for all three Kubernetes environments
// (Development / UAT / Production), we append the current ASPNETCORE_ENVIRONMENT
// as a subfolder (e.g. "MyDiary/Logs/Production") so each environment's logs are
// kept separate and never intermixed on the shared share.
//
// We also create the target directory up front. Serilog's file sink normally
// creates missing folders itself, but on a freshly-mounted NFS path under
// Kubernetes that silently fails if the process working directory / mount is not
// what the file sink expects — creating it here (and logging any failure to the
// console) makes the "no logs appear under MyDiary" symptom diagnosable instead
// of silent.
static string ResolveLogFilePath(IConfiguration cfg)
{
    const string fileName = "mydiary-.log";
    const string localDir = "Logs";

    var useNfs = cfg.GetValue<bool>("StaticAssets:UseNfs");

    string logDir;
    if (!useNfs)
    {
        logDir = localDir;
    }
    else
    {
        var nfsLogDir = cfg["Logging:NfsLogPath"];
        if (string.IsNullOrWhiteSpace(nfsLogDir))
            nfsLogDir = "MyDiary/Logs";

        // Separate logs per environment on the shared NFS share.
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (string.IsNullOrWhiteSpace(env))
            env = "Production";

        logDir = Path.Combine(nfsLogDir, env);
    }

    // Ensure the directory exists so the file sink can write to it. Never let a
    // logging-path problem take the app down — fall back to the pod-local folder.
    try
    {
        Directory.CreateDirectory(logDir);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(
            $"[Startup] Could not create log directory '{logDir}': {ex.Message}. " +
            $"Falling back to pod-local '{localDir}'.");
        logDir = localDir;
        try { Directory.CreateDirectory(logDir); } catch { /* best effort */ }
    }

    return Path.Combine(logDir, fileName);
}

// Minimal early configuration (appsettings + env-specific + env vars) so the
// bootstrap logger can honour the NFS log path too, before the host is built.
var bootstrapConfig = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(ResolveLogFilePath(bootstrapConfig), rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting My Diary V3");

    // ── Thread pool warm-up ─────────────────────────────────────────────────
    // Oracle.ManagedDataAccess.Core has no true async I/O (OpenAsync/ExecuteAsync
    // run synchronously under the hood — see OracleConnectionFactory.cs), and several
    // report/export services still call blocking APIs. Under a burst of concurrent
    // circuits (login storms, report generation) the default thread-pool growth rate
    // (~1-2 new threads/sec once the pool is exhausted) causes queued work to stall
    // for seconds. Raising the minimum worker/IOCP thread count means the pool starts
    // at a size that can absorb a burst without waiting on the slow growth ramp.
    ThreadPool.SetMinThreads(200, 200);

    // ── EPPlus 8.x license (set once at startup) ──────────────────────────
    OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("Union Bank of India");

    var builder = WebApplication.CreateBuilder(args);

    // ── Decrypt connection strings (AES-256-GCM) and inject plain values ───
    var plainConn = MyDiary.Data.Extensions.ConnectionStringDecryptor.DecryptAll(builder.Configuration);
    builder.Configuration.AddInMemoryCollection(
        plainConn.Select(kv => new KeyValuePair<string, string?>($"ConnectionStrings:{kv.Key}", kv.Value)));

    // ── Serilog ────────────────────────────────────────────────────────────
    // Log file path is NFS-aware (see ResolveLogFilePath): centralized/persistent
    // NFS folder when StaticAssets:UseNfs=true, else pod-local "Logs/".
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(ResolveLogFilePath(ctx.Configuration), rollingInterval: RollingInterval.Day));

    // ── SignalR + Razor components (Blazor Server) ─────────────────────────
    builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = null);

    // ── Web API controllers (server-to-server integration endpoints) ───────
    builder.Services.AddControllers();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents(o =>
        {
            o.DetailedErrors = true;
            o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
        })
        .AddHubOptions(o =>
        {
            o.MaximumReceiveMessageSize = 1024 * 1024;
            // Server sends a keep-alive ping every 15s. The client's serverTimeout
            // (configured in App.razor) is 120s — an 8x margin so a delayed ping
            // (GC pause, slow DB call, network blip) never triggers a false
            // "Server timeout elapsed" disconnect on the client.
            o.KeepAliveInterval = TimeSpan.FromSeconds(15);
            // Server waits 2 minutes without any client message before dropping
            // the circuit. Must be >= the client keep-alive interval (15s) with margin.
            o.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
            o.HandshakeTimeout = TimeSpan.FromSeconds(30);
        });

    // ── UI libraries ────────────────────────────────────────────────────────
    builder.Services.AddMudServices();
    builder.Services.AddApexCharts();

    // ── Project B feature services (reflection auto-registration) ──────────
    builder.Services.AddApplicationServices();

    // ── Data providers (Project B raw ADO.NET providers, point at docker DBs) ─
    builder.Services.AddSingleton<OracleDbProvider>();
    builder.Services.AddSingleton<SqlDbProvider>();
    builder.Services.AddMemoryCache();

    // ── Response compression (Brotli/Gzip) — no-op on localhost's near-zero
    // latency/bandwidth, but a real win over an actual network (UAT/Production):
    // compresses the Blazor framework payload, MudBlazor CSS/JS, and any text
    // responses. EnableForHttps is required since every environment here uses TLS.
    builder.Services.AddResponseCompression(o =>
    {
        o.EnableForHttps = true;
        o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
        o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    });

    // ── Project C Dapper repositories (Oracle rp-oracle / SQL Server gap) ──
    builder.Services.AddScoped<ISessionRepository,        SessionRepository>();
    builder.Services.AddScoped<IUserRepository,           UserRepository>();
    builder.Services.AddScoped<IRbacRepository,           RbacRepository>();
    builder.Services.AddScoped<INotificationRepository,   NotificationRepository>();
    builder.Services.AddScoped<IMomRepo, MomRepo>();
    builder.Services.AddScoped<IFeedbackRepo, FeedbackRepo>();
    builder.Services.AddScoped<IDbvService, DbvService>();
    builder.Services.AddScoped<IBranchFoundationService, BranchFoundationService>();


    builder.Services.AddScoped<IBackDatedDepositRepo,BackDatedDepositRepo>();
    builder.Services.AddScoped<IDmdRbacRepo,DmdRbacRepo>();
    builder.Services.AddScoped<IDmdRbacService,DmdRbacService>();
    builder.Services.AddScoped <IBackDatedDepositWorkflowService,BackDatedDepositWorkflowService>();

    // ── Project A Request Portal (Core + Data, unchanged query logic vs RP_* tables) ─
    builder.Services.AddRequestPortalCore();
    builder.Services.AddRequestPortalData();
    builder.Services.AddScoped<RequestPortal.Core.Abstractions.ICurrentUser,
                               RequestPortal.Web.Auth.CurrentUserService>();
    builder.Services.AddScoped<RequestPortal.Core.Abstractions.IUserDirectory,
                               RequestPortal.Web.Auth.UserDirectory>();
    builder.Services.AddScoped<RequestPortal.Web.Services.ModuleContext>();
    builder.Services.AddSingleton<RequestPortal.Web.Hubs.NotificationStream>();
    // Bridge A's email/SMS sender interfaces to C's unified MailKit senders
    builder.Services.AddScoped<RequestPortal.Core.Services.IEmailSender,
                               MyDiary.Web.RequestPortal.RequestPortalEmailSenderAdapter>();
    builder.Services.AddScoped<RequestPortal.Core.Services.ISmsSender,
                               MyDiary.Web.RequestPortal.RequestPortalSmsSenderAdapter>();

    builder.Services.AddScoped<RequestPortal.Core.Abstractions.INotificationPublisher,
                              MyDiary.Web.RequestPortal.InProcessNotificationPublisher>();

    //Commented for NotificationBell.razor on 21-07-2026
    //builder.Services.AddScoped<RequestPortal.Core.Abstractions.INotificationPublisher,
    //                           MyDiary.Web.RequestPortal.NoOpNotificationPublisher>();

    //Commented for NotificationBell.razor on 21-07-2026

    builder.Services.AddScoped<RequestPortal.Core.Abstractions.IBroadcastDirectory,
                               RequestPortal.Data.Repositories.SqlBroadcastDirectory>();
    // Attachments persist to the DB (RP_ATTACHMENT_BLOB) rather than the filesystem.
    builder.Services.AddScoped<RequestPortal.Core.Services.IAttachmentStore,
                               RequestPortal.Data.Repositories.DbAttachmentStore>();
    // In-memory page metadata (avoids dragging Oracle PageInfo dependency)
    builder.Services.AddScoped<RequestPortal.Web.Features.PageInfo.Services.IPageInfoService,
                               RequestPortal.Web.Features.PageInfo.Services.OraclePageInfoService>();
    //Deployment Pulse ITSM
    builder.Services.AddScoped<IDeploymentPulseService, DeploymentPulseService>();

    //Project Muskaan
    builder.Services.AddScoped<IProjectMuskaanService, ProjectMuskaanService>();

    // ── HTTP clients ────────────────────────────────────────────────────────
    builder.Services.AddHttpClient<UserApiClient>();
    builder.Services.AddHttpClient("CryptoService", c => c.Timeout = TimeSpan.FromSeconds(10));
    builder.Services.AddHttpClient();

    // ── Encryption (Project B — CryptoService delegate) ────────────────────
    builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
    // Access-control config (Enabled flag, allowed PF ids, allowed zones),
    // sourced from Oracle MyDiaryDB (MD_ACCESS_SETTING / MD_ALLOWED_PF / MD_ALLOWED_ZONE)
    builder.Services.AddScoped<MyDiary.Web.Features.Shared.Services.IAccessControlService,
                               MyDiary.Web.Features.Shared.Services.AccessControlService>();
    builder.Services.AddScoped<AuthApiService>();
    builder.Services.AddSingleton<MyDiary.Web.Services.EkamSsoService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Shared.Services.IVCardService, MyDiary.Web.Features.Shared.Services.VCardService>();

    // ── EoiSsoService ─────────────────────────────────
    builder.Services.AddSingleton<MyDiary.Web.Services.EoiSsoService>();

    // ── Union Hub (Corporate Events feed) ─────────────────────────────────
    builder.Services.AddScoped<RequestPortal.Core.Abstractions.IEventFeedRepo,
                               RequestPortal.Data.Repositories.EventFeedRepo>();
    builder.Services.AddScoped<RequestPortal.Core.Abstractions.IEventFeedService,
                               RequestPortal.Core.Services.EventFeedService>();
    builder.Services.AddSingleton<RequestPortal.Core.Abstractions.IEventMediaStore,
                                  MyDiary.Web.RequestPortal.Events.FileSystemEventMediaStore>();

    // ── MoM Developer (Phase 1) ────────────────────────────────────────────
    builder.Services.AddSingleton<RequestPortal.Core.Abstractions.IMomAttachmentStore,
                                  MyDiary.Web.RequestPortal.FileSystemMomAttachmentStore>();

    // ── Feedback Developer (Phase 1) ────────────────────────────────────────────
    builder.Services.AddSingleton<RequestPortal.Core.Abstractions.IFeedbackFileStore,
                                  MyDiary.Web.RequestPortal.FeedbackFileStore>();

    // ── Qlik Sense embedding (Capability API + QPS ticket) ────────────────
    builder.Services.Configure<MyDiary.Web.Features.QlikDashboard.QlikOptions>(builder.Configuration.GetSection("Qlik"));
    builder.Services.AddScoped<MyDiary.Web.Features.QlikDashboard.IQlikTicketService,
                               MyDiary.Web.Features.QlikDashboard.QlikTicketService>();

    // ── ECircular (merged from My_Diary_V2) ────────────────────────────────
    builder.Services.Configure<MyDiary.Web.Features.ECircular.Services.ECircularOptions>(
        builder.Configuration.GetSection("ECircular"));
    builder.Services.AddHttpClient("ECircularDms")
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MyDiary.Web.Features.ECircular.Services.ECircularOptions>>().Value;
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };

            if (o.AcceptAnyServerCertificate)
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            return handler;
        })
        .ConfigureHttpClient((sp, client) =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MyDiary.Web.Features.ECircular.Services.ECircularOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(o.HttpRequestTimeoutSeconds);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/138.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "*/*");
        });

    // ── PdfSharpCore: Register custom font resolver so Arial is found on Linux/K8s ──
    // This reads the TTF files from wwwroot/css/fonts at startup. Without this,
    // PdfSharpCore relies on OS font infrastructure which doesn't exist in Alpine/Debian containers.
    {
        var fontsPath = Path.Combine(builder.Environment.WebRootPath, "css", "fonts");
        PdfSharpCore.Fonts.GlobalFontSettings.FontResolver =
            new MyDiary.Web.Features.ECircular.Services.ArialFontResolver(fontsPath);
    }

    // ── Procurement / ATM Indent (merged from My_Diary_V2) ─────────────────
    builder.Services.AddScoped<MyDiary.Web.Features.Procurement.Interfaces.IATMIndentService,
                               MyDiary.Web.Features.Procurement.Services.ATMIndentService>();
    builder.Services.AddDbContext<MyDiary.Web.Features.Procurement.Data.ProcurementDbContext>(o =>
        o.UseOracle(builder.Configuration.GetConnectionString("ProcurementConnection")));

    // ── Email / SMS (MailKit, migrated from Project A) ─────────────────────
    builder.Services.Configure<MyDiary.Web.Notify.SmtpOptions>(builder.Configuration.GetSection("Smtp"));
    //builder.Services.AddScoped<IEmailSender, MyDiary.Web.Notify.SmtpEmailSender>();
    //builder.Services.AddScoped<ISmsSender,   MyDiary.Web.Notify.NoOpSmsSender>();
    builder.Services.AddScoped< MyDiary.Core.Abstractions.IEmailSender,MyDiary.Web.Notify.SmtpEmailSender>();

    builder.Services.AddScoped< MyDiary.Core.Abstractions.ISmsSender, MyDiary.Web.Notify.NoOpSmsSender>();

    // ── Blazor circuit options ───────────────────────────────────────────────
    builder.Services.Configure<CircuitOptions>(o =>
    {
        o.DetailedErrors                       = builder.Environment.IsDevelopment();
        o.DisconnectedCircuitRetentionPeriod   = TimeSpan.FromMinutes(20);
        o.DisconnectedCircuitMaxRetained       = 1000;
        o.JSInteropDefaultCallTimeout          = TimeSpan.FromSeconds(60);
        // Default is 10. At 1000 concurrent circuits, a slow/flaky client that stops
        // acknowledging render batches (e.g. a mobile network hiccup) will otherwise
        // let the server keep buffering unacknowledged batches in memory indefinitely
        // until it disconnects the circuit — this caps that per-circuit buffer.
        o.MaxBufferedUnacknowledgedRenderBatches = 10;
    });

    // ── Authorization (unified policies A + B) ──────────────────────────────
    builder.Services.AddAuthorization(MyDiary.Web.Auth.AuthorizationPolicies.AddMyDiaryAuthorization);

    // ── Authentication (Project B form login + session storage, RBAC-enriched) ─
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(o =>
        {
            o.LoginPath          = "/login";
            o.ExpireTimeSpan     = TimeSpan.FromHours(24);
            o.SlidingExpiration  = true;
            o.Cookie.HttpOnly    = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite    = SameSiteMode.Strict;
        })
        // Server-to-server API key scheme (UCCRMC etc.) — used by /api/* controllers
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                   MyDiary.Web.Auth.ApiKeyAuthenticationHandler>(
            MyDiary.Web.Auth.ApiKeyAuthenticationHandler.SchemeName, null);

    builder.Services.AddCascadingAuthenticationState();

    // ── Data Protection key ring ─────────────────────────────────────────────
    // MUST be persisted to shared storage in any multi-instance deployment
    // (K8s replicas, an IIS farm, or even a single IIS box across AppPool
    // recycles) — this key ring is what encrypts the auth cookie and the
    // attachment-link tokens (AttachmentLinkSigner/MomAttachmentLinkSigner).
    // Without a shared/persistent store, AddDataProtection() defaults to an
    // ephemeral, per-process key ring: any request that lands on a different
    // instance (or the same instance after a restart) can no longer decrypt
    // a cookie/token issued before that point, which the app treats as
    // "not authenticated" — this silently kills the user's session/SignalR
    // circuit and is the most likely cause of the prod-only disconnects seen
    // on both K8s (3 pods) and IIS.
    //
    // Preferred: persist to the RP_OWNER Oracle DB (table RP_DATAPROTECTION_KEYS,
    // see OracleXmlRepository.cs) — every pod/instance already connects to this
    // same database (it's also Hangfire's job-storage backend below), so this
    // gives every instance a shared key ring with ZERO extra infrastructure —
    // no PVC, no UNC share, no IIS-console changes needed. Falls back to
    // "DataProtection:KeysPath" (a shared file path) if explicitly configured,
    // or ephemeral per-process storage (with a loud warning) if neither is set.
    var dataProtectionBuilder = builder.Services.AddDataProtection()
        .SetApplicationName("MyDiaryV3");
    var dataProtectionKeysPath = builder.Configuration.GetValue<string>("DataProtection:KeysPath");
    if (plainConn.TryGetValue("RP_Owner", out var rpOwnerConn) && !string.IsNullOrWhiteSpace(rpOwnerConn))
    {
        dataProtectionBuilder.AddKeyManagementOptions(o =>
            o.XmlRepository = new MyDiary.Web.Auth.OracleXmlRepository(rpOwnerConn));
        Log.Information("Data Protection keys persisted to RP_OWNER (RP_DATAPROTECTION_KEYS) — " +
                         "shared across all instances via the existing database, no extra infra needed.");
    }
    else if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
    {
        Directory.CreateDirectory(dataProtectionKeysPath);
        dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
        Log.Information("Data Protection keys persisted to {KeysPath}", dataProtectionKeysPath);
    }
    else
    {
        Log.Warning("Data Protection keys have no shared persistence configured (no RP_Owner " +
                    "connection string, no DataProtection:KeysPath) — using ephemeral, per-process " +
                    "key storage. In any deployment with more than one instance (K8s replicas, " +
                    "an IIS farm) or across process restarts, this WILL cause auth cookies and " +
                    "circuits issued by one instance to silently fail on another, producing " +
                    "exactly the kind of session/SignalR disconnects seen in production.");
    }

    builder.Services.AddScoped<CustomAuthenticationStateProvider>();
    builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
        sp.GetRequiredService<CustomAuthenticationStateProvider>());
    builder.Services.AddScoped<
    RequestPortal.Core.Abstractions.ICurrentUser,
    RequestPortal.Web.Auth.CurrentUserService>();
    builder.Services.AddScoped<IITKonnectService, ITKonnectService>();

    builder.Services.AddScoped<
        MyDiary.Core.Abstractions.ICurrentUser,
        MyDiary.Web.Auth.CurrentUser>();

    // SSO + session circuit handler (Project B)
    builder.Services.AddScoped<MyDiary.Web.Core.Services.Authentication.SSOService>();

    // Session storage: plain (load testing, Development only) or AES-encrypted (default)
    if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DevSettings:LoadTestingMode"))
        builder.Services.AddScoped<ISessionStorageService, PlainSessionStorageService>();
    else
        builder.Services.AddScoped<ISessionStorageService, ProtectedSessionStorageService>();

    builder.Services.AddHttpContextAccessor();

    // DevSettings singleton (Project B)
    var devSettings = builder.Configuration.GetSection("DevSettings").Get<DevSettings>();
    builder.Services.AddSingleton(devSettings ?? new DevSettings());

    // ── Shared state services (Project B) ──────────────────────────────────
    builder.Services.AddScoped<ReportStateService>();
    builder.Services.AddScoped<NavState>();
    builder.Services.AddScoped<MyDiary.Web.Services.AppModuleNav>();

    // ── Concrete feature services not caught by reflection auto-registration ─
    // (these have no matching I*Service interface, so register explicitly — from Project B's Program.cs)
    builder.Services.AddScoped<MyDiary.Web.Features.Shared.Services.DynamicReportService>();
    builder.Services.AddScoped<IDashboardsService, DashboardsServices>();
    builder.Services.AddScoped<MyDiary.Web.Features.Shared.Services.BreadcrumbState>();
    builder.Services.AddScoped<MyDiary.Web.Features.Shared.Services.LanguageState>();
    builder.Services.AddScoped<MyDiary.Web.Features.ReportModule.Services.ReportExecutionService>();
    builder.Services.AddScoped<MyDiary.Web.Features.ReportModule.Services.ReportExportService>();
    builder.Services.AddSingleton<MyDiary.Web.Features.Shared.Services.IInlineComponentResolver,
                                  MyDiary.Web.Features.Shared.Services.InlineComponentResolver>();
    builder.Services.AddScoped<MyDiary.Web.Features.Reports.Services.DCFEMenuReportService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Reports.Services.ChequeIssuedReportService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Reports.Services.SundryReportService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Reports.Services.DeathClaimReportService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Reports.Services.ATRReportService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Shared.Services.AtrMonthlyReportService>();
    builder.Services.AddScoped<ChargeReportPdfService>();

    // ── Landing page carousel (DB-driven images) ──────────────────────────
    builder.Services.AddScoped<MyDiary.Web.Features.Landing.Services.ILandingCarouselService,
                               MyDiary.Web.Features.Landing.Services.LandingCarouselService>();

    // ── Focus 360 (point at gap-sqlserver via GapReportSettings) ───────────
    builder.Services.Configure<GapReportSettings>(builder.Configuration.GetSection("Focus360"));
    builder.Services.AddScoped<IGapService, SqlGapService>();
    builder.Services.AddScoped<IRoVisitRepo, RoVisitRepo>();
    builder.Services.AddScoped<GapExportService>();
    builder.Services.AddScoped<AssurancePanelService>();
    //builder.Services.AddScoped<MyDiary.Web.Features.Business.Services.IBusiness360DashboardService,
    //    MyDiary.Web.Features.Business.Services.Business360DashboardService>();
    //builder.Services.AddScoped<MyDiary.Web.Features.Business.Services.CommandCenterPresentationService>();
    // Assurance Snapshot reuses IGapService / GapReportSettings (same tables as Focus 360)
    // for the branch header, plus its own data-driven panel grid (usp_Assurance_GetPanelGrid —
    // see db/sqlserver/GAP_04_assurance_panels.sql) for the report body.
    builder.Services.AddScoped<MyDiary.Web.Features.Assurance.Services.AssuranceExportService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Assurance.Services.AssurancePanelService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Assurance.Services.AssurancePanelService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Assurance.Services.RoVisitMetricsService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Assurance.Services.RoVisitPdfService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Assurance.Services.ExecBranchVisitMetricsService>();
    builder.Services.AddScoped<MyDiary.Web.Features.Assurance.Services.ExecBranchVisitPdfService>();

    // Service for Red Flagged Account
    builder.Services.AddScoped<IRFAService, RFAService>();

    // Feedback Profile Service — reads employee identity/org fields from
    // AuthState/CustomAuthState (same source as CurrentUser), replacing the mock.
    builder.Services.AddScoped<IEmployeeProfileService, EmployeeProfileService>();

    // Register - IUnionHubVideoLaunchService
    builder.Services.AddScoped<MyDiary.Web.Features.UnionHubVideoLaunch.Services.IUnionHubVideoLaunchService, MyDiary.Web.Features.UnionHubVideoLaunch.Services.UnionHubVideoLaunchService>();

    // Register - IPositiveBranchListService
    builder.Services.AddScoped<MyDiary.Web.Features.PositiveBranchList.Services.IPositiveBranchListService, MyDiary.Web.Features.PositiveBranchList.Services.PositiveBranchListService>();

    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    // ── Hangfire (notification dispatch) — durable Oracle storage ──────────
    // Tables (HANGFIRE_*) are created under the Oracle connection's schema
    // (RP_OWNER) on first run. Queued jobs now survive app restarts.
    var hangfireCs = builder.Configuration.GetConnectionString("RP_Owner")
        ?? throw new InvalidOperationException("ConnectionStrings:Oracle is required for Hangfire storage.");
    var hangfireStorage = new Hangfire.Oracle.Core.OracleStorage(hangfireCs,
        new Hangfire.Oracle.Core.OracleStorageOptions
        {
            PrepareSchemaIfNecessary = true,
            // Longer poll interval → far fewer UPDATE HF_JOB_QUEUE / lock round-trips.
            // Our jobs are minute-granularity at finest (notification dispatch) plus
            // two nightly jobs, so polling every 60s instead of 15s cuts the queue
            // poller's connection churn ~4x with no meaningful latency cost.
            QueuePollInterval = TimeSpan.FromSeconds(60),
            InvisibilityTimeout      = TimeSpan.FromMinutes(30),
        });
    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseStorage(hangfireStorage));
    // The default AddHangfireServer() uses WorkerCount = ProcessorCount * 5 (20–40+
    // workers on a multi-core pod) and runs on EVERY replica. Each worker plus the
    // heartbeat, scheduler and queue poller opens its own Oracle storage connection,
    // so the community Oracle provider ends up holding 100+ sessions per pod for
    // HF_JOB_QUEUE / HF_SERVER / HF_DISTRIBUTED_LOCK housekeeping — far more than this
    // light workload (1 dispatch/min + 2 nightly jobs) needs. Cap the worker count and
    // scope to the default queue so the server opens only a handful of connections.
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 2;
        options.Queues = new[] { "default" };
        // Slower background loops = fewer periodic DELETE HF_SERVER (heartbeat) and
        // DELETE HF_DISTRIBUTED_LOCK round-trips. Defaults are aggressive (heartbeat
        // ~30s, server-check ~1min); relax them for a low-throughput job set.
        options.HeartbeatInterval = TimeSpan.FromMinutes(2);
        options.ServerCheckInterval = TimeSpan.FromMinutes(10);
        options.SchedulePollingInterval = TimeSpan.FromMinutes(1);
    });

    builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = true; });

    // ── NFS static asset configuration (K8s deployments) ──────────────────
    builder.Services.AddStaticAssetConfiguration(builder.Configuration);

    builder.Services.AddHttpClient("VisitingCardGateway", c => c.Timeout = TimeSpan.FromSeconds(15));
    builder.Services.AddHttpClient("WhatsAppGateway", c => c.Timeout = TimeSpan.FromSeconds(15));

    var app = builder.Build();

    // ── Static helpers (Project B) ──────────────────────────────────────────
    AppLogger.Initialize(app.Services);
    MyDiary.Web.Services.AdApiCrypto.CryptoServiceUrl = app.Configuration["ApiEndpoints:CryptoServiceUrl"]?.TrimEnd('/');
    MyDiary.Web.Services.AdApiCrypto.HttpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
    MyDiary.Web.Services.AdApiCrypto.Logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AdApiCrypto");

    // ── Middleware pipeline ────────────────────────────────────────────────

    // Must run first — before anything that inspects Request.Scheme/RemoteIp
    // (UseHsts/UseHttpsRedirection, the Secure-cookie policy below, auth).
    // Behind a reverse proxy (K8s ingress, IIS ARR/farm LB) that terminates
    // TLS and forwards plain HTTP internally, the app otherwise sees every
    // request as insecure HTTP — which, combined with the auth cookie's
    // Cookie.SecurePolicy = Always, means the browser is told not to send/
    // store the cookie at all, or the app redirect-loops through
    // UseHttpsRedirection. ForwardedHeaders trusts the proxy's
    // X-Forwarded-Proto/X-Forwarded-For so the app sees the real scheme.
    //
    // KnownProxies/KnownNetworks are intentionally left empty (== trust any
    // proxy) because the actual proxy IPs vary per environment (K8s ingress
    // pod IP, IIS ARR box) and change across deployments; this is safe only
    // because the app is not directly internet-reachable — it always sits
    // behind that proxy. If that assumption changes, lock this down to the
    // known proxy IP ranges instead.
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();

    // Security headers: mitigate clickjacking/MIME-sniffing/referrer leakage.
    // Added by security audit — no CSP here (Blazor Server + MudBlazor use
    // inline styles/scripts extensively; a strict CSP would need broad,
    // page-by-page testing to avoid breaking the UI, out of scope for this pass).
    app.Use(async (ctx, next) =>
    {
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        await next();
    });

    // PathBase — required when hosted behind IIS at a sub-path (e.g. /My_Diary_V3)
    var pathBase = app.Configuration.GetValue<string>("AppSettings:PathBase");
    if (!string.IsNullOrEmpty(pathBase) && pathBase != "/")
        app.UsePathBase(pathBase);

    app.UseCookiePolicy(new CookiePolicyOptions
    {
        HttpOnly              = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always,
        Secure                = CookieSecurePolicy.Always,
        MinimumSameSitePolicy = SameSiteMode.Strict
    });

    // Must run before UseStaticFiles so static content (images/CSS/JS) is also compressed.
    app.UseResponseCompression();

    // Rewrite legacy asset paths (DB stores /download but files are at /MyDiary/Downloads)
    app.UseStaticAssetPathRewrite();
    app.UseStaticFilesWithNfs();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    if (app.Environment.IsDevelopment())
        app.UseHangfireDashboard("/hangfire");

    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<MyDiary.Web.Jobs.NotificationDispatchJob>(
        "md.notif.dispatch",
        j => j.RunAsync(CancellationToken.None),
        Cron.MinuteInterval(1));

    // Hangfire evaluates cron in UTC by default. Pin these to IST so "2 AM" and
    // "9 AM" mean Indian wall-clock time on a UTC Kubernetes pod (matching the
    // old IST-configured IIS behaviour), and so they line up with the IST day
    // boundary the jobs themselves now use (AppTime.Today).
    var istRecurringOptions = new Hangfire.RecurringJobOptions
    {
        TimeZone = MyDiary.Core.Services.AppTime.IndiaTimeZone
    };

    // ── Adoption Dashboard — nightly usage/login/report-gen aggregation + underused-page ranking ──
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<MyDiary.Web.Jobs.PageUsageAggregationJob>(
        "rp.page.usage.aggregation",
        j => j.RunAsync(CancellationToken.None),
        "0 2 * * *", // 2 AM IST
        istRecurringOptions);

    // ── Adoption Dashboard — 9 AM email digest to the configured recipient list ──
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<MyDiary.Web.Jobs.AdoptionDigestEmailJob>(
        "rp.adoption.digest.email",
        j => j.RunAsync(CancellationToken.None),
        "0 9 * * *", // 9 AM IST
        istRecurringOptions); // 9 AM server time

    // ── Assurance Corner: Positive Branch List — 10:30 AM daily aggregate + zone automailer.
    // Self-reschedules an hourly retry (see PositiveBranchListDailyJob) if T-1's data isn't in yet.
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<MyDiary.Web.Jobs.PositiveBranchListDailyJob>(
        "rp.pbl.daily.mail",
        j => j.RunAsync(CancellationToken.None),
        "30 10 * * *"); // 10:30 AM server time

    // ── CS&BE Monthly Information Notes reminders ──────────────────────────
    //app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<MyDiary.Web.Jobs.CsbeMonthlyReminderJob>(
    //    "csbe.monthly.upload.reminder",
    //    j => j.MonthlyUploadReminderAsync(CancellationToken.None),
    //    "0 8 1 * *");

    //app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<MyDiary.Web.Jobs.CsbeMonthlyReminderJob>(
    //    "csbe.admin.exception.reminder",
    //    j => j.AdminExceptionReminderAsync(CancellationToken.None),
    //    "0 8 10 * *");

    app.MapControllers();

    app.MapRazorComponents<App>()
       .AddInteractiveServerRenderMode();

    app.MapHub<MyDiary.Web.Hubs.NotificationHub>("/hubs/notifications");
    MyDiary.Web.Features.StrikeCorner.StrikeCornerEndpoints.MapStrikeCorner(app);
    MyDiary.Web.Features.SubsidiaryJv.SjvEndpoints.MapSjv(app);           

    Log.Information("My Diary V3 started");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    Log.CloseAndFlush();
}
