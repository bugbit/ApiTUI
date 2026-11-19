using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed class ApiFolder : IEquatable<ApiFolder>
{
    public ApiFolderId Id { get; }
    public ItemName Name { get; }
    public ApiCollectionId? CollectionId { get; }
    public ApiFolderId? ParentFolderId { get; }
    public IReadOnlyList<VarValue> Variables { get; }

    private ApiFolder(
        ApiFolderId id,
        ItemName name,
        ApiCollectionId? collectionId,
        ApiFolderId? parentFolderId,
        IReadOnlyList<VarValue> variables)
    {
        Id = id;
        Name = name;
        CollectionId = collectionId;
        ParentFolderId = parentFolderId;
        Variables = variables;
    }

    public static Result<ApiFolder> CreateInCollection(
        ApiFolderId? id,
        ApiCollectionId? collectionId,
        string? name,
        IEnumerable<VarValue?>? variables = null) => Create(id, collectionId, null, name, variables);

    public static Result<ApiFolder> CreateInFolder(
        ApiFolderId? id,
        ApiFolderId? parentFolderId,
        string? name,
        IEnumerable<VarValue?>? variables = null) => Create(id, null, parentFolderId, name, variables);

    private static Result<ApiFolder> Create(
        ApiFolderId? id,
        ApiCollectionId? collectionId,
        ApiFolderId? parentFolderId,
        string? name,
        IEnumerable<VarValue?>? variables)
    {
        if (id is null)
        {
            return new Error(ErrorsReasons.IdNull, DomainErrorCodes.ApiFolderIdRequired);
        }

        if (collectionId is null && parentFolderId is null)
        {
            return new Error(ErrorsReasons.IdNull, DomainErrorCodes.ApiFolderLocationRequired);
        }

        if (parentFolderId == id)
        {
            return new Error(ErrorsReasons.InvalidInput, DomainErrorCodes.ApiFolderParentIsSelf);
        }

        var itemName = ItemName.Create(name);
        if (itemName is Error itemNameError)
        {
            return itemNameError;
        }

        var variableList = VarValueList.Create(variables);
        if (variableList is Error variableListError)
        {
            return variableListError;
        }

        return new ApiFolder(id, (ItemName)itemName.Value, collectionId, parentFolderId,
            (IReadOnlyList<VarValue>)variableList.Value);
    }

    public (ApiFolder Entity, IReadOnlyList<VarName> Missing) WithVariableValues(
        IReadOnlyDictionary<VarName, StringWithVars> values)
    {
        var replacement = VarValueList.ReplaceValues(Variables, values);
        return (new ApiFolder(Id, Name, CollectionId, ParentFolderId, replacement.Variables), replacement.Missing);
    }

    public bool Equals(ApiFolder? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as ApiFolder);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ApiFolder? left, ApiFolder? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ApiFolder? left, ApiFolder? right) => !(left == right);
}
