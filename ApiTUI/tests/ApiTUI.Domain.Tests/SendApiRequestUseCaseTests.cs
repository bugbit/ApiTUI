using ApiTUI.Adapters.Persistence;
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
        var useCase = CreateUseCase(sender, clock: clock);
        var request = Request(body: "body", headers:
            [Header("X-One", "1"), Header("X-Disabled", "ignored", false), Header("X-One", "2")]);

        var result = await useCase.ExecuteAsync(request, null, CancellationToken.None);

        var sent = Assert.IsType<HttpRequestData>(sender.Request);
        Assert.Equal(RequestMethod.Post, sent.Method);
        Assert.Equal("https://example.test/api", sent.Url.AbsoluteUri);
        Assert.Equal(new[] { new KeyValuePair<string, string>("X-One", "1"), new("X-One", "2") }, sent.Headers);
        Assert.Equal("body", sent.Body);
        var value = result.AssertSuccess();
        Assert.Equal(201, value.StatusCode);
        Assert.Equal("Created", value.ReasonPhrase);
        Assert.Same(sender.Response.Headers, value.Headers);
        Assert.Equal("response", value.Body);
        Assert.Equal(TimeSpan.FromMilliseconds(1250), value.Duration);
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\0")]
    [InlineData("a\r\nX-Injected: 1")]
    public async Task Execute_InvalidHeaderValue_ReturnsErrorWithoutSending(string value)
    {
        var sender = new FakeHttpSender();
        var result = await CreateUseCase(sender)
            .ExecuteAsync(Request(headers: [Header("X-Test", value)]), null, CancellationToken.None);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(ApplicationErrorCodes.HeaderValueInvalid, error.Code);
        Assert.Equal("X-Test", Assert.Single(error.Args));
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

        var result = await CreateUseCase(sender).ExecuteAsync(request, null, CancellationToken.None);

        var error = result.AssertFailure();
        Assert.Equal(ApplicationErrorCodes.UnresolvedVariables, error.Code);
        Assert.Equal("baseUrl", Assert.Single(error.Args));
        Assert.Null(sender.Request);
    }

    [Fact]
    public async Task Execute_RepeatedUnresolvedVariables_ListsEachOnceInOrder()
    {
        var result = await CreateUseCase(new FakeHttpSender()).ExecuteAsync(
            Request("{{b}}/{{a}}", "{{b}}", [Header("X-Test", "{{c}}")]), null, CancellationToken.None);

        Assert.Equal("b, a, c", Assert.Single(result.AssertFailure().Args));
    }

    [Theory]
    [InlineData("{{missing}}")]
    [InlineData("a\r\nInjected: 1")]
    public async Task Execute_DisabledHeader_IgnoresVariablesAndInvalidValue(string value)
    {
        var sender = new FakeHttpSender();
        var result = await CreateUseCase(sender)
            .ExecuteAsync(Request(headers: [Header("X-Test", value, false)]), null, CancellationToken.None);

        result.AssertSuccess();
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
        var result = await CreateUseCase(sender)
            .ExecuteAsync(Request(url), null, CancellationToken.None);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(code, error.Code);
        Assert.Null(sender.Request);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Execute_EmptyBody_SendsNoContent(string? body)
    {
        var sender = new FakeHttpSender();
        var result = await CreateUseCase(sender)
            .ExecuteAsync(Request(body: body, headers: [Header("X-Empty", null)]), null, CancellationToken.None);

        result.AssertSuccess();
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

        var result = await CreateUseCase(sender)
            .ExecuteAsync(Request(), null, cancellation.Token);

        Assert.Same(error, result.AssertFailure());
        Assert.Equal(cancellation.Token, sender.Token);
    }

    [Fact]
    public async Task Execute_AlreadyCancelled_DoesNotCallPort()
    {
        var sender = new FakeHttpSender();
        var useCase = CreateUseCase(sender);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(Request(), null, new CancellationToken(true)));
        Assert.Null(sender.Request);
    }

    [Theory]
    [InlineData(null, "root", true, "root")]
    [InlineData("leaf", "root", true, "leaf")]
    [InlineData(null, null, true, "environment")]
    [InlineData(null, null, false, "collection")]
    public async Task Execute_UsesBrunoPrecedence(string? leafValue, string? rootValue, bool environmentEnabled,
        string expected)
    {
        var sender = new FakeHttpSender();
        var store = new InMemoryApiStore();
        var environment = ApiEnvironment.Create(ApiEnvironmentId.Create("env").AssertSuccess(), "Environment",
            [Variable("host", "example.test"), Variable("scope", "environment", environmentEnabled)]).AssertSuccess();
        var collection = ApiCollection.Create(CollectionId(), "Collection",
            [Variable("scope", "collection"), Variable("onlyCollection", "c")]).AssertSuccess();
        var root = ApiFolder.CreateInCollection(FolderId("root"), collection.Id, "Root",
            [Variable("scope", rootValue, rootValue is not null), Variable("onlyRoot", "r")]).AssertSuccess();
        var middle = ApiFolder.CreateInFolder(FolderId("middle"), root.Id, "Middle", [Variable("onlyMiddle", "m")]).AssertSuccess();
        var leaf = ApiFolder.CreateInFolder(FolderId("leaf"), middle.Id, "Leaf",
            [Variable("scope", leafValue, leafValue is not null), Variable("onlyLeaf", "l")]).AssertSuccess();
        store.Add(environment);
        store.Add(collection);
        store.Add(root);
        store.Add(middle);
        store.Add(leaf);
        var request = ApiRequest.CreateInFolder(ApiRequestId.Create("request").AssertSuccess(), leaf.Id, "Request",
            url: new StringWithVars("https://{{host}}/{{scope}}"),
            body: new StringWithVars("{{onlyCollection}}/{{onlyRoot}}/{{onlyMiddle}}/{{onlyLeaf}}")).AssertSuccess();

        var result = await CreateUseCase(sender, store).ExecuteAsync(request, environment.Id, CancellationToken.None);

        result.AssertSuccess();
        var sent = Assert.IsType<HttpRequestData>(sender.Request);
        Assert.Equal($"https://example.test/{expected}", sent.Url.AbsoluteUri);
        Assert.Equal("c/r/m/l", sent.Body);
    }

    [Fact]
    public async Task Execute_WithoutEnvironmentId_DoesNotUseStoredEnvironment()
    {
        var sender = new FakeHttpSender();
        var store = new InMemoryApiStore();
        store.Add(ApiCollection.Create(CollectionId(), "Collection").AssertSuccess());
        store.Add(ApiEnvironment.Create(ApiEnvironmentId.Create("env").AssertSuccess(), "Environment", [Variable("host", "example.test")]).AssertSuccess());

        var result = await CreateUseCase(sender, store).ExecuteAsync(Request("https://{{host}}/"), null, CancellationToken.None);

        Assert.Equal(ApplicationErrorCodes.UnresolvedVariables, result.AssertFailure().Code);
        Assert.Null(sender.Request);
    }

    [Fact]
    public async Task Execute_ValidatesResolvedHeadersAndDoesNotResolveRecursively()
    {
        var sender = new FakeHttpSender();
        var store = new InMemoryApiStore();
        store.Add(ApiCollection.Create(CollectionId(), "Collection",
            [Variable("invalid", "a\r\nb"), Variable("literal", "{{other}}"), Variable("other", "second"), Variable("empty", null)]).AssertSuccess());
        var useCase = CreateUseCase(sender, store);

        var rejected = await useCase.ExecuteAsync(Request(headers: [Header("X-Test", "{{invalid}}")]), null, CancellationToken.None);
        Assert.Equal(ApplicationErrorCodes.HeaderValueInvalid, rejected.AssertFailure().Code);
        Assert.Null(sender.Request);

        var accepted = await useCase.ExecuteAsync(Request(body: "{{literal}}", headers: [Header("X-Test", "{{empty}}")]),
            null, CancellationToken.None);
        accepted.AssertSuccess();
        var sent = Assert.IsType<HttpRequestData>(sender.Request);
        Assert.Equal("{{other}}", sent.Body);
        Assert.Equal(string.Empty, Assert.Single(sent.Headers).Value);
    }

    [Theory]
    [InlineData(ApplicationErrorCodes.EnvironmentNotFound, false, "env")]
    [InlineData(ApplicationErrorCodes.CollectionNotFound, false, "collection")]
    [InlineData(ApplicationErrorCodes.CollectionNotFound, true, "collection")]
    [InlineData(ApplicationErrorCodes.FolderNotFound, true, "parent")]
    [InlineData(ApplicationErrorCodes.FolderCycle, true, "leaf")]
    public async Task Execute_InvalidReferences_ReturnsErrorWithoutSending(string code, bool inFolder, string expectedId)
    {
        var sender = new FakeHttpSender();
        var store = new InMemoryApiStore();
        var leafId = FolderId("leaf");
        var parentId = FolderId("parent");
        if (code == ApplicationErrorCodes.CollectionNotFound)
        {
            store.Add(ApiFolder.CreateInCollection(leafId, CollectionId(), "Leaf").AssertSuccess());
        }
        else if (inFolder)
        {
            store.Add(ApiFolder.CreateInFolder(leafId, parentId, "Leaf").AssertSuccess());
            if (code == ApplicationErrorCodes.FolderCycle)
            {
                store.Add(ApiFolder.CreateInFolder(parentId, leafId, "Parent").AssertSuccess());
            }
        }

        var request = inFolder
            ? ApiRequest.CreateInFolder(ApiRequestId.Create("request").AssertSuccess(), leafId, "Request").AssertSuccess()
            : Request();
        var environmentId = code == ApplicationErrorCodes.EnvironmentNotFound ? ApiEnvironmentId.Create("env").AssertSuccess() : null;
        var result = await CreateUseCase(sender, store).ExecuteAsync(request, environmentId, CancellationToken.None);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(code, error.Code);
        Assert.Equal(expectedId, Assert.Single(error.Args));
        Assert.Null(sender.Request);
    }

    private static SendApiRequestUseCase CreateUseCase(IHttpSender sender, InMemoryApiStore? store = null, TimeProvider? clock = null)
    {
        if (store is null)
        {
            store = new InMemoryApiStore();
            store.Add(ApiCollection.Create(CollectionId(), "Collection").AssertSuccess());
        }

        return new SendApiRequestUseCase(sender, clock ?? TimeProvider.System, store, store, store);
    }

    private static ApiCollectionId CollectionId() => ApiCollectionId.Create("collection").AssertSuccess();
    private static ApiFolderId FolderId(string id) => ApiFolderId.Create(id).AssertSuccess();

    private static VarValue Variable(string name, string? value, bool enabled = true) =>
        VarValue.Create(VarName.Create(name).AssertSuccess(), new StringWithVars(value), enabled).AssertSuccess();


    private static ApiRequest Request(string? url = "https://example.test/api", string? body = null,
        IEnumerable<HeaderValue?>? headers = null)
    {
        var id = ApiRequestId.Create("request");
        var collection = ApiCollectionId.Create("collection");
        var result = ApiRequest.CreateInCollection(id.AssertSuccess(), collection.AssertSuccess(), "Request", RequestMethod.Post,
            new StringWithVars(url), headers, new StringWithVars(body));
        return result.AssertSuccess();
    }

    private static HeaderValue Header(string name, string? value, bool enabled = true)
    {
        var headerName = HeaderName.Create(name);
        var result = HeaderValue.Create(headerName.AssertSuccess(), new StringWithVars(value), enabled);
        return result.AssertSuccess();
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
            return Task.FromResult<Result<HttpResponseData>>(Error is null ? Response : Error);
        }
    }
}
