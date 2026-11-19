using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed record VarName
{
    public string Value { get; }

    private VarName(string value) => Value = value;

    public static Result<VarName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Error(ErrorsReasons.InvalidInput, DomainErrorCodes.VarNameEmpty);
        }

        var invalidChars = value.Where(c => !IsAllowed(c)).Distinct().ToArray();
        if (invalidChars.Length > 0)
        {
            return new Error(ErrorsReasons.InvalidInput,
                DomainErrorCodes.VarNameInvalidChars, string.Join(", ", invalidChars.Select(c => $"'{c}'")));
        }

        return new VarName(value);
    }

    private static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.';

    public override string ToString() => Value;
}
