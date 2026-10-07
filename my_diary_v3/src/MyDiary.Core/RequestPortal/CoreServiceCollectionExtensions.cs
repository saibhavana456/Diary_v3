using Microsoft.Extensions.DependencyInjection;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Services;

namespace RequestPortal.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddRequestPortalCore(this IServiceCollection services)
    {
        services.AddScoped<ISlaCalculator, SlaCalculator>();
        services.AddScoped<IRoutingService, RoutingService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationApi, NotificationApiService>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<IUccrmcService, UccrmcService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IRbacService, RbacService>();
        services.AddScoped<ICcpRbacService, CcpRbacService>();
        services.AddSingleton<ITemplateRenderer, ScribanRenderer>();
        return services;
    }
}
