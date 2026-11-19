using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed record HeaderValue
{
    public HeaderName Name { get; }
    public StringWithVars Value { get; }
    public bool Enabled { get; init; }

    private HeaderValue(HeaderName name, StringWithVars value, bool enabled)
    {
        Name = name;
        Value = value;
        Enabled = enabled;
    }

    public static Result<HeaderValue> Create(HeaderName? name, StringWithVars? value, bool enabled = true)
    {
        if (name is null)
        {
            return new Error(ErrorsReasons.InvalidInput, DomainErrorCodes.HeaderValueNameRequired);
        }

        return new HeaderValue(name, value ?? new StringWithVars(null), enabled);
    }
}
