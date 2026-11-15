using Microsoft.Extensions.DependencyInjection;

namespace ApiTUI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiTui(this IServiceCollection services)
    {
        return services.AddSingleton<ApiTuiApp>();
    }
}
