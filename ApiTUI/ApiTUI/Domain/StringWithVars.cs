using System.Globalization;
using System.Text.RegularExpressions;
using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed partial record StringWithVars(string? Text)
{
    // Captura cualquier contenido entre {{ y }}; VarName.Create decide si es un nombre válido.
    [GeneratedRegex(@"\{\{([^{}]*)\}\}")]
    private static partial Regex VarPattern();

    public IReadOnlyList<VarName> GetVarNames()
    {
        if (string.IsNullOrEmpty(Text))
        {
            return [];
        }

        var names = new List<VarName>();
        var seen = new HashSet<VarName>();
        foreach (Match match in VarPattern().Matches(Text))
        {
            var name = VarName.Create(match.Groups[1].Value);
            if (name is VarName varName && seen.Add(varName))
            {
                names.Add(varName);
            }
        }

        return names.AsReadOnly();
    }

    public IReadOnlyList<string> GetInvalidPlaceholders()
    {
        if (string.IsNullOrEmpty(Text))
        {
            return [];
        }

        var invalid = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in VarPattern().Matches(Text))
        {
            var content = match.Groups[1].Value;
            if (VarName.Create(content) is Error && seen.Add(content))
            {
                invalid.Add(content);
            }
        }

        return invalid.AsReadOnly();
    }

    public string? Resolve(IReadOnlyDictionary<VarName, object?> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        if (string.IsNullOrEmpty(Text))
        {
            return Text;
        }

        return VarPattern().Replace(Text, match =>
        {
            var name = VarName.Create(match.Groups[1].Value);
            return name is VarName varName && variables.TryGetValue(varName, out var value)
                ? Format(value)
                : match.Value;
        });
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    public override string? ToString() => Text;
}
