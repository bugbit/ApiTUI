using ApiTUI.Application.Ports;
using ApiTUI.Application.SendApiRequest;
using ApiTUI.Common;
using ApiTUI.Domain;
using ApiTUI.Localization;

namespace ApiTUI;

// Prueba provisional por consola del caso de uso de envío, hasta que exista la TUI:
// hace un GET a una API pública de ejemplo y muestra el resultado.
public sealed class ApiTuiApp(SendApiRequestUseCase sendApiRequest, IIdGenerator idGenerator, Messages messages)
{
    public const string SampleUrl = "https://jsonplaceholder.typicode.com/todos/1";

    private readonly SendApiRequestUseCase _sendApiRequest = sendApiRequest;
    private readonly IIdGenerator _idGenerator = idGenerator;
    private readonly Messages _messages = messages;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = CreateSampleRequest();
        if (!request.IsSuccess)
        {
            WriteError(request.Error);
            return;
        }

        Console.WriteLine(_messages.Get(UiMessageKeys.Request, $"{request.Value.Method.ToString().ToUpperInvariant()} {SampleUrl}"));
        var result = await _sendApiRequest.ExecuteAsync(request.Value, cancellationToken);
        if (!result.IsSuccess)
        {
            WriteError(result.Error);
            return;
        }

        var response = result.Value;
        Console.WriteLine(_messages.Get(UiMessageKeys.ResponseStatus, $"{response.StatusCode} {response.ReasonPhrase}".TrimEnd()));
        Console.WriteLine(_messages.Get(UiMessageKeys.ResponseDuration, (long)response.Duration.TotalMilliseconds));
        Console.WriteLine(_messages.Get(UiMessageKeys.ResponseHeaders));
        foreach (var (name, value) in response.Headers)
        {
            Console.WriteLine($"  {name}: {value}");
        }

        Console.WriteLine(_messages.Get(UiMessageKeys.ResponseBody));
        Console.WriteLine(response.Body);
    }

    private Result<ApiRequest> CreateSampleRequest()
    {
        var requestId = _idGenerator.NextApiRequestId();
        if (!requestId.IsSuccess)
        {
            return Result<ApiRequest>.Failure(requestId.Error);
        }

        var collectionId = _idGenerator.NextApiCollectionId();
        if (!collectionId.IsSuccess)
        {
            return Result<ApiRequest>.Failure(collectionId.Error);
        }

        var acceptName = HeaderName.Create("Accept");
        if (!acceptName.IsSuccess)
        {
            return Result<ApiRequest>.Failure(acceptName.Error);
        }

        var accept = HeaderValue.Create(acceptName.Value, new StringWithVars("application/json"));
        if (!accept.IsSuccess)
        {
            return Result<ApiRequest>.Failure(accept.Error);
        }

        return ApiRequest.CreateInCollection(requestId.Value, collectionId.Value, "Prueba GET", RequestMethod.Get,
            new StringWithVars(SampleUrl), [accept.Value]);
    }

    private void WriteError(Error error) => Console.WriteLine(_messages.Get(UiMessageKeys.RequestFailed, _messages.Format(error)));
}
