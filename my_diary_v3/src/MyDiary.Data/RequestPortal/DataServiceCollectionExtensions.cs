using Microsoft.Extensions.DependencyInjection;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Services;
using RequestPortal.Data.Repositories;

namespace RequestPortal.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddRequestPortalData(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, OracleConnectionFactory>();
        services.AddSingleton<IOrganisationsDbFactory, OrganisationsConnectionFactory>();
        services.AddScoped<AdoUnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AdoUnitOfWork>());

        services.AddScoped<IMasterRepo, MasterRepo>();
        services.AddScoped<IUserRepo, UserRepo>();
        services.AddScoped<IRoutingRepo, RoutingRepo>();
        services.AddScoped<IRequestRepo, RequestRepo>();
        services.AddScoped<IAuditRepo, AuditRepo>();
        services.AddScoped<INotifRepo, NotifRepo>();
        services.AddScoped<INotificationRepo, NotificationRepo>();
        services.AddScoped<IAttachmentRepo, AttachmentRepo>();
        services.AddScoped<IRbacRepo, RbacRepo>();
        services.AddScoped<ICcpRbacRepo, CcpRbacRepo>();
        services.AddScoped<IPageUsageRepo, PageUsageRepo>();
        services.AddScoped<IEscalationMatrixRepo, EscalationMatrixRepo>();
        services.AddScoped<IClassificationMapRepo, ClassificationMapRepo>();
        services.AddScoped<IUccrmcRepo, UccrmcRepo>();
        services.AddScoped<IReportRepo, ReportRepo>();
        services.AddScoped<IMomRepo, MomRepo>();
        //services.AddScoped<ICsbeMonthlyNoteRepo, CsbeMonthlyNoteRepo>();
        //services.AddScoped<IOrgDataSource, OrgDataRepo>();
        //services.AddScoped<IRoVisitRepo, RoVisitRepo>();
        services.AddScoped<IExecBranchVisitRepo, ExecBranchVisitRepo>();

        return services;
    }
}
