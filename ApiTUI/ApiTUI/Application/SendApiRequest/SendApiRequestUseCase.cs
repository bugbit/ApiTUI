using System.Text.Json;
using ApiTUI.Application.Ports;
using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Application.SendApiRequest;

public sealed class SendApiRequestUseCase(
    IHttpSender httpSender,
    TimeProvider timeProvider,
    IApiEnvironmentReader environmentReader,
    IApiCollectionReader collectionReader,
    IApiFolderReader folderReader,
    IApiEnvironmentWriter environmentWriter,
    IApiCollectionWriter collectionWriter,
    IApiFolderWriter folderWriter)
{
    private readonly IHttpSender _httpSender = httpSender;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IApiEnvironmentReader _environmentReader = environmentReader;
    private readonly IApiCollectionReader _collectionReader = collectionReader;
    private readonly IApiFolderReader _folderReader = folderReader;
    private readonly IApiEnvironmentWriter _environmentWriter = environmentWriter;
    private readonly IApiCollectionWriter _collectionWriter = collectionWriter;
    private readonly IApiFolderWriter _folderWriter = folderWriter;

    public async Task<Result<ApiResponse>> ExecuteAsync(
        ApiRequest request, ApiEnvironmentId? environmentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var loaded = await LoadVariablesAsync(request, environmentId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (loaded is Error loadedError)
        {
            return loadedError;
        }

        var (variables, scopes) = (LoadedVariables)loaded.Value;
        var unresolved = request.Url.GetVarNames().Concat(request.Body.GetVarNames())
            .Concat(request.Headers.Where(header => header.Enabled).SelectMany(header => header.Value.GetVarNames()))
            .Where(name => !variables.ContainsKey(name)).Distinct().ToArray();
        if (unresolved.Length > 0)
        {
            return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.UnresolvedVariables,
                string.Join(", ", unresolved.Select(name => name.Value)));
        }

        var url = request.Url.Resolve(variables);
        if (string.IsNullOrWhiteSpace(url))
        {
            return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.UrlEmpty);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.UrlInvalid, url);
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.UrlSchemeNotSupported, uri.Scheme);
        }

        var headers = new List<KeyValuePair<string, string>>();
        foreach (var header in request.Headers.Where(header => header.Enabled))
        {
            var value = header.Value.Resolve(variables) ?? string.Empty;
            if (value.AsSpan().ContainsAny('\r', '\n', '\0'))
            {
                return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.HeaderValueInvalid, header.Name.Value);
            }

            headers.Add(new KeyValuePair<string, string>(header.Name.Value, value));
        }

        var body = request.Body.Resolve(variables);
        var data = new HttpRequestData(request.Method, uri, headers.AsReadOnly(), string.IsNullOrEmpty(body) ? null : body);
        var start = _timeProvider.GetTimestamp();
        var result = await _httpSender.SendAsync(data, cancellationToken);
        if (result is Error resultError)
        {
            return resultError;
        }

        var response = (HttpResponseData)result.Value;
        var apiResponse = new ApiResponse(response.StatusCode, response.ReasonPhrase, response.Headers,
            response.Body, _timeProvider.GetElapsedTime(start));
        if (request.VarsOut is not { Count: > 0 } varsOut)
        {
            return apiResponse;
        }

        return await ApplyVarsOutAsync(apiResponse, varsOut, variables, scopes, cancellationToken);
    }

    private async Task<ApiResponse> ApplyVarsOutAsync(ApiResponse response, IReadOnlyList<VarValue> varsOut,
        IReadOnlyDictionary<VarName, object?> variables, IReadOnlyDictionary<VarName, Identifier> scopes,
        CancellationToken cancellationToken)
    {
        var errors = new List<VarOutError>();
        var batches = new Dictionary<Identifier, List<VarValue>>();
        var json = new Lazy<JsonDocument?>(() => ParseJson(response.Body));
        try
        {
            foreach (var variable in varsOut)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!variable.Enabled)
                {
                    continue;
                }

                if (!scopes.TryGetValue(variable.Name, out var scope))
                {
                    errors.Add(TargetNotFound(variable.Name));
                    continue;
                }

                var evaluated = ResponseVarExtractor.Evaluate(variable.Value, response.Body, json, variables);
                if (evaluated is Error error)
                {
                    errors.Add(new VarOutError(variable.Name, error));
                    continue;
                }

                if (!batches.TryGetValue(scope, out var batch))
                {
                    batch = [];
                    batches.Add(scope, batch);
                }

                batch.Add(variable.WithValue(new StringWithVars((string)evaluated.Value)));
            }
        }
        finally
        {
            if (json.IsValueCreated)
            {
                json.Value?.Dispose();
            }
        }

        var assigned = new List<VarValue>();
        foreach (var (scope, batch) in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = batch.ToDictionary(variable => variable.Name, variable => variable.Value);
            var saved = await SaveVarsOutAsync(scope, values, cancellationToken);
            if (saved is Error error)
            {
                errors.AddRange(batch.Select(variable => new VarOutError(variable.Name, error)));
                continue;
            }

            var missing = ((IReadOnlyList<VarName>)saved.Value).ToHashSet();
            foreach (var variable in batch)
            {
                if (missing.Contains(variable.Name))
                {
                    errors.Add(TargetNotFound(variable.Name));
                }
                else
                {
                    assigned.Add(variable);
                }
            }
        }

        return response with { AssignedVars = assigned.AsReadOnly(), VarOutErrors = errors.AsReadOnly() };
    }

    private async Task<Result<IReadOnlyList<VarName>>> SaveVarsOutAsync(Identifier scope,
        IReadOnlyDictionary<VarName, StringWithVars> values, CancellationToken cancellationToken)
    {
        try
        {
            return scope switch
            {
                ApiCollectionId id => await _collectionWriter.UpdateVariablesAsync(id, values, cancellationToken),
                ApiEnvironmentId id => await _environmentWriter.UpdateVariablesAsync(id, values, cancellationToken),
                ApiFolderId id => await _folderWriter.UpdateVariablesAsync(id, values, cancellationToken),
                _ => throw new InvalidOperationException(),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new Error(ErrorsReasons.PersistenceFailure, ApplicationErrorCodes.VarOutSaveFailed);
        }
    }

    private static JsonDocument? ParseJson(string body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static VarOutError TargetNotFound(VarName name) =>
        new(name, new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.VarOutTargetNotFound, name.Value));

    private async Task<Result<LoadedVariables>> LoadVariablesAsync(
        ApiRequest request, ApiEnvironmentId? environmentId, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<VarName, object?>();
        var scopes = new Dictionary<VarName, Identifier>();
        ApiEnvironment? environment = null;
        if (environmentId is not null)
        {
            environment = await _environmentReader.FindAsync(environmentId, cancellationToken);
            if (environment is null)
            {
                return Missing(ApplicationErrorCodes.EnvironmentNotFound, environmentId.Id);
            }
        }

        var chain = new List<ApiFolder>();
        var collectionId = request.CollectionId;
        if (request.FolderId is { } folderId)
        {
            var visited = new HashSet<ApiFolderId>();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!visited.Add(folderId))
                {
                    return Missing(ApplicationErrorCodes.FolderCycle, folderId.Id);
                }

                var folder = await _folderReader.FindAsync(folderId, cancellationToken);
                if (folder is null)
                {
                    return Missing(ApplicationErrorCodes.FolderNotFound, folderId.Id);
                }

                chain.Add(folder);
                if (folder.ParentFolderId is not { } parentId)
                {
                    collectionId = folder.CollectionId;
                    break;
                }

                folderId = parentId;
            }
        }

        if (collectionId is not { } id)
        {
            return Missing(ApplicationErrorCodes.CollectionNotFound, string.Empty);
        }

        var collection = await _collectionReader.FindAsync(id, cancellationToken);
        if (collection is null)
        {
            return Missing(ApplicationErrorCodes.CollectionNotFound, id.Id);
        }

        // Precedencia como en Bruno (gana lo aplicado después):
        // colección < entorno < carpeta raíz < ... < carpeta de la petición.
        Apply(collection.Id, collection.Variables);
        if (environment is not null)
        {
            Apply(environment.Id, environment.Variables);
        }

        chain.Reverse();
        foreach (var folder in chain)
        {
            Apply(folder.Id, folder.Variables);
        }

        return new LoadedVariables(variables, scopes);

        void Apply(Identifier scope, IReadOnlyList<VarValue> values)
        {
            foreach (var variable in values)
            {
                if (variable.Enabled)
                {
                    variables[variable.Name] = variable.Value.Text;
                    scopes[variable.Name] = scope;
                }
            }
        }
    }

    private static Result<LoadedVariables> Missing(string code, string id) =>
        new Error(ErrorsReasons.InvalidInput, code, id);

    private sealed record LoadedVariables(
        IReadOnlyDictionary<VarName, object?> Values, IReadOnlyDictionary<VarName, Identifier> Scopes);
}
