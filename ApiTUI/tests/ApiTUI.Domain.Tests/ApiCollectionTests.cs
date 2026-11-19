using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class ApiCollectionTests
{
    [Fact]
    public void Create_ValidCollection_PreservesIdAndTrimsName()
    {
        var id = CollectionId();

        var result = ApiCollection.Create(id, "  Favorites  ");

        var value = result.AssertSuccess();
        Assert.Same(id, value.Id);
        Assert.Equal("Favorites", value.Name.Value);
        Assert.Empty(value.Variables);
    }

    [Fact]
    public void Create_Variables_PreservesOrderAndInstances()
    {
        var first = Variable("baseUrl", "https://localhost");
        var second = Variable("token", "secret");

        var result = ApiCollection.Create(CollectionId(), "Favorites", [first, second]);

        Assert.Collection(result.AssertSuccess().Variables,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    [Fact]
    public void Create_DuplicateVariable_PropagatesVarValueListError()
    {
        var result = ApiCollection.Create(CollectionId(), "Favorites",
            [Variable("token", "first"), Variable("token", "second")]);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListDuplicated, error.Code);
        Assert.Equal("token", Assert.Single(error.Args));
    }

    [Fact]
    public void Create_NullId_ReturnsIdNull()
    {
        var result = ApiCollection.Create(null, "Favorites");

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.IdNull, error.Reason);
        Assert.Equal(DomainErrorCodes.ApiCollectionIdRequired, error.Code);
    }

    [Fact]
    public void Create_InvalidName_PropagatesItemNameError()
    {
        var expected = ItemName.Create("CON");

        var result = ApiCollection.Create(CollectionId(), "CON");

        Assert.Equal(expected.AssertFailure().Code, result.AssertFailure().Code);
        Assert.Equal(expected.AssertFailure().Args, result.AssertFailure().Args);
    }

    [Fact]
    public void Equality_SameIdDifferentName_AreEqual()
    {
        var first = ApiCollection.Create(CollectionId("collection-1"), "First");
        var second = ApiCollection.Create(CollectionId("collection-1"), "Second");

        Assert.Equal(first.AssertSuccess(), second.AssertSuccess());
        Assert.True(first.AssertSuccess() == second.AssertSuccess());
        Assert.False(first.AssertSuccess() != second.AssertSuccess());
        Assert.Equal(first.AssertSuccess().GetHashCode(), second.AssertSuccess().GetHashCode());
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var first = ApiCollection.Create(CollectionId("collection-1"), "First");
        var second = ApiCollection.Create(CollectionId("collection-2"), "First");

        Assert.NotEqual(first.AssertSuccess(), second.AssertSuccess());
        Assert.True(first.AssertSuccess() != second.AssertSuccess());
    }

    private static ApiCollectionId CollectionId(string value = "collection-1")
    {
        var result = ApiCollectionId.Create(value);
        return result.AssertSuccess();
    }

    private static VarValue Variable(string name, string value)
    {
        var varName = VarName.Create(name);
        var variable = VarValue.Create(varName.AssertSuccess(), new StringWithVars(value));
        return variable.AssertSuccess();
    }
}
