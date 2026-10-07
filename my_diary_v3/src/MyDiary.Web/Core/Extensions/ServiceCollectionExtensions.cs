using System.Reflection;

namespace MyDiary.Web.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // 1. Get the current assembly (the project we are in)
            var assembly = Assembly.GetExecutingAssembly();

            // 2. Find all classes that end with "Service" and are not abstract
            var serviceTypes = assembly.GetTypes()
                .Where(t => t.Name.EndsWith("Service") && t.IsClass && !t.IsAbstract)
                .ToList();

            foreach (var type in serviceTypes)
            {
                // 3. Find the matching interface (e.g., DashboardService -> IDashboardService)
                // Assumption: The interface has the same name but starts with 'I'
                var interfaceType = type.GetInterface($"I{type.Name}");

                if (interfaceType != null)
                {
                    // 4. Register them automatically as Scoped
                    services.AddScoped(interfaceType, type);
                }
            }

            return services;
        }
    }
}