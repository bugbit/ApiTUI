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

        Assert.True(result.IsSuccess);
        Assert.Same(id, result.Value.Id);
        Assert.Equal("Auth", result.Value.Name.Value);
        Assert.Same(collectionId, result.Value.CollectionId);
        Assert.Null(result.Value.ParentFolderId);
    }

    [Fact]
    public void CreateInFolder_PreservesParentFolderLocation()
    {
        var id = FolderId("folder-2");
        var parentFolderId = FolderId("folder-1");

        var result = ApiFolder.CreateInFolder(id, parentFolderId, "OAuth");

        Assert.True(result.IsSuccess);
        Assert.Same(id, result.Value.Id);
        Assert.Equal("OAuth", result.Value.Name.Value);
        Assert.Null(result.Value.CollectionId);
        Assert.Same(parentFolderId, result.Value.ParentFolderId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_NullId_ReturnsIdNull(bool inFolder)
    {
        var result = inFolder
            ? ApiFolder.CreateInFolder(null, FolderId(), "Folder")
            : ApiFolder.CreateInCollection(null, CollectionId(), "Folder");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.IdNull, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ApiFolderIdRequired, result.Error.Code);
    }

    [Fact]
    public void CreateInCollection_NullLocation_ReturnsIdNull()
    {
        var result = ApiFolder.CreateInCollection(FolderId(), null, "Folder");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.IdNull, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ApiFolderLocationRequired, result.Error.Code);
    }

    [Fact]
    public void CreateInFolder_NullLocation_ReturnsIdNull()
    {
        var result = ApiFolder.CreateInFolder(FolderId(), null, "Folder");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.IdNull, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ApiFolderLocationRequired, result.Error.Code);
    }

    [Fact]
    public void CreateInFolder_ParentIsSelf_ReturnsInvalidInput()
    {
        var id = FolderId();

        var result = ApiFolder.CreateInFolder(id, FolderId(), "Folder");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ApiFolderParentIsSelf, result.Error.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_InvalidName_PropagatesItemNameError(bool inFolder)
    {
        var expected = ItemName.Create("CON");
        Assert.False(expected.IsSuccess);

        var result = inFolder
            ? ApiFolder.CreateInFolder(FolderId("folder-2"), FolderId("folder-1"), "CON")
            : ApiFolder.CreateInCollection(FolderId(), CollectionId(), "CON");

        Assert.False(result.IsSuccess);
        Assert.Equal(expected.Error.Code, result.Error.Code);
        Assert.Equal(expected.Error.Args, result.Error.Args);
    }

    [Fact]
    public void Equality_SameIdDifferentProperties_AreEqual()
    {
        var first = ApiFolder.CreateInCollection(FolderId("folder-1"), CollectionId(), "First");
        var second = ApiFolder.CreateInFolder(FolderId("folder-1"), FolderId("parent"), "Second");
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
        var first = ApiFolder.CreateInCollection(FolderId("folder-1"), CollectionId(), "Folder");
        var second = ApiFolder.CreateInCollection(FolderId("folder-2"), CollectionId(), "Folder");
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.NotEqual(first.Value, second.Value);
        Assert.True(first.Value != second.Value);
    }

    private static ApiFolderId FolderId(string value = "folder-1")
    {
        var result = ApiFolderId.Create(value);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ApiCollectionId CollectionId()
    {
        var result = ApiCollectionId.Create("collection-1");
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
