using ApiTUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.ConfigureContainer(new DefaultServiceProviderFactory(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
builder.Services.AddSingleton<IHello, Hello>();
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

class JsonPrueba { }

[JsonSerializable(typeof(JsonPrueba))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
internal partial class ApiTuiJsonContext : JsonSerializerContext;
