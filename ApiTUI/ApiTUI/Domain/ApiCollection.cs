using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed class ApiCollection : IEquatable<ApiCollection>
{
    public ApiCollectionId Id { get; }
    public ItemName Name { get; }
    public IReadOnlyList<VarValue> Variables { get; }

    private ApiCollection(ApiCollectionId id, ItemName name, IReadOnlyList<VarValue> variables)
    {
        Id = id;
        Name = name;
        Variables = variables;
    }

    public static Result<ApiCollection> Create(
        ApiCollectionId? id,
        string? name,
        IEnumerable<VarValue?>? variables = null)
    {
        if (id is null)
        {
            return new Error(ErrorsReasons.IdNull, DomainErrorCodes.ApiCollectionIdRequired);
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

        return new ApiCollection(id, (ItemName)itemName.Value, (IReadOnlyList<VarValue>)variableList.Value);
    }

    public (ApiCollection Entity, IReadOnlyList<VarName> Missing) WithVariableValues(
        IReadOnlyDictionary<VarName, StringWithVars> values)
    {
        var replacement = VarValueList.ReplaceValues(Variables, values);
        return (new ApiCollection(Id, Name, replacement.Variables), replacement.Missing);
    }

    public bool Equals(ApiCollection? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as ApiCollection);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ApiCollection? left, ApiCollection? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ApiCollection? left, ApiCollection? right) => !(left == right);
}
