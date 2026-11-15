using ApiTUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.ConfigureContainer(new DefaultServiceProviderFactory(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
builder.Services.AddApiTui();

using var host = builder.Build();
await host.StartAsync(CancellationToken.None);
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

try
{
    await host.Services.GetRequiredService<ApiTuiApp>().RunAsync(lifetime.ApplicationStopping);
}
catch (OperationCanceledException) when (lifetime.ApplicationStopping.IsCancellationRequested)
{
    // La cancelación solicitada al host es una salida normal.
}
finally
{
    await host.StopAsync(CancellationToken.None);
}
