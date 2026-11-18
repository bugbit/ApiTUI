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

        Assert.True(result.IsSuccess);
        var request = result.Value;
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

        Assert.True(result.IsSuccess);
        var request = result.Value;
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

        Assert.True(result.IsSuccess);
        Assert.Equal(RequestMethod.Get, result.Value.Method);
        Assert.NotNull(result.Value.Url);
        Assert.Null(result.Value.Url.Text);
        Assert.NotNull(result.Value.Body);
        Assert.Null(result.Value.Body.Text);
        Assert.Empty(result.Value.Headers);
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

        Assert.True(result.IsSuccess);
        Assert.Equal(method, result.Value.Method);
    }

    [Fact]
    public void CreateInCollection_InvalidName_PropagatesItemNameError()
    {
        var nameError = ItemName.Create("CON");
        Assert.False(nameError.IsSuccess);

        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "CON");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(nameError.Error.Code, result.Error.Code);
        Assert.Equal(nameError.Error.Args, result.Error.Args);
    }

    [Fact]
    public void CreateInFolder_InvalidName_PropagatesItemNameError()
    {
        var nameError = ItemName.Create("name.");
        Assert.False(nameError.IsSuccess);

        var result = ApiRequest.CreateInFolder(RequestId(), FolderId(), "name.");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(nameError.Error.Code, result.Error.Code);
        Assert.Equal(nameError.Error.Args, result.Error.Args);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void CreateInCollection_UndefinedMethod_ReturnsInvalidInput(int method)
    {
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request", (RequestMethod)method);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ApiRequestMethodInvalid, result.Error.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_NullRequestId_ReturnsIdNull(bool inFolder)
    {
        var result = inFolder
            ? ApiRequest.CreateInFolder(null, FolderId(), "Request")
            : ApiRequest.CreateInCollection(null, CollectionId(), "Request");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.IdNull, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ApiRequestIdRequired, result.Error.Code);
        Assert.Empty(result.Error.Args);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_NullLocationId_ReturnsIdNull(bool inFolder)
    {
        var result = inFolder
            ? ApiRequest.CreateInFolder(RequestId(), null, "Request")
            : ApiRequest.CreateInCollection(RequestId(), null, "Request");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.IdNull, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ApiRequestLocationRequired, result.Error.Code);
    }

    [Fact]
    public void CreateInCollection_EmptyUrlAndBody_PreservesWrappers()
    {
        var url = new StringWithVars("");
        var body = new StringWithVars("");

        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Draft", url: url, body: body);

        Assert.True(result.IsSuccess);
        Assert.Same(url, result.Value.Url);
        Assert.Same(body, result.Value.Body);
    }

    [Fact]
    public void CreateInCollection_NullHeader_ReturnsInvalidInput()
    {
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request",
            headers: [Header("Accept", "application/json"), null]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ApiRequestHeaderNull, result.Error.Code);
    }

    [Fact]
    public void CreateInCollection_Headers_PreservesOrderAndDuplicatesAndCopiesSource()
    {
        var first = Header("Accept", "application/json");
        var second = Header("accept", "text/plain", enabled: false);
        var third = Header("X-Custom", "{{value}}");
        var headers = new List<HeaderValue?> { first, second, third };
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request", headers: headers);
        Assert.True(result.IsSuccess);

        headers[0] = third;
        headers.Clear();

        Assert.Equal(new[] { first, second, third }, result.Value.Headers);
        Assert.False(result.Value.Headers[1].Enabled);
        Assert.Equal("{{value}}", result.Value.Headers[2].Value.Text);
    }

    [Fact]
    public void Headers_ExposedCollection_CannotBeModified()
    {
        var header = Header("Accept", "application/json");
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request", headers: [header]);
        Assert.True(result.IsSuccess);
        var list = Assert.IsAssignableFrom<IList<HeaderValue>>(result.Value.Headers);

        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = Header("Accept", "text/plain"));
        Assert.Same(header, Assert.Single(result.Value.Headers));
    }

    [Fact]
    public void Equality_SameIdDifferentProperties_AreEqual()
    {
        var first = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request");
        var second = ApiRequest.CreateInFolder(RequestId(), FolderId(), "Renamed", RequestMethod.Post);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.Equal(first.Value, second.Value);
        Assert.True(first.Value == second.Value);
        Assert.False(first.Value != second.Value);
        Assert.Equal(first.Value.GetHashCode(), second.Value.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentIdSameProperties_AreNotEqual()
    {
        var first = ApiRequest.CreateInCollection(RequestId("request-1"), CollectionId(), "Request");
        var second = ApiRequest.CreateInCollection(RequestId("request-2"), CollectionId(), "Request");
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.NotEqual(first.Value, second.Value);
        Assert.True(first.Value != second.Value);
    }

    [Fact]
    public void Equality_ComparedWithNull_IsNotEqual()
    {
        var result = ApiRequest.CreateInCollection(RequestId(), CollectionId(), "Request");
        Assert.True(result.IsSuccess);
        ApiRequest? none = null;

        Assert.False(result.Value.Equals(none));
        Assert.False(result.Value == none);
        Assert.True(none == null);
    }

    private static ApiRequestId RequestId(string id = "request-1")
    {
        var result = ApiRequestId.Create(id);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ApiCollectionId CollectionId()
    {
        var result = ApiCollectionId.Create("collection-1");
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ApiFolderId FolderId()
    {
        var result = ApiFolderId.Create("folder-1");
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static HeaderValue Header(string name, string value, bool enabled = true)
    {
        var headerName = HeaderName.Create(name);
        Assert.True(headerName.IsSuccess);
        var header = HeaderValue.Create(headerName.Value, new StringWithVars(value), enabled);
        Assert.True(header.IsSuccess);
        return header.Value;
    }
}
