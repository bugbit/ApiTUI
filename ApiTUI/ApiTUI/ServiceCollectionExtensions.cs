using ApiTUI.Adapters.Identity;
using ApiTUI.Adapters.Http;
using ApiTUI.Application.Ports;
using ApiTUI.Application.SendApiRequest;
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
            .AddSingleton(TimeProvider.System)
            .AddSingleton(_ => new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) })
            {
                Timeout = Timeout.InfiniteTimeSpan,
            })
            .AddSingleton(new HttpSenderOptions(TimeSpan.FromSeconds(100)))
            .AddSingleton<IHttpSender, HttpClientSender>()
            .AddSingleton<SendApiRequestUseCase>()
            .AddSingleton<ApiTuiApp>();
    }
}
