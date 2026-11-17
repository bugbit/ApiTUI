using System.Text;
using ApiTUI.Application;
using ApiTUI.Application.Ports;
using ApiTUI.Application.SendApiRequest;
using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Adapters.Http;

public sealed class HttpClientSender(HttpClient httpClient, HttpSenderOptions options, TimeProvider timeProvider) : IHttpSender
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly HttpSenderOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<Result<HttpResponseData>> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using var message = new HttpRequestMessage(ToHttpMethod(request.Method), request.Url);
        if (request.Body is not null)
        {
            message.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(request.Body));
        }

        foreach (var (name, value) in request.Headers)
        {
            if (value.AsSpan().ContainsAny('\r', '\n', '\0'))
            {
                return Result<HttpResponseData>.Failure(ErrorsReasons.InvalidInput, ApplicationErrorCodes.HeaderValueInvalid, name);
            }

            if (!message.Headers.TryAddWithoutValidation(name, value)
                && !(message.Content?.Headers.TryAddWithoutValidation(name, value) ?? false))
            {
                return Result<HttpResponseData>.Failure(ErrorsReasons.InvalidInput, ApplicationErrorCodes.HeaderInvalid, name);
            }
        }

        using var timeoutCts = new CancellationTokenSource(_options.Timeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            var body = await response.Content.ReadAsStringAsync(linked.Token);
            var headers = response.Headers.Concat(response.Content.Headers)
                .SelectMany(header => header.Value.Select(value => new KeyValuePair<string, string>(header.Key, value)))
                .ToArray();
            return Result<HttpResponseData>.Success(new HttpResponseData((int)response.StatusCode, response.ReasonPhrase,
                Array.AsReadOnly(headers), body));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return Result<HttpResponseData>.Failure(ErrorsReasons.Timeout, ApplicationErrorCodes.HttpTimeout, _options.Timeout.TotalSeconds);
        }
        catch (HttpRequestException exception)
        {
            return Result<HttpResponseData>.Failure(ErrorsReasons.HttpFailure, ApplicationErrorCodes.HttpRequestFailed,
                exception.HttpRequestError.ToString());
        }
    }

    private static HttpMethod ToHttpMethod(RequestMethod method) => method switch
    {
        RequestMethod.Get => HttpMethod.Get,
        RequestMethod.Post => HttpMethod.Post,
        RequestMethod.Put => HttpMethod.Put,
        RequestMethod.Patch => HttpMethod.Patch,
        RequestMethod.Delete => HttpMethod.Delete,
        RequestMethod.Head => HttpMethod.Head,
        RequestMethod.Options => HttpMethod.Options,
        RequestMethod.Query => new HttpMethod("QUERY"),
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };
}
