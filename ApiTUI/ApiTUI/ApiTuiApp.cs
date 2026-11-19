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

        var requestResult = CreateSampleRequest();
        if (requestResult is Error requestError)
        {
            WriteError(requestError);
            return;
        }

        var request = (ApiRequest)requestResult.Value;
        Console.WriteLine(_messages.Get(UiMessageKeys.Request, $"{request.Method.ToString().ToUpperInvariant()} {LoginUrl}"));
        var result = await _sendApiRequest.ExecuteAsync(request, null, cancellationToken);
        if (result is Error resultError)
        {
            WriteError(resultError);
            return;
        }

        var response = (ApiResponse)result.Value;
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
        if (requestId is Error requestIdError)
        {
            return requestIdError;
        }

        var collectionId = _idGenerator.NextApiCollectionId();
        if (collectionId is Error collectionIdError)
        {
            return collectionIdError;
        }

        // La URL se guarda como variable de la colección para probar la resolución de {{url}} al enviar.
        var urlName = VarName.Create("url");
        if (urlName is Error urlNameError)
        {
            return urlNameError;
        }

        var urlVariable = VarValue.Create((VarName)urlName.Value, new StringWithVars(BaseUrl));
        if (urlVariable is Error urlVariableError)
        {
            return urlVariableError;
        }

        var collection = ApiCollection.Create((ApiCollectionId)collectionId.Value, "Demo", [(VarValue)urlVariable.Value]);
        if (collection is Error collectionError)
        {
            return collectionError;
        }

        _store.Add((ApiCollection)collection.Value);

        var accept = CreateJsonHeader("Accept");
        if (accept is Error acceptError)
        {
            return acceptError;
        }

        var contentType = CreateJsonHeader("Content-Type");
        if (contentType is Error contentTypeError)
        {
            return contentTypeError;
        }

        return ApiRequest.CreateInCollection((ApiRequestId)requestId.Value, (ApiCollectionId)collectionId.Value, "Login",
            RequestMethod.Post, new StringWithVars("{{url}}/login"), [(HeaderValue)accept.Value, (HeaderValue)contentType.Value], new StringWithVars(LoginBody));
    }

    private static Result<HeaderValue> CreateJsonHeader(string name)
    {
        return HeaderName.Create(name) switch
        {
            Error error => error,
            HeaderName headerName => HeaderValue.Create(headerName, new StringWithVars("application/json")),
        };
    }

    private void WriteError(Error error) => Console.WriteLine(_messages.Get(UiMessageKeys.RequestFailed, _messages.Format(error)));
}
