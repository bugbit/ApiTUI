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

    [Fact]
    public async Task Execute_VarsOut_UpdatesWinningScopesAndRetainsLiteralMarkerText()
    {
        var store = new InMemoryApiStore();
        var environmentId = ApiEnvironmentId.Create("scope").AssertSuccess();
        var folderId = FolderId("scope");
        var collectionId = ApiCollectionId.Create("scope").AssertSuccess();
        store.Add(ApiCollection.Create(collectionId, "Collection",
            [Variable("token", "collection"), Variable("collectionOnly", "old")]).AssertSuccess());
        store.Add(ApiEnvironment.Create(environmentId, "Environment",
            [Variable("token", "environment"), Variable("environmentOnly", "old")]).AssertSuccess());
        store.Add(ApiFolder.CreateInCollection(folderId, collectionId, "Folder",
            [Variable("token", "folder")]).AssertSuccess());
        var request = ApiRequest.CreateInFolder(ApiRequestId.Create("request").AssertSuccess(), folderId, "Request",
            url: new StringWithVars("https://example.test"),
            varsOut: [Variable("token", "{{response.json.token}}"), Variable("environmentOnly", "environment new"),
                Variable("collectionOnly", "collection new")]).AssertSuccess();
        var sender = new FakeHttpSender { Response = new(200, "OK", [], """{"token":"{{later}}"}""") };

        var response = (await CreateUseCase(sender, store).ExecuteAsync(request, environmentId, CancellationToken.None)).AssertSuccess();

        var collection = Assert.IsType<ApiCollection>(await store.FindAsync(collectionId, CancellationToken.None));
        var environment = Assert.IsType<ApiEnvironment>(await store.FindAsync(environmentId, CancellationToken.None));
        var folder = Assert.IsType<ApiFolder>(await store.FindAsync(folderId, CancellationToken.None));
        Assert.Equal(new[] { "collection", "collection new" }, collection.Variables.Select(v => v.Value.Text));
        Assert.Equal(new[] { "environment", "environment new" }, environment.Variables.Select(v => v.Value.Text));
        var token = Assert.Single(folder.Variables);
        Assert.Equal("{{later}}", token.Value.Text);
        Assert.Equal("later", Assert.Single(token.Value.GetVarNames()).Value);
        Assert.Equal(new[] { "token", "environmentOnly", "collectionOnly" }, response.AssignedVars.Select(v => v.Name.Value));
        Assert.Empty(response.VarOutErrors);
    }

    [Fact]
    public async Task Execute_VarsOut_UsesEnvironmentValueLoadedBeforeHttpAndSavesToCollection()
    {
        var store = new InMemoryApiStore();
        var environmentId = ApiEnvironmentId.Create("env").AssertSuccess();
        store.Add(ApiCollection.Create(CollectionId(), "Collection", [Variable("token", "old")]).AssertSuccess());
        store.Add(ApiEnvironment.Create(environmentId, "Environment", [Variable("suffix", "original")]).AssertSuccess());
        var sender = new FakeHttpSender
        {
            Response = new(200, "OK", [], """{"token":"new"}"""),
            OnSend = () => store.Add(ApiEnvironment.Create(environmentId, "Environment",
                [Variable("suffix", "changed")]).AssertSuccess()),
        };
        var request = Request(body: "{{suffix}}", varsOut: [Variable("token", "{{response.json.token}}_{{suffix}}")]);

        var result = await CreateUseCase(sender, store).ExecuteAsync(request, environmentId, CancellationToken.None);

        var response = result.AssertSuccess();
        Assert.Equal("original", Assert.IsType<HttpRequestData>(sender.Request).Body);
        Assert.Empty(response.VarOutErrors);
        Assert.Equal("new_original", Assert.Single(response.AssignedVars).Value.Text);
        var collection = Assert.IsType<ApiCollection>(await store.FindAsync(CollectionId(), CancellationToken.None));
        Assert.Equal("new_original", Assert.Single(collection.Variables).Value.Text);
    }

    [Fact]
    public async Task Execute_VarsOut_BatchesSuccessfulAssignmentsForNonSuccessStatusAndReportsErrors()
    {
        var store = new InMemoryApiStore();
        store.Add(ApiCollection.Create(CollectionId(), "Collection",
            [Variable("first", "old"), Variable("second", "old"), Variable("json", "old")]).AssertSuccess());
        var writer = new TrackingCollectionWriter(store.UpdateVariablesAsync);
        var sender = new FakeHttpSender { Response = new(422, "Unprocessable Content", [], "not JSON") };
        var request = Request(varsOut: [Variable("missing", "{{response}}"), Variable("json", "{{response.json.token}}"),
            Variable("first", "{{response}}"), Variable("ignored", "{{unsupported}}", enabled: false),
            Variable("second", "prefix {{response}}")]);
        using var cancellation = new CancellationTokenSource();

        var response = (await CreateUseCase(sender, store, collectionWriter: writer)
            .ExecuteAsync(request, null, cancellation.Token)).AssertSuccess();

        Assert.Equal(422, response.StatusCode);
        Assert.Equal(new[] { "first", "second" }, response.AssignedVars.Select(v => v.Name.Value));
        Assert.Equal(new[] { "not JSON", "prefix not JSON" }, response.AssignedVars.Select(v => v.Value.Text));
        Assert.Collection(response.VarOutErrors,
            error => AssertVarOutError(error, "missing", ApplicationErrorCodes.VarOutTargetNotFound),
            error => AssertVarOutError(error, "json", ApplicationErrorCodes.VarOutResponseNotJson));
        Assert.Equal(new[] { "first", "second" }, Assert.Single(writer.Batches).Keys.Select(name => name.Value));
        Assert.Equal(cancellation.Token, writer.Token);
        var current = Assert.IsType<ApiCollection>(await store.FindAsync(CollectionId(), CancellationToken.None));
        Assert.Equal(new[] { "not JSON", "prefix not JSON", "old" }, current.Variables.Select(v => v.Value.Text));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_EmptyOrNullVarsOut_DoesNotWrite(bool empty)
    {
        var writer = new TrackingCollectionWriter((_, _, _) => throw new InvalidOperationException());

        var response = (await CreateUseCase(new FakeHttpSender(), collectionWriter: writer)
            .ExecuteAsync(Request(varsOut: empty ? [] : null), null, CancellationToken.None)).AssertSuccess();

        Assert.Empty(writer.Batches);
        Assert.Empty(response.AssignedVars);
        Assert.Empty(response.VarOutErrors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_VarsOut_TargetChangedDuringHttp_ReportsMissingAndSavesRemaining(bool disabled)
    {
        var store = new InMemoryApiStore();
        var kept = Variable("kept", "initial");
        var target = Variable("target", "old");
        store.Add(ApiCollection.Create(CollectionId(), "Initial", [target, kept]).AssertSuccess());
        var sender = new FakeHttpSender
        {
            OnSend = () => store.Add(ApiCollection.Create(CollectionId(), "Current",
                disabled ? [target with { Enabled = false }, kept] : [kept]).AssertSuccess()),
        };
        var request = Request(varsOut: [Variable("target", "new target"), Variable("kept", "new kept")]);

        var response = (await CreateUseCase(sender, store).ExecuteAsync(request, null, CancellationToken.None)).AssertSuccess();

        AssertVarOutError(Assert.Single(response.VarOutErrors), "target", ApplicationErrorCodes.VarOutTargetNotFound);
        Assert.Equal("kept", Assert.Single(response.AssignedVars).Name.Value);
        var stored = Assert.IsType<ApiCollection>(await store.FindAsync(CollectionId(), CancellationToken.None));
        Assert.Equal("Current", stored.Name.Value);
        Assert.Equal(disabled ? new[] { "old", "new kept" } : new[] { "new kept" }, stored.Variables.Select(v => v.Value.Text));
    }

    [Theory]
    [InlineData(ApplicationErrorCodes.CollectionNotFound)]
    [InlineData(ApplicationErrorCodes.VarOutSaveFailed)]
    public async Task Execute_VarsOut_WriteFailureReportsEveryVariableAndContinuesWithOtherScopes(string code)
    {
        var store = new InMemoryApiStore();
        store.Add(ApiCollection.Create(CollectionId(), "Collection",
            [Variable("first", "old"), Variable("second", "old")]).AssertSuccess());
        var environmentId = ApiEnvironmentId.Create("env").AssertSuccess();
        store.Add(ApiEnvironment.Create(environmentId, "Environment", [Variable("third", "old")]).AssertSuccess());
        var writer = new TrackingCollectionWriter((_, _, _) => code == ApplicationErrorCodes.VarOutSaveFailed
            ? throw new IOException()
            : Task.FromResult<Result<IReadOnlyList<VarName>>>(new Error(ErrorsReasons.InvalidInput, code, "collection")));

        var response = (await CreateUseCase(new FakeHttpSender(), store, collectionWriter: writer)
            .ExecuteAsync(Request(varsOut: [Variable("first", "new"), Variable("second", "new"), Variable("third", "new")]),
                environmentId, CancellationToken.None)).AssertSuccess();

        Assert.Collection(response.VarOutErrors,
            error => AssertVarOutError(error, "first", code),
            error => AssertVarOutError(error, "second", code));
        Assert.Equal("third", Assert.Single(response.AssignedVars).Name.Value);
    }

    [Fact]
    public async Task Execute_VarsOut_WriteCancellationPropagates()
    {
        var store = new InMemoryApiStore();
        store.Add(ApiCollection.Create(CollectionId(), "Collection", [Variable("token", "old")]).AssertSuccess());
        var writer = new TrackingCollectionWriter((_, _, token) => throw new OperationCanceledException(token));
        var useCase = CreateUseCase(new FakeHttpSender(), store, collectionWriter: writer);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(
            Request(varsOut: [Variable("token", "{{response}}")]), null, CancellationToken.None));
    }

    [Fact]
    public async Task Execute_ConcurrentVarsOut_FromSameInitialCollection_PreserveBothAssignments()
    {
        var store = new InMemoryApiStore();
        store.Add(ApiCollection.Create(CollectionId(), "Collection",
            [Variable("token", "old"), Variable("id", "old")]).AssertSuccess());
        var sender = new BarrierHttpSender();
        var useCase = CreateUseCase(sender, store);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var first = useCase.ExecuteAsync(Request(varsOut: [Variable("token", "new token")]), null, cancellation.Token);
        var second = useCase.ExecuteAsync(Request(varsOut: [Variable("id", "new id")]), null, cancellation.Token);
        var responses = await Task.WhenAll(first, second);

        Assert.All(responses, result => Assert.Empty(result.AssertSuccess().VarOutErrors));
        var stored = Assert.IsType<ApiCollection>(await store.FindAsync(CollectionId(), CancellationToken.None));
        Assert.Equal(new[] { "new token", "new id" }, stored.Variables.Select(v => v.Value.Text));
    }

    private static void AssertVarOutError(VarOutError error, string name, string code)
    {
        Assert.Equal(name, error.Name.Value);
        Assert.Equal(code, error.Error.Code);
    }

    private static SendApiRequestUseCase CreateUseCase(IHttpSender sender, InMemoryApiStore? store = null,
        TimeProvider? clock = null, IApiCollectionWriter? collectionWriter = null)
    {
        if (store is null)
        {
            store = new InMemoryApiStore();
            store.Add(ApiCollection.Create(CollectionId(), "Collection").AssertSuccess());
        }

        return new SendApiRequestUseCase(sender, clock ?? TimeProvider.System, store, store, store,
            store, collectionWriter ?? store, store);
    }

    private static ApiCollectionId CollectionId() => ApiCollectionId.Create("collection").AssertSuccess();
    private static ApiFolderId FolderId(string id) => ApiFolderId.Create(id).AssertSuccess();

    private static VarValue Variable(string name, string? value, bool enabled = true) =>
        VarValue.Create(VarName.Create(name).AssertSuccess(), new StringWithVars(value), enabled).AssertSuccess();


    private static ApiRequest Request(string? url = "https://example.test/api", string? body = null,
        IEnumerable<HeaderValue?>? headers = null, IEnumerable<VarValue?>? varsOut = null)
    {
        var id = ApiRequestId.Create("request");
        var collection = ApiCollectionId.Create("collection");
        var result = ApiRequest.CreateInCollection(id.AssertSuccess(), collection.AssertSuccess(), "Request", RequestMethod.Post,
            new StringWithVars(url), headers, new StringWithVars(body), varsOut);
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

    private sealed class TrackingCollectionWriter(
        Func<ApiCollectionId, IReadOnlyDictionary<VarName, StringWithVars>, CancellationToken,
            Task<Result<IReadOnlyList<VarName>>>> update) : IApiCollectionWriter
    {
        private readonly Func<ApiCollectionId, IReadOnlyDictionary<VarName, StringWithVars>, CancellationToken,
            Task<Result<IReadOnlyList<VarName>>>> _update = update;

        public List<IReadOnlyDictionary<VarName, StringWithVars>> Batches { get; } = [];
        public CancellationToken Token { get; private set; }

        public Task<Result<IReadOnlyList<VarName>>> UpdateVariablesAsync(ApiCollectionId id,
            IReadOnlyDictionary<VarName, StringWithVars> values, CancellationToken cancellationToken)
        {
            Batches.Add(values);
            Token = cancellationToken;
            return _update(id, values, cancellationToken);
        }
    }

    private sealed class BarrierHttpSender : IHttpSender
    {
        private readonly TaskCompletionSource _bothSent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public async Task<Result<HttpResponseData>> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 2)
            {
                _bothSent.SetResult();
            }

            await _bothSent.Task.WaitAsync(cancellationToken);
            return new HttpResponseData(200, "OK", [], string.Empty);
        }
    }

    private sealed class FakeHttpSender : IHttpSender
    {
        public HttpRequestData? Request { get; private set; }
        public CancellationToken Token { get; private set; }
        public Action? OnSend { get; init; }
        public Error? Error { get; init; }
        public HttpResponseData Response { get; init; } = new(201, "Created", [new("X-Response", "1")], "response");

        public Task<Result<HttpResponseData>> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
        {
            Request = request;
            Token = cancellationToken;
            OnSend?.Invoke();
            return Task.FromResult<Result<HttpResponseData>>(Error is null ? Response : Error);
        }
    }
}
