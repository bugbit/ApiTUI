using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed record HeaderName
{
    private const string ExtraTokenChars = "!#$%&'*+-.^_`|~";

    public string Value { get; }

    private HeaderName(string value) => Value = value;

    public static Result<HeaderName> Create(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result<HeaderName>.Failure(ErrorsReasons.InvalidInput, "El nombre de la cabecera no puede estar vacío.");
        }

        var invalidChars = value.Where(c => !IsTokenChar(c)).Distinct().ToArray();
        if (invalidChars.Length > 0)
        {
            return Result<HeaderName>.Failure(ErrorsReasons.InvalidInput,
                $"El nombre de la cabecera contiene caracteres no permitidos: {Describe(invalidChars)}. Solo se admiten letras ASCII, dígitos y !#$%&'*+-.^_`|~.");
        }

        return Result<HeaderName>.Success(new HeaderName(value));
    }

    private static bool IsTokenChar(char c) => char.IsAsciiLetterOrDigit(c) || ExtraTokenChars.Contains(c);

    private static string Describe(IEnumerable<char> characters) => string.Join(", ", characters.Select(c => c switch
    {
        ' ' => "' '",
        '\t' => "'\\t'",
        '\r' => "'\\r'",
        '\n' => "'\\n'",
        '\0' => "'\\0'",
        _ when char.IsControl(c) || char.IsWhiteSpace(c) || char.IsSurrogate(c) => $"U+{(int)c:X4}",
        _ => $"'{c}'",
    }));

    public bool Equals(HeaderName? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;
}
