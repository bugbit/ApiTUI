using ApiTUI.Adapters.Persistence;
using ApiTUI.Application.Ports;
using ApiTUI.Application.SendApiRequest;
using ApiTUI.Common;
using ApiTUI.Domain;
using ApiTUI.Localization;

namespace ApiTUI;

// Prueba provisional por consola del caso de uso de envío.
// Hace login y consulta las tareas en la TodoApi local de ApiTUI/samples/TodoApi.
public sealed class ApiTuiApp(SendApiRequestUseCase sendApiRequest, IIdGenerator idGenerator, Messages messages, InMemoryApiStore store)
{
    public const string BaseUrl = "http://localhost:5080";
    public const string LoginUrl = BaseUrl + "/login";
    public const string TasksUrl = BaseUrl + "/tasks";
    public const string LoginBody = """{"username":"user2","password":"user2"}""";
    public const string TokenVarName = "token";
    public const string TokenVarOut = "Bearer {{response.json.token}}";

    private readonly SendApiRequestUseCase _sendApiRequest = sendApiRequest;
    private readonly IIdGenerator _idGenerator = idGenerator;
    private readonly Messages _messages = messages;
    private readonly InMemoryApiStore _store = store;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sampleResult = PrepareSample();
        if (sampleResult is Error requestError)
        {
            WriteError(requestError);
            return;
        }

        var (login, tasks, environmentId) = (Sample)sampleResult.Value;
        Console.WriteLine(_messages.Get(UiMessageKeys.Request, $"POST {LoginUrl}"));
        var result = await _sendApiRequest.ExecuteAsync(login, environmentId, cancellationToken);
        if (result is Error resultError)
        {
            WriteError(resultError);
            return;
        }

        var response = (ApiResponse)result.Value;
        WriteResponse(response);

        foreach (var (name, error) in response.VarOutErrors)
        {
            Console.WriteLine(_messages.Get(UiMessageKeys.VarOutFailed, name.Value, _messages.Format(error)));
        }

        var environment = await _store.FindAsync(environmentId, cancellationToken);
        var token = environment?.Variables.FirstOrDefault(variable => variable.Name.Value == TokenVarName);
        Console.WriteLine(_messages.Get(UiMessageKeys.VariableValue, TokenVarName, token?.Value.Text ?? string.Empty));

        Console.WriteLine(_messages.Get(UiMessageKeys.Request, $"GET {TasksUrl}"));
        var tasksResult = await _sendApiRequest.ExecuteAsync(tasks, environmentId, cancellationToken);
        if (tasksResult is Error tasksError)
        {
            WriteError(tasksError);
            return;
        }

        WriteResponse((ApiResponse)tasksResult.Value);
    }

    private Result<Sample> PrepareSample()
    {
        var loginId = _idGenerator.NextApiRequestId();
        if (loginId is Error loginIdError)
        {
            return loginIdError;
        }

        var tasksId = _idGenerator.NextApiRequestId();
        if (tasksId is Error tasksIdError)
        {
            return tasksIdError;
        }

        var collectionId = _idGenerator.NextApiCollectionId();
        if (collectionId is Error collectionIdError)
        {
            return collectionIdError;
        }

        var environmentId = _idGenerator.NextApiEnvironmentId();
        if (environmentId is Error environmentIdError)
        {
            return environmentIdError;
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

        var tokenName = VarName.Create(TokenVarName);
        if (tokenName is Error tokenNameError)
        {
            return tokenNameError;
        }

        var tokenVariable = VarValue.Create((VarName)tokenName.Value, new StringWithVars(string.Empty));
        if (tokenVariable is Error tokenVariableError)
        {
            return tokenVariableError;
        }

        var environment = ApiEnvironment.Create((ApiEnvironmentId)environmentId.Value, "Demo", [(VarValue)tokenVariable.Value]);
        if (environment is Error environmentError)
        {
            return environmentError;
        }

        var tokenVarOut = VarValue.Create((VarName)tokenName.Value, new StringWithVars(TokenVarOut));
        if (tokenVarOut is Error tokenVarOutError)
        {
            return tokenVarOutError;
        }

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

        var login = ApiRequest.CreateInCollection((ApiRequestId)loginId.Value, (ApiCollectionId)collectionId.Value, "Login",
            RequestMethod.Post, new StringWithVars("{{url}}/login"), [(HeaderValue)accept.Value, (HeaderValue)contentType.Value],
            new StringWithVars(LoginBody), varsOut: [(VarValue)tokenVarOut.Value]);
        if (login is Error loginError)
        {
            return loginError;
        }

        var authorization = CreateHeader("Authorization", "{{token}}");
        if (authorization is Error authorizationError)
        {
            return authorizationError;
        }

        var tasks = ApiRequest.CreateInCollection((ApiRequestId)tasksId.Value, (ApiCollectionId)collectionId.Value, "Tasks",
            RequestMethod.Get, new StringWithVars("{{url}}/tasks"), [(HeaderValue)accept.Value, (HeaderValue)authorization.Value]);
        if (tasks is Error tasksError)
        {
            return tasksError;
        }

        _store.Add((ApiCollection)collection.Value);
        _store.Add((ApiEnvironment)environment.Value);
        return new Sample((ApiRequest)login.Value, (ApiRequest)tasks.Value, (ApiEnvironmentId)environmentId.Value);
    }

    private static Result<HeaderValue> CreateJsonHeader(string name) => CreateHeader(name, "application/json");

    private static Result<HeaderValue> CreateHeader(string name, string value)
    {
        return HeaderName.Create(name) switch
        {
            Error error => error,
            HeaderName headerName => HeaderValue.Create(headerName, new StringWithVars(value)),
        };
    }

    private void WriteResponse(ApiResponse response)
    {
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

    private void WriteError(Error error) => Console.WriteLine(_messages.Get(UiMessageKeys.RequestFailed, _messages.Format(error)));

    private sealed record Sample(ApiRequest Login, ApiRequest Tasks, ApiEnvironmentId EnvironmentId);
}
