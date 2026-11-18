using ApiTUI.Application.Ports;
using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Application.SendApiRequest;

public sealed class SendApiRequestUseCase(
    IHttpSender httpSender,
    TimeProvider timeProvider,
    IApiEnvironmentReader environmentReader,
    IApiCollectionReader collectionReader,
    IApiFolderReader folderReader)
{
    private readonly IHttpSender _httpSender = httpSender;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IApiEnvironmentReader _environmentReader = environmentReader;
    private readonly IApiCollectionReader _collectionReader = collectionReader;
    private readonly IApiFolderReader _folderReader = folderReader;

    public async Task<Result<ApiResponse>> ExecuteAsync(
        ApiRequest request, ApiEnvironmentId? environmentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var loaded = await LoadVariablesAsync(request, environmentId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!loaded.IsSuccess)
        {
            return Result<ApiResponse>.Failure(loaded.Error);
        }

        var variables = loaded.Value;
        var unresolved = request.Url.GetVarNames().Concat(request.Body.GetVarNames())
            .Concat(request.Headers.Where(header => header.Enabled).SelectMany(header => header.Value.GetVarNames()))
            .Where(name => !variables.ContainsKey(name)).Distinct().ToArray();
        if (unresolved.Length > 0)
        {
            return Result<ApiResponse>.Failure(ErrorsReasons.InvalidInput, ApplicationErrorCodes.UnresolvedVariables,
                string.Join(", ", unresolved.Select(name => name.Value)));
        }

        var url = request.Url.Resolve(variables);
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result<ApiResponse>.Failure(ErrorsReasons.InvalidInput, ApplicationErrorCodes.UrlEmpty);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Result<ApiResponse>.Failure(ErrorsReasons.InvalidInput, ApplicationErrorCodes.UrlInvalid, url);
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return Result<ApiResponse>.Failure(ErrorsReasons.InvalidInput, ApplicationErrorCodes.UrlSchemeNotSupported, uri.Scheme);
        }

        var headers = new List<KeyValuePair<string, string>>();
        foreach (var header in request.Headers.Where(header => header.Enabled))
        {
            var value = header.Value.Resolve(variables) ?? string.Empty;
            if (value.AsSpan().ContainsAny('\r', '\n', '\0'))
            {
                return Result<ApiResponse>.Failure(ErrorsReasons.InvalidInput, ApplicationErrorCodes.HeaderValueInvalid, header.Name.Value);
            }

            headers.Add(new KeyValuePair<string, string>(header.Name.Value, value));
        }

        var body = request.Body.Resolve(variables);
        var data = new HttpRequestData(request.Method, uri, headers.AsReadOnly(), string.IsNullOrEmpty(body) ? null : body);
        var start = _timeProvider.GetTimestamp();
        var result = await _httpSender.SendAsync(data, cancellationToken);
        if (!result.IsSuccess)
        {
            return Result<ApiResponse>.Failure(result.Error);
        }

        var response = result.Value;
        return Result<ApiResponse>.Success(new ApiResponse(response.StatusCode, response.ReasonPhrase, response.Headers,
            response.Body, _timeProvider.GetElapsedTime(start)));
    }

    private async Task<Result<Dictionary<VarName, object?>>> LoadVariablesAsync(
        ApiRequest request, ApiEnvironmentId? environmentId, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<VarName, object?>();
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
        Apply(collection.Variables);
        if (environment is not null)
        {
            Apply(environment.Variables);
        }

        chain.Reverse();
        foreach (var folder in chain)
        {
            Apply(folder.Variables);
        }

        return Result<Dictionary<VarName, object?>>.Success(variables);

        void Apply(IReadOnlyList<VarValue> scope)
        {
            foreach (var variable in scope)
            {
                if (variable.Enabled)
                {
                    variables[variable.Name] = variable.Value.Text;
                }
            }
        }
    }

    private static Result<Dictionary<VarName, object?>> Missing(string code, string id) =>
        Result<Dictionary<VarName, object?>>.Failure(ErrorsReasons.InvalidInput, code, id);
}
