using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed class ApiEnvironment : IEquatable<ApiEnvironment>
{
    public ApiEnvironmentId Id { get; }
    public ItemName Name { get; }
    public IReadOnlyList<VarValue> Variables { get; }

    private ApiEnvironment(ApiEnvironmentId id, ItemName name, IReadOnlyList<VarValue> variables)
    {
        Id = id;
        Name = name;
        Variables = variables;
    }

    public static Result<ApiEnvironment> Create(
        ApiEnvironmentId? id,
        string? name,
        IEnumerable<VarValue?>? variables = null)
    {
        if (id is null)
        {
            return new Error(ErrorsReasons.IdNull, DomainErrorCodes.ApiEnvironmentIdRequired);
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

        return new ApiEnvironment(id, (ItemName)itemName.Value, (IReadOnlyList<VarValue>)variableList.Value);
    }

    public (ApiEnvironment Entity, IReadOnlyList<VarName> Missing) WithVariableValues(
        IReadOnlyDictionary<VarName, StringWithVars> values)
    {
        var replacement = VarValueList.ReplaceValues(Variables, values);
        return (new ApiEnvironment(Id, Name, replacement.Variables), replacement.Missing);
    }

    public bool Equals(ApiEnvironment? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as ApiEnvironment);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ApiEnvironment? left, ApiEnvironment? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ApiEnvironment? left, ApiEnvironment? right) => !(left == right);
}
