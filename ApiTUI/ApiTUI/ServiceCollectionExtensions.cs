using ApiTUI.Adapters.Identity;
using ApiTUI.Application.Ports;
using ApiTUI.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTUI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiTui(this IServiceCollection services, string? language = null)
    {
        return services
            .AddSingleton(new Messages(language))
            .AddSingleton<IIdGenerator, GuidIdGenerator>()
            .AddSingleton<ApiTuiApp>();
    }
}
