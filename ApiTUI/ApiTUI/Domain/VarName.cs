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

        foreach (var c in value)
        {
            if (!IsAllowed(c))
            {
                return Result<VarName>.Failure(ErrorsReasons.InvalidInput,
                    $"El nombre de la variable contiene el carácter no permitido '{c}'. Solo se admiten letras, dígitos, '-', '_' y '.'.");
            }
        }

        return Result<VarName>.Success(new VarName(value));
    }

    private static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.';

    public override string ToString() => Value;
}
