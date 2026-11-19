using ApiTUI.Common;

namespace ApiTUI.Domain;

public static class VarValueList
{
    // Se rechazan nombres repetidos para que cada marcador {{var}} tenga un único valor.
    public static Result<IReadOnlyList<VarValue>> Create(IEnumerable<VarValue?>? variables)
    {
        var validated = new List<VarValue>();
        var names = new HashSet<VarName>();

        if (variables is not null)
        {
            foreach (var variable in variables)
            {
                if (variable is null)
                {
                    return new Error(ErrorsReasons.InvalidInput, DomainErrorCodes.VarValueListItemNull);
                }

                if (!names.Add(variable.Name))
                {
                    return new Error(ErrorsReasons.InvalidInput, DomainErrorCodes.VarValueListDuplicated, variable.Name.Value);
                }

                validated.Add(variable);
            }
        }

        IReadOnlyList<VarValue> readOnly = Array.AsReadOnly(validated.ToArray());
        return readOnly;
    }

    public static VarValuesReplacement ReplaceValues(IReadOnlyList<VarValue> list,
        IReadOnlyDictionary<VarName, StringWithVars> values)
    {
        var variables = new List<VarValue>(list.Count);
        var replaced = new HashSet<VarName>();
        foreach (var variable in list)
        {
            if (variable.Enabled && values.TryGetValue(variable.Name, out var value))
            {
                variables.Add(variable.WithValue(value));
                replaced.Add(variable.Name);
            }
            else
            {
                variables.Add(variable);
            }
        }

        var missing = values.Keys.Where(name => !replaced.Contains(name)).ToArray();
        return new VarValuesReplacement(variables.AsReadOnly(), Array.AsReadOnly(missing));
    }
}
