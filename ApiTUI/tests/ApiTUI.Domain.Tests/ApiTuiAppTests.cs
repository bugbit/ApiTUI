using ApiTUI.Adapters.Identity;
using ApiTUI.Adapters.Persistence;
using ApiTUI.Application;
using ApiTUI.Application.Ports;
using ApiTUI.Application.SendApiRequest;
using ApiTUI.Common;
using ApiTUI.Localization;

namespace ApiTUI.Domain.Tests;

// Redirige Console.Out: comparte la colección sin paralelismo con los demás tests que tocan estado global.
[Collection("Language configuration")]
public sealed class ApiTuiAppTests
{
    [Fact]
    public async Task Run_SuccessfulResponse_SendsLoginAndPrintsResult()
    {
        var sender = new FakeHttpSender(new HttpResponseData(200, "OK",
            [new("Content-Type", "application/json")], """{"token":"abc"}"""));

        var output = await RunAsync(sender, "es");

        var request = Assert.Single(sender.Requests);
        Assert.Equal(RequestMethod.Post, request.Method);
        Assert.Equal(new Uri("http://localhost:5080/login"), request.Url);
        Assert.Equal(
            [new KeyValuePair<string, string>("Accept", "application/json"), new("Content-Type", "application/json")],
            request.Headers);
        Assert.Equal("""{"username":"user2","password":"user2"}""", request.Body);
        Assert.Contains("Petición: POST http://localhost:5080/login", output);
        Assert.Contains("Estado: 200 OK", output);
        Assert.Contains("Duración: ", output);
        Assert.Contains("  Content-Type: application/json", output);
        Assert.Contains("""{"token":"abc"}""", output);
        Assert.EndsWith($"Variable {ApiTuiApp.TokenVarName} = abc{Environment.NewLine}", output);
    }

    [Theory]
    [InlineData("es", "Error al extraer token: No se ha encontrado la ruta JSON «token» en la respuesta.")]
    [InlineData("en", "Failed to extract token: JSON path 'token' was not found in the response.")]
    public async Task Run_ResponseWithoutToken_PrintsTranslatedVarOutErrorAndEmptyVariable(string language, string error)
    {
        var sender = new FakeHttpSender(new HttpResponseData(200, "OK", [], "{}"));

        var output = await RunAsync(sender, language);

        Assert.EndsWith($"{error}{Environment.NewLine}Variable {ApiTuiApp.TokenVarName} = {Environment.NewLine}", output);
    }

    [Fact]
    public async Task Run_Failure_PrintsTranslatedError()
    {
        var sender = new FakeHttpSender(new Error(ErrorsReasons.HttpFailure,
            ApplicationErrorCodes.HttpRequestFailed, "NameResolutionError"));

        var output = await RunAsync(sender, "en");

        Assert.Contains($"Request: POST {ApiTuiApp.LoginUrl}", output);
        Assert.Contains("Error: ", output);
        Assert.Contains("NameResolutionError", output);
        Assert.DoesNotContain("Status:", output);
        Assert.DoesNotContain($"Variable {ApiTuiApp.TokenVarName} =", output);
    }

    [Fact]
    public async Task Run_Cancelled_PropagatesCancellation()
    {
        var sender = new FakeHttpSender(new HttpResponseData(200, "OK", [], ""));
        var app = CreateApp(sender, "es");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => app.RunAsync(new CancellationToken(true)));
        Assert.Empty(sender.Requests);
    }

    private static async Task<string> RunAsync(FakeHttpSender sender, string language)
    {
        var app = CreateApp(sender, language);
        var originalOut = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            await app.RunAsync(CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return output.ToString();
    }

    private static ApiTuiApp CreateApp(IHttpSender sender, string language)
    {
        var store = new InMemoryApiStore();
        var useCase = new SendApiRequestUseCase(sender, TimeProvider.System, store, store, store, store, store, store);
        return new ApiTuiApp(useCase, new GuidIdGenerator(), new Messages(language), store);
    }

    private sealed class FakeHttpSender(Result<HttpResponseData> result) : IHttpSender
    {
        private readonly Result<HttpResponseData> _result = result;

        public List<HttpRequestData> Requests { get; } = [];

        public Task<Result<HttpResponseData>> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_result);
        }
    }
}
