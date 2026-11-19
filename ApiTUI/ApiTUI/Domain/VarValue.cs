using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed record VarValue
{
    public VarName Name { get; }
    public StringWithVars Value { get; }
    public bool Enabled { get; init; }

    private VarValue(VarName name, StringWithVars value, bool enabled)
    {
        Name = name;
        Value = value;
        Enabled = enabled;
    }

    internal VarValue WithValue(StringWithVars value) => new(Name, value, Enabled);

    public static Result<VarValue> Create(VarName? name, StringWithVars? value, bool enabled = true)
    {
        if (name is null)
        {
            return new Error(ErrorsReasons.InvalidInput, DomainErrorCodes.VarValueNameRequired);
        }

        return new VarValue(name, value ?? new StringWithVars(null), enabled);
    }
}
