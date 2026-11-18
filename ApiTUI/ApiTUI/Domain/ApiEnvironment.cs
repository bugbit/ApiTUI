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
            return Result<ApiEnvironment>.Failure(ErrorsReasons.IdNull, DomainErrorCodes.ApiEnvironmentIdRequired);
        }

        var itemName = ItemName.Create(name);
        if (!itemName.IsSuccess)
        {
            return Result<ApiEnvironment>.Failure(itemName.Error);
        }

        var variableList = VarValueList.Create(variables);
        if (!variableList.IsSuccess)
        {
            return Result<ApiEnvironment>.Failure(variableList.Error);
        }

        return Result<ApiEnvironment>.Success(new ApiEnvironment(id, itemName.Value, variableList.Value));
    }

    public bool Equals(ApiEnvironment? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as ApiEnvironment);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ApiEnvironment? left, ApiEnvironment? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ApiEnvironment? left, ApiEnvironment? right) => !(left == right);
}
