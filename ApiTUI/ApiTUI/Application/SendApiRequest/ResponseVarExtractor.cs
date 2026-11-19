using System.Text.Json;
using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Application.SendApiRequest;

public static class ResponseVarExtractor
{
    private const string JsonPrefix = "response.json.";

    public static Result<string> Evaluate(StringWithVars template, string body, Lazy<JsonDocument?> json,
        IReadOnlyDictionary<VarName, object?> variables)
    {
        var invalid = template.GetInvalidPlaceholders();
        if (invalid.Count > 0)
        {
            return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.VarOutPlaceholderNotSupported, invalid[0]);
        }

        var values = new Dictionary<VarName, object?>();
        var unresolved = new List<string>();
        foreach (var name in template.GetVarNames())
        {
            if (name.Value == "response")
            {
                values.Add(name, body);
                continue;
            }

            if (name.Value is "response.json" or JsonPrefix)
            {
                return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.VarOutPlaceholderNotSupported, name.Value);
            }

            if (name.Value.StartsWith(JsonPrefix, StringComparison.Ordinal))
            {
                var path = name.Value[JsonPrefix.Length..];
                var jsonValue = ReadJsonValue(path, json.Value);
                if (jsonValue is Error error)
                {
                    return error;
                }

                values.Add(name, jsonValue.Value);
            }
            else if (variables.TryGetValue(name, out var variableValue))
            {
                values.Add(name, variableValue);
            }
            else
            {
                unresolved.Add(name.Value);
            }
        }

        if (unresolved.Count > 0)
        {
            return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.UnresolvedVariables,
                string.Join(", ", unresolved));
        }

        return template.Resolve(values) ?? string.Empty;
    }

    private static Result<string> ReadJsonValue(string path, JsonDocument? json)
    {
        if (json is null)
        {
            return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.VarOutResponseNotJson);
        }

        try
        {
            var element = json.RootElement;
            foreach (var property in path.Split('.'))
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out element))
                {
                    return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.VarOutPathNotFound, path);
                }
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString() ?? string.Empty;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetRawText();
                case JsonValueKind.Null:
                    return string.Empty;
                default:
                    return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.VarOutValueNotScalar, path);
            }
        }
        catch (InvalidOperationException)
        {
            // JsonDocument puede detectar secuencias Unicode inválidas al leer una cadena.
            return new Error(ErrorsReasons.InvalidInput, ApplicationErrorCodes.VarOutResponseNotJson);
        }
    }
}
