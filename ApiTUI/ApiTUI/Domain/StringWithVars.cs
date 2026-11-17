using System.Globalization;
using System.Text.RegularExpressions;

namespace ApiTUI.Domain;

public sealed partial record StringWithVars(string? Text)
{
    // Captura cualquier contenido entre {{ y }}; VarName.Create decide si es un nombre válido.
    [GeneratedRegex(@"\{\{([^{}]+)\}\}")]
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
            if (name.IsSuccess && seen.Add(name.Value))
            {
                names.Add(name.Value);
            }
        }

        return names.AsReadOnly();
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
            return name.IsSuccess && variables.TryGetValue(name.Value, out var value)
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
