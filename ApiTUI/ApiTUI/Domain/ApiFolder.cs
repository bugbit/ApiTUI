using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed class ApiFolder : IEquatable<ApiFolder>
{
    public ApiFolderId Id { get; }
    public ItemName Name { get; }
    public ApiCollectionId? CollectionId { get; }
    public ApiFolderId? ParentFolderId { get; }

    private ApiFolder(ApiFolderId id, ItemName name, ApiCollectionId? collectionId, ApiFolderId? parentFolderId)
    {
        Id = id;
        Name = name;
        CollectionId = collectionId;
        ParentFolderId = parentFolderId;
    }

    public static Result<ApiFolder> CreateInCollection(ApiFolderId? id, ApiCollectionId? collectionId, string? name) =>
        Create(id, collectionId, null, name);

    public static Result<ApiFolder> CreateInFolder(ApiFolderId? id, ApiFolderId? parentFolderId, string? name) =>
        Create(id, null, parentFolderId, name);

    private static Result<ApiFolder> Create(
        ApiFolderId? id,
        ApiCollectionId? collectionId,
        ApiFolderId? parentFolderId,
        string? name)
    {
        if (id is null)
        {
            return Result<ApiFolder>.Failure(ErrorsReasons.IdNull, DomainErrorCodes.ApiFolderIdRequired);
        }

        if (collectionId is null && parentFolderId is null)
        {
            return Result<ApiFolder>.Failure(ErrorsReasons.IdNull, DomainErrorCodes.ApiFolderLocationRequired);
        }

        if (parentFolderId == id)
        {
            return Result<ApiFolder>.Failure(ErrorsReasons.InvalidInput, DomainErrorCodes.ApiFolderParentIsSelf);
        }

        var itemName = ItemName.Create(name);
        if (!itemName.IsSuccess)
        {
            return Result<ApiFolder>.Failure(itemName.Error);
        }

        return Result<ApiFolder>.Success(new ApiFolder(id, itemName.Value, collectionId, parentFolderId));
    }

    public bool Equals(ApiFolder? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as ApiFolder);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ApiFolder? left, ApiFolder? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ApiFolder? left, ApiFolder? right) => !(left == right);
}
