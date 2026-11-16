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
            return Result<VarName>.Failure(ErrorsReasons.InvalidInput, "El nombre de la variable no puede estar vacío.");
        }

        var invalidChars = value.Where(c => !IsAllowed(c)).Distinct().ToArray();
        if (invalidChars.Length > 0)
        {
            return Result<VarName>.Failure(ErrorsReasons.InvalidInput,
                $"El nombre de la variable contiene caracteres no permitidos: {string.Join(", ", invalidChars.Select(c => $"'{c}'"))}. Solo se admiten letras ASCII (a-z, A-Z), dígitos (0-9), '-', '_' y '.'.");
        }

        return Result<VarName>.Success(new VarName(value));
    }

    private static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.';

    public override string ToString() => Value;
}
