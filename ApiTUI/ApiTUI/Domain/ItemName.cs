using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed record ItemName
{
    // Los nombres se usan en rutas de fichero; se aplica el conjunto más restrictivo de Windows.
    private const string InvalidNameChars = "<>:\"/\\|?*";

    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    public string Value { get; }

    private ItemName(string value) => Value = value;

    public static Result<ItemName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<ItemName>.Failure(ErrorsReasons.InvalidInput, DomainErrorCodes.ItemNameEmpty);
        }

        var trimmedValue = value.Trim();
        if (trimmedValue.Any(c => char.IsControl(c) || InvalidNameChars.Contains(c)))
        {
            return Result<ItemName>.Failure(ErrorsReasons.InvalidInput,
                DomainErrorCodes.ItemNameInvalidChars, InvalidNameChars);
        }

        if (trimmedValue.EndsWith('.'))
        {
            return Result<ItemName>.Failure(ErrorsReasons.InvalidInput, DomainErrorCodes.ItemNameEndsWithDot);
        }

        var baseName = trimmedValue.Split('.')[0].TrimEnd();
        if (ReservedNames.Contains(baseName, StringComparer.OrdinalIgnoreCase))
        {
            return Result<ItemName>.Failure(ErrorsReasons.InvalidInput, DomainErrorCodes.ItemNameReserved, baseName);
        }

        return Result<ItemName>.Success(new ItemName(trimmedValue));
    }

    public override string ToString() => Value;
}
