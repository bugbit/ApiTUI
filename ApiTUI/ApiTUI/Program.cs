// Punto de entrada y composition root de ApiTUI.
// Configura el Generic Host sin proveedores de logging y con validación del contenedor DI.
// Registra IHello y los servicios de AddApiTui(), arranca el host y ejecuta ApiTuiApp
// con el token ApplicationStopping. Trata la cancelación como salida normal y detiene
// el host en finally cuando termina la aplicación.
// También declara ApiTuiJsonContext para generar la serialización JSON compatible con Native AOT.

using ApiTUI;
using ApiTUI.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using System.Globalization;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.ConfigureContainer(new DefaultServiceProviderFactory(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
builder.Services.AddSingleton<IHello, Hello>();
builder.Services.AddApiTui(builder.Configuration["Language"]);

using var host = builder.Build();
var messages = host.Services.GetRequiredService<Messages>();
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = messages.Culture;
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
