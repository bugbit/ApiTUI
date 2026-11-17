using ApiTUI.Application.Ports;
using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Application.SendApiRequest;

public sealed class SendApiRequestUseCase(IHttpSender httpSender, TimeProvider timeProvider)
{
    private readonly IHttpSender _httpSender = httpSender;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<Result<ApiResponse>> ExecuteAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var variables = CreateVariables();
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

    private static Dictionary<VarName, object?> CreateVariables()
    {
        // Por ahora no hay valores; aquí se incorporará el entorno activo cuando exista.
        return [];
    }
}
