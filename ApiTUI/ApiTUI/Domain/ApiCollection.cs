using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed class ApiCollection : IEquatable<ApiCollection>
{
    public ApiCollectionId Id { get; }
    public ItemName Name { get; }

    private ApiCollection(ApiCollectionId id, ItemName name)
    {
        Id = id;
        Name = name;
    }

    public static Result<ApiCollection> Create(ApiCollectionId? id, string? name)
    {
        if (id is null)
        {
            return Result<ApiCollection>.Failure(ErrorsReasons.IdNull, DomainErrorCodes.ApiCollectionIdRequired);
        }

        var itemName = ItemName.Create(name);
        if (!itemName.IsSuccess)
        {
            return Result<ApiCollection>.Failure(itemName.Error);
        }

        return Result<ApiCollection>.Success(new ApiCollection(id, itemName.Value));
    }

    public bool Equals(ApiCollection? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as ApiCollection);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ApiCollection? left, ApiCollection? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ApiCollection? left, ApiCollection? right) => !(left == right);
}
