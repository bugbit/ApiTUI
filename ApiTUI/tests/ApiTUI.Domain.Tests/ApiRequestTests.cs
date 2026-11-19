using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class ApiRequestTests
{
    [Fact]
    public void CreateInCollection_CompleteRequest_PreservesProperties()
    {
        var id = RequestId();
        var collectionId = CollectionId();
        var url = new StringWithVars("{{baseUrl}}/login");
        var body = new StringWithVars("{{credentials}}");
        var header = Header("Content-Type", "application/json");

        var result = ApiRequest.CreateInCollection(id, collectionId, "Login", RequestMethod.Post,
            url, [header], body);

        var request = result.AssertSuccess();
        Assert.Same(id, request.Id);
        Assert.Equal("Login", request.Name.Value);
        Assert.Equal(RequestMethod.Post, request.Method);
        Assert.Same(url, request.Url);
        Assert.Same(body, request.Body);
        Assert.Same(header, Assert.Single(request.Headers));
        Assert.Same(collectionId, request.CollectionId);
        Assert.Null(request.FolderId);
    }

    [Fact]
    public void CreateInFolder_CompleteRequest_PreservesProperties()
    {
        var id = RequestId();
        var folderId = FolderId();
        var url = new StringWithVars("{{baseUrl}}/search");
        var body = new StringWithVars("{{query}}");
        var header = Header("Accept", "application/json", enabled: false);

        var result = ApiRequest.CreateInFolder(id, folderId, "Search", RequestMethod.Query, url, [header], body);

        var request = result.AssertSuccess();
        Assert.Same(id, request.Id);
        Assert.Equal("Search", request.Name.Value);
        Assert.Equal(RequestMethod.Query, request.Method);
        Assert.Same(url, request.Url);
        Assert.Same(body, request.Body);
        Assert.Same(header, Assert.Single(request.Headers));
        Assert.Same(folderId, request.FolderId);
        Assert.Null(request.CollectionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_Defaults_ProducesEmptyDraft(bool inFolder)
    {
        var result = inFolder
            ? ApiRequest.CreateInFolder(RequestId(), FolderId(), "Draft")
            : ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Draft");

        var value = result.AssertSuccess();
        Assert.Equal(RequestMethod.Get, value.Method);
        Assert.NotNull(value.Url);
        Assert.Null(value.Url.Text);
        Assert.NotNull(value.Body);
        Assert.Null(value.Body.Text);
        Assert.Empty(value.Headers);
        Assert.Null(value.VarsOut);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_EmptyVarsOut_PreservesEmptyList(bool inFolder)
    {
        var result = inFolder
            ? ApiRequest.CreateInFolder(RequestId(), FolderId(), "Request", varsOut: [])
            : ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request", varsOut: []);

        var varsOut = result.AssertSuccess().VarsOut;
        Assert.NotNull(varsOut);
        Assert.Empty(varsOut);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_VarsOut_PreservesOrderAndValuesAndCopiesSource(bool inFolder)
    {
        var first = Var("token", "{{responseToken}}");
        var second = Var("id", "42", enabled: false);
        var source = new List<VarValue?> { first, second };

        var result = inFolder
            ? ApiRequest.CreateInFolder(RequestId(), FolderId(), "Request", varsOut: source)
            : ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request", varsOut: source);

        source.Clear();

        var value = result.AssertSuccess();
        Assert.NotNull(value.VarsOut);
        Assert.Equal(new[] { first, second }, value.VarsOut);
    }

    [Fact]
    public void VarsOut_ExposedCollection_CannotBeModified()
    {
        var variable = Var("token", "value");
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request", varsOut: [variable]);
        var list = Assert.IsAssignableFrom<IList<VarValue>>(result.AssertSuccess().VarsOut);

        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = Var("other", "replacement"));
        Assert.Same(variable, Assert.Single(list));
    }

    [Fact]
    public void CreateInCollection_NullVarsOutElement_ReturnsInvalidInput()
    {
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request",
            varsOut: [Var("token", "value"), null]);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListItemNull, error.Code);
    }

    [Fact]
    public void CreateInFolder_DuplicateVarsOutName_ReturnsNameAndInvalidInput()
    {
        var result = ApiRequest.CreateInFolder(RequestId(), FolderId(), "Request",
            varsOut: [Var("token", "first"), Var("token", "second", enabled: false)]);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListDuplicated, error.Code);
        Assert.Equal("token", Assert.Single(error.Args));
    }

    [Theory]
    [InlineData(RequestMethod.Get)]
    [InlineData(RequestMethod.Post)]
    [InlineData(RequestMethod.Put)]
    [InlineData(RequestMethod.Patch)]
    [InlineData(RequestMethod.Delete)]
    [InlineData(RequestMethod.Head)]
    [InlineData(RequestMethod.Options)]
    [InlineData(RequestMethod.Query)]
    public void CreateInCollection_DefinedMethod_IsValid(RequestMethod method)
    {
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request", method);

        Assert.Equal(method, result.AssertSuccess().Method);
    }

    [Fact]
    public void CreateInCollection_InvalidName_PropagatesItemNameError()
    {
        var nameError = ItemName.Create("CON");

        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "CON");

        Assert.Equal(ErrorsReasons.InvalidInput, result.AssertFailure().Reason);
        Assert.Equal(nameError.AssertFailure().Code, result.AssertFailure().Code);
        Assert.Equal(nameError.AssertFailure().Args, result.AssertFailure().Args);
    }

    [Fact]
    public void CreateInFolder_InvalidName_PropagatesItemNameError()
    {
        var nameError = ItemName.Create("name.");

        var result = ApiRequest.CreateInFolder(RequestId(), FolderId(), "name.");

        Assert.Equal(ErrorsReasons.InvalidInput, result.AssertFailure().Reason);
        Assert.Equal(nameError.AssertFailure().Code, result.AssertFailure().Code);
        Assert.Equal(nameError.AssertFailure().Args, result.AssertFailure().Args);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void CreateInCollection_UndefinedMethod_ReturnsInvalidInput(int method)
    {
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request", (RequestMethod)method);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.ApiRequestMethodInvalid, error.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_NullRequestId_ReturnsIdNull(bool inFolder)
    {
        var result = inFolder
            ? ApiRequest.CreateInFolder(null, FolderId(), "Request")
            : ApiRequest.CreateInCollection(null, CollectionId(), "Request");

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.IdNull, error.Reason);
        Assert.Equal(DomainErrorCodes.ApiRequestIdRequired, error.Code);
        Assert.Empty(error.Args);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_NullLocationId_ReturnsIdNull(bool inFolder)
    {
        var result = inFolder
            ? ApiRequest.CreateInFolder(RequestId(), null, "Request")
            : ApiRequest.CreateInCollection(RequestId(), null, "Request");

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.IdNull, error.Reason);
        Assert.Equal(DomainErrorCodes.ApiRequestLocationRequired, error.Code);
    }

    [Fact]
    public void CreateInCollection_EmptyUrlAndBody_PreservesWrappers()
    {
        var url = new StringWithVars("");
        var body = new StringWithVars("");

        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Draft", url: url, body: body);

        var value = result.AssertSuccess();
        Assert.Same(url, value.Url);
        Assert.Same(body, value.Body);
    }

    [Fact]
    public void CreateInCollection_NullHeader_ReturnsInvalidInput()
    {
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request",
            headers: [Header("Accept", "application/json"), null]);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.ApiRequestHeaderNull, error.Code);
    }

    [Fact]
    public void CreateInCollection_Headers_PreservesOrderAndDuplicatesAndCopiesSource()
    {
        var first = Header("Accept", "application/json");
        var second = Header("accept", "text/plain", enabled: false);
        var third = Header("X-Custom", "{{value}}");
        var headers = new List<HeaderValue?> { first, second, third };
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request", headers: headers);

        headers[0] = third;
        headers.Clear();

        var value = result.AssertSuccess();
        Assert.Equal(new[] { first, second, third }, value.Headers);
        Assert.False(value.Headers[1].Enabled);
        Assert.Equal("{{value}}", value.Headers[2].Value.Text);
    }

    [Fact]
    public void Headers_ExposedCollection_CannotBeModified()
    {
        var header = Header("Accept", "application/json");
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request", headers: [header]);
        var value = result.AssertSuccess();
        var list = Assert.IsAssignableFrom<IList<HeaderValue>>(value.Headers);

        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = Header("Accept", "text/plain"));
        Assert.Same(header, Assert.Single(value.Headers));
    }

    [Fact]
    public void Equality_SameIdDifferentProperties_AreEqual()
    {
        var first = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request");
        var second = ApiRequest.CreateInFolder(RequestId(), FolderId(), "Renamed", RequestMethod.Post,
            varsOut: [Var("token", "value")]);

        Assert.Equal(first.AssertSuccess(), second.AssertSuccess());
        Assert.True(first.AssertSuccess() == second.AssertSuccess());
        Assert.False(first.AssertSuccess() != second.AssertSuccess());
        Assert.Equal(first.AssertSuccess().GetHashCode(), second.AssertSuccess().GetHashCode());
    }

    [Fact]
    public void Equality_DifferentIdSameProperties_AreNotEqual()
    {
        var first = ApiRequest.CreateInCollection(RequestId("request-1"), CollectionId(), "Request");
        var second = ApiRequest.CreateInCollection(RequestId("request-2"), CollectionId(), "Request");

        Assert.NotEqual(first.AssertSuccess(), second.AssertSuccess());
        Assert.True(first.AssertSuccess() != second.AssertSuccess());
    }

    [Fact]
    public void Equality_ComparedWithNull_IsNotEqual()
    {
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request");
        ApiRequest? none = null;

        var value = result.AssertSuccess();
        Assert.False(value.Equals(none));
        Assert.False(value == none);
        Assert.True(none == null);
    }

    private static ApiRequestId RequestId(string id = "request-1")
    {
        var result = ApiRequestId.Create(id);
        return result.AssertSuccess();
    }

    private static ApiCollectionId CollectionId()
    {
        var result = ApiCollectionId.Create("collection-1");
        return result.AssertSuccess();
    }

    private static ApiFolderId FolderId()
    {
        var result = ApiFolderId.Create("folder-1");
        return result.AssertSuccess();
    }

    private static HeaderValue Header(string name, string value, bool enabled = true)
    {
        var headerName = HeaderName.Create(name);
        var header = HeaderValue.Create(headerName.AssertSuccess(), new StringWithVars(value), enabled);
        return header.AssertSuccess();
    }

    private static VarValue Var(string name, string value, bool enabled = true)
    {
        var varName = VarName.Create(name);
        var variable = VarValue.Create(varName.AssertSuccess(), new StringWithVars(value), enabled);
        return variable.AssertSuccess();
    }
}
