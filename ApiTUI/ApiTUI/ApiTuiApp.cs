using ApiTUI.Adapters.Persistence;
using ApiTUI.Application.Ports;
using ApiTUI.Application.SendApiRequest;
using ApiTUI.Common;
using ApiTUI.Domain;
using ApiTUI.Localization;

namespace ApiTUI;

// Prueba provisional por consola del caso de uso de envío.
// Hace login contra la TodoApi local de ApiTUI/samples/TodoApi, que debe estar arrancada.
public sealed class ApiTuiApp(SendApiRequestUseCase sendApiRequest, IIdGenerator idGenerator, Messages messages, InMemoryApiStore store)
{
    public const string BaseUrl = "http://localhost:5080";
    public const string LoginUrl = BaseUrl + "/login";
    public const string LoginBody = """{"username":"user2","password":"user2"}""";

    private readonly SendApiRequestUseCase _sendApiRequest = sendApiRequest;
    private readonly IIdGenerator _idGenerator = idGenerator;
    private readonly Messages _messages = messages;
    private readonly InMemoryApiStore _store = store;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = CreateSampleRequest();
        if (!request.IsSuccess)
        {
            WriteError(request.Error);
            return;
        }

        Console.WriteLine(_messages.Get(UiMessageKeys.Request, $"{request.Value.Method.ToString().ToUpperInvariant()} {LoginUrl}"));
        var result = await _sendApiRequest.ExecuteAsync(request.Value, null, cancellationToken);
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

        // La URL se guarda como variable de la colección para probar la resolución de {{url}} al enviar.
        var urlName = VarName.Create("url");
        if (!urlName.IsSuccess)
        {
            return Result<ApiRequest>.Failure(urlName.Error);
        }

        var urlVariable = VarValue.Create(urlName.Value, new StringWithVars(BaseUrl));
        if (!urlVariable.IsSuccess)
        {
            return Result<ApiRequest>.Failure(urlVariable.Error);
        }

        var collection = ApiCollection.Create(collectionId.Value, "Demo", [urlVariable.Value]);
        if (!collection.IsSuccess)
        {
            return Result<ApiRequest>.Failure(collection.Error);
        }

        _store.Add(collection.Value);

        var accept = CreateJsonHeader("Accept");
        if (!accept.IsSuccess)
        {
            return Result<ApiRequest>.Failure(accept.Error);
        }

        var contentType = CreateJsonHeader("Content-Type");
        if (!contentType.IsSuccess)
        {
            return Result<ApiRequest>.Failure(contentType.Error);
        }

        return ApiRequest.CreateInCollection(requestId.Value, collectionId.Value, "Login", RequestMethod.Post,
            new StringWithVars("{{url}}/login"), [accept.Value, contentType.Value], new StringWithVars(LoginBody));
    }

    private static Result<HeaderValue> CreateJsonHeader(string name)
    {
        var headerName = HeaderName.Create(name);
        return headerName.IsSuccess
            ? HeaderValue.Create(headerName.Value, new StringWithVars("application/json"))
            : Result<HeaderValue>.Failure(headerName.Error);
    }

    private void WriteError(Error error) => Console.WriteLine(_messages.Get(UiMessageKeys.RequestFailed, _messages.Format(error)));
}
