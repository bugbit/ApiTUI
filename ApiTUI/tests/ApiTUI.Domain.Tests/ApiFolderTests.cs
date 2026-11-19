using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class ApiFolderTests
{
    [Fact]
    public void CreateInCollection_PreservesCollectionLocation()
    {
        var id = FolderId("folder-1");
        var collectionId = CollectionId();

        var result = ApiFolder.CreateInCollection(id, collectionId, "  Auth  ");

        var value = result.AssertSuccess();
        Assert.Same(id, value.Id);
        Assert.Equal("Auth", value.Name.Value);
        Assert.Same(collectionId, value.CollectionId);
        Assert.Null(value.ParentFolderId);
        Assert.Empty(value.Variables);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_Variables_PreservesOrderAndInstances(bool inFolder)
    {
        var first = Variable("baseUrl", "https://localhost");
        var second = Variable("token", "secret");

        var result = inFolder
            ? ApiFolder.CreateInFolder(FolderId("folder-2"), FolderId("folder-1"), "Nested", [first, second])
            : ApiFolder.CreateInCollection(FolderId(), CollectionId(), "Root", [first, second]);

        Assert.Collection(result.AssertSuccess().Variables,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_NullVariable_PropagatesVarValueListError(bool inFolder)
    {
        var result = inFolder
            ? ApiFolder.CreateInFolder(FolderId("folder-2"), FolderId("folder-1"), "Nested", [null])
            : ApiFolder.CreateInCollection(FolderId(), CollectionId(), "Root", [null]);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListItemNull, error.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_DuplicateVariable_PropagatesVarValueListError(bool inFolder)
    {
        var variables = new[] { Variable("token", "first"), Variable("token", "second") };
        var result = inFolder
            ? ApiFolder.CreateInFolder(FolderId("folder-2"), FolderId("folder-1"), "Nested", variables)
            : ApiFolder.CreateInCollection(FolderId(), CollectionId(), "Root", variables);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListDuplicated, error.Code);
        Assert.Equal("token", Assert.Single(error.Args));
    }

    [Fact]
    public void CreateInFolder_PreservesParentFolderLocation()
    {
        var id = FolderId("folder-2");
        var parentFolderId = FolderId("folder-1");

        var result = ApiFolder.CreateInFolder(id, parentFolderId, "OAuth");

        var value = result.AssertSuccess();
        Assert.Same(id, value.Id);
        Assert.Equal("OAuth", value.Name.Value);
        Assert.Null(value.CollectionId);
        Assert.Same(parentFolderId, value.ParentFolderId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_WithoutVariables_ReturnsEmptyList(bool inFolder)
    {
        var result = inFolder
            ? ApiFolder.CreateInFolder(FolderId("folder-2"), FolderId("folder-1"), "Nested")
            : ApiFolder.CreateInCollection(FolderId(), CollectionId(), "Root");

        Assert.Empty(result.AssertSuccess().Variables);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_NullId_ReturnsIdNull(bool inFolder)
    {
        var result = inFolder
            ? ApiFolder.CreateInFolder(null, FolderId(), "Folder")
            : ApiFolder.CreateInCollection(null, CollectionId(), "Folder");

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.IdNull, error.Reason);
        Assert.Equal(DomainErrorCodes.ApiFolderIdRequired, error.Code);
    }

    [Fact]
    public void CreateInCollection_NullLocation_ReturnsIdNull()
    {
        var result = ApiFolder.CreateInCollection(FolderId(), null, "Folder");

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.IdNull, error.Reason);
        Assert.Equal(DomainErrorCodes.ApiFolderLocationRequired, error.Code);
    }

    [Fact]
    public void CreateInFolder_NullLocation_ReturnsIdNull()
    {
        var result = ApiFolder.CreateInFolder(FolderId(), null, "Folder");

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.IdNull, error.Reason);
        Assert.Equal(DomainErrorCodes.ApiFolderLocationRequired, error.Code);
    }

    [Fact]
    public void CreateInFolder_ParentIsSelf_ReturnsInvalidInput()
    {
        var id = FolderId();

        var result = ApiFolder.CreateInFolder(id, FolderId(), "Folder");

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.ApiFolderParentIsSelf, error.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_InvalidName_PropagatesItemNameError(bool inFolder)
    {
        var expected = ItemName.Create("CON");

        var result = inFolder
            ? ApiFolder.CreateInFolder(FolderId("folder-2"), FolderId("folder-1"), "CON")
            : ApiFolder.CreateInCollection(FolderId(), CollectionId(), "CON");

        Assert.Equal(expected.AssertFailure().Code, result.AssertFailure().Code);
        Assert.Equal(expected.AssertFailure().Args, result.AssertFailure().Args);
    }

    [Fact]
    public void Equality_SameIdDifferentProperties_AreEqual()
    {
        var first = ApiFolder.CreateInCollection(FolderId("folder-1"), CollectionId(), "First");
        var second = ApiFolder.CreateInFolder(FolderId("folder-1"), FolderId("parent"), "Second");

        Assert.Equal(first.AssertSuccess(), second.AssertSuccess());
        Assert.True(first.AssertSuccess() == second.AssertSuccess());
        Assert.False(first.AssertSuccess() != second.AssertSuccess());
        Assert.Equal(first.AssertSuccess().GetHashCode(), second.AssertSuccess().GetHashCode());
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var first = ApiFolder.CreateInCollection(FolderId("folder-1"), CollectionId(), "Folder");
        var second = ApiFolder.CreateInCollection(FolderId("folder-2"), CollectionId(), "Folder");

        Assert.NotEqual(first.AssertSuccess(), second.AssertSuccess());
        Assert.True(first.AssertSuccess() != second.AssertSuccess());
    }

    private static ApiFolderId FolderId(string value = "folder-1")
    {
        var result = ApiFolderId.Create(value);
        return result.AssertSuccess();
    }

    private static ApiCollectionId CollectionId()
    {
        var result = ApiCollectionId.Create("collection-1");
        return result.AssertSuccess();
    }

    private static VarValue Variable(string name, string value)
    {
        var varName = VarName.Create(name);
        var variable = VarValue.Create(varName.AssertSuccess(), new StringWithVars(value));
        return variable.AssertSuccess();
    }
}
