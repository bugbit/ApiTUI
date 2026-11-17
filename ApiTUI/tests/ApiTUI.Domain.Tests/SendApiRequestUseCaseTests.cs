using ApiTUI.Application;
using ApiTUI.Application.Ports;
using ApiTUI.Application.SendApiRequest;
using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class SendApiRequestUseCaseTests
{
    [Fact]
    public async Task Execute_ResolvedRequest_SendsEnabledHeadersAndReturnsTimedResponse()
    {
        var clock = new TestTimeProvider();
        var sender = new FakeHttpSender { OnSend = () => clock.Timestamp = 1250 };
        var useCase = new SendApiRequestUseCase(sender, clock);
        var request = Request(body: "body", headers:
            [Header("X-One", "1"), Header("X-Disabled", "ignored", false), Header("X-One", "2")]);

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var sent = Assert.IsType<HttpRequestData>(sender.Request);
        Assert.Equal(RequestMethod.Post, sent.Method);
        Assert.Equal("https://example.test/api", sent.Url.AbsoluteUri);
        Assert.Equal(new[] { new KeyValuePair<string, string>("X-One", "1"), new("X-One", "2") }, sent.Headers);
        Assert.Equal("body", sent.Body);
        Assert.Equal(201, result.Value.StatusCode);
        Assert.Equal("Created", result.Value.ReasonPhrase);
        Assert.Same(sender.Response.Headers, result.Value.Headers);
        Assert.Equal("response", result.Value.Body);
        Assert.Equal(TimeSpan.FromMilliseconds(1250), result.Value.Duration);
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\0")]
    [InlineData("a\r\nX-Injected: 1")]
    public async Task Execute_InvalidHeaderValue_ReturnsErrorWithoutSending(string value)
    {
        var sender = new FakeHttpSender();
        var result = await new SendApiRequestUseCase(sender, TimeProvider.System)
            .ExecuteAsync(Request(headers: [Header("X-Test", value)]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(ApplicationErrorCodes.HeaderValueInvalid, result.Error.Code);
        Assert.Equal("X-Test", Assert.Single(result.Error.Args));
        Assert.Null(sender.Request);
    }

    [Theory]
    [InlineData("url")]
    [InlineData("body")]
    [InlineData("header")]
    public async Task Execute_UnresolvedVariable_ReturnsErrorWithoutSending(string location)
    {
        var sender = new FakeHttpSender();
        var request = Request(url: location == "url" ? "{{baseUrl}}/api" : "https://example.test/api",
            body: location == "body" ? "{{baseUrl}}" : null,
            headers: location == "header" ? [Header("X-Test", "{{baseUrl}}")] : null);

        var result = await new SendApiRequestUseCase(sender, TimeProvider.System).ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrorCodes.UnresolvedVariables, result.Error.Code);
        Assert.Equal("baseUrl", Assert.Single(result.Error.Args));
        Assert.Null(sender.Request);
    }

    [Fact]
    public async Task Execute_RepeatedUnresolvedVariables_ListsEachOnceInOrder()
    {
        var result = await new SendApiRequestUseCase(new FakeHttpSender(), TimeProvider.System).ExecuteAsync(
            Request("{{b}}/{{a}}", "{{b}}", [Header("X-Test", "{{c}}")]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("b, a, c", Assert.Single(result.Error.Args));
    }

    [Theory]
    [InlineData("{{missing}}")]
    [InlineData("a\r\nInjected: 1")]
    public async Task Execute_DisabledHeader_IgnoresVariablesAndInvalidValue(string value)
    {
        var sender = new FakeHttpSender();
        var result = await new SendApiRequestUseCase(sender, TimeProvider.System)
            .ExecuteAsync(Request(headers: [Header("X-Test", value, false)]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(Assert.IsType<HttpRequestData>(sender.Request).Headers);
    }

    [Theory]
    [InlineData(null, ApplicationErrorCodes.UrlEmpty)]
    [InlineData("", ApplicationErrorCodes.UrlEmpty)]
    [InlineData(" ", ApplicationErrorCodes.UrlEmpty)]
    [InlineData("relative/path", ApplicationErrorCodes.UrlInvalid)]
    [InlineData("https://", ApplicationErrorCodes.UrlInvalid)]
    [InlineData("ftp://example.test", ApplicationErrorCodes.UrlSchemeNotSupported)]
    public async Task Execute_InvalidUrl_ReturnsErrorWithoutSending(string? url, string code)
    {
        var sender = new FakeHttpSender();
        var result = await new SendApiRequestUseCase(sender, TimeProvider.System)
            .ExecuteAsync(Request(url), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(code, result.Error.Code);
        Assert.Null(sender.Request);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Execute_EmptyBody_SendsNoContent(string? body)
    {
        var sender = new FakeHttpSender();
        var result = await new SendApiRequestUseCase(sender, TimeProvider.System)
            .ExecuteAsync(Request(body: body, headers: [Header("X-Empty", null)]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var sent = Assert.IsType<HttpRequestData>(sender.Request);
        Assert.Null(sent.Body);
        Assert.Equal(string.Empty, Assert.Single(sent.Headers).Value);
    }

    [Fact]
    public async Task Execute_PortFailure_ReturnsSameErrorAndPassesCancellationToken()
    {
        var error = new Error(ErrorsReasons.HttpFailure, ApplicationErrorCodes.HttpRequestFailed, "NameResolutionError");
        var sender = new FakeHttpSender { Error = error };
        using var cancellation = new CancellationTokenSource();

        var result = await new SendApiRequestUseCase(sender, TimeProvider.System)
            .ExecuteAsync(Request(), cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Equal(cancellation.Token, sender.Token);
    }

    [Fact]
    public async Task Execute_AlreadyCancelled_DoesNotCallPort()
    {
        var sender = new FakeHttpSender();
        var useCase = new SendApiRequestUseCase(sender, TimeProvider.System);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(Request(), new CancellationToken(true)));
        Assert.Null(sender.Request);
    }

    private static ApiRequest Request(string? url = "https://example.test/api", string? body = null,
        IEnumerable<HeaderValue?>? headers = null)
    {
        var id = ApiRequestId.Create("request");
        var collection = ApiCollectionId.Create("collection");
        Assert.True(id.IsSuccess);
        Assert.True(collection.IsSuccess);
        var result = ApiRequest.CreateInCollection(id.Value, collection.Value, "Request", RequestMethod.Post,
            new StringWithVars(url), headers, new StringWithVars(body));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static HeaderValue Header(string name, string? value, bool enabled = true)
    {
        var headerName = HeaderName.Create(name);
        Assert.True(headerName.IsSuccess);
        var result = HeaderValue.Create(headerName.Value, new StringWithVars(value), enabled);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        public long Timestamp { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Timestamp;
    }

    private sealed class FakeHttpSender : IHttpSender
    {
        public HttpRequestData? Request { get; private set; }
        public CancellationToken Token { get; private set; }
        public Action? OnSend { get; init; }
        public Error? Error { get; init; }
        public HttpResponseData Response { get; } = new(201, "Created", [new("X-Response", "1")], "response");

        public Task<Result<HttpResponseData>> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
        {
            Request = request;
            Token = cancellationToken;
            OnSend?.Invoke();
            return Task.FromResult(Error is null ? Result<HttpResponseData>.Success(Response) : Result<HttpResponseData>.Failure(Error));
        }
    }
}
