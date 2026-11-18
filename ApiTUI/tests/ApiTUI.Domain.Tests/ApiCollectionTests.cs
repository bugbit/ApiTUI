using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class ApiCollectionTests
{
    [Fact]
    public void Create_ValidCollection_PreservesIdAndTrimsName()
    {
        var id = CollectionId();

        var result = ApiCollection.Create(id, "  Favorites  ");

        Assert.True(result.IsSuccess);
        Assert.Same(id, result.Value.Id);
        Assert.Equal("Favorites", result.Value.Name.Value);
        Assert.Empty(result.Value.Variables);
    }

    [Fact]
    public void Create_Variables_PreservesOrderAndInstances()
    {
        var first = Variable("baseUrl", "https://localhost");
        var second = Variable("token", "secret");

        var result = ApiCollection.Create(CollectionId(), "Favorites", [first, second]);

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value.Variables,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    [Fact]
    public void Create_DuplicateVariable_PropagatesVarValueListError()
    {
        var result = ApiCollection.Create(CollectionId(), "Favorites",
            [Variable("token", "first"), Variable("token", "second")]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListDuplicated, result.Error.Code);
        Assert.Equal("token", Assert.Single(result.Error.Args));
    }

    [Fact]
    public void Create_NullId_ReturnsIdNull()
    {
        var result = ApiCollection.Create(null, "Favorites");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.IdNull, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ApiCollectionIdRequired, result.Error.Code);
    }

    [Fact]
    public void Create_InvalidName_PropagatesItemNameError()
    {
        var expected = ItemName.Create("CON");
        Assert.False(expected.IsSuccess);

        var result = ApiCollection.Create(CollectionId(), "CON");

        Assert.False(result.IsSuccess);
        Assert.Equal(expected.Error.Code, result.Error.Code);
        Assert.Equal(expected.Error.Args, result.Error.Args);
    }

    [Fact]
    public void Equality_SameIdDifferentName_AreEqual()
    {
        var first = ApiCollection.Create(CollectionId("collection-1"), "First");
        var second = ApiCollection.Create(CollectionId("collection-1"), "Second");
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.Equal(first.Value, second.Value);
        Assert.True(first.Value == second.Value);
        Assert.False(first.Value != second.Value);
        Assert.Equal(first.Value.GetHashCode(), second.Value.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var first = ApiCollection.Create(CollectionId("collection-1"), "First");
        var second = ApiCollection.Create(CollectionId("collection-2"), "First");
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.NotEqual(first.Value, second.Value);
        Assert.True(first.Value != second.Value);
    }

    private static ApiCollectionId CollectionId(string value = "collection-1")
    {
        var result = ApiCollectionId.Create(value);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static VarValue Variable(string name, string value)
    {
        var varName = VarName.Create(name);
        Assert.True(varName.IsSuccess);
        var variable = VarValue.Create(varName.Value, new StringWithVars(value));
        Assert.True(variable.IsSuccess);
        return variable.Value;
    }
}
