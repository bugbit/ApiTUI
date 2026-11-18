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
                    return Result<IReadOnlyList<VarValue>>.Failure(
                        ErrorsReasons.InvalidInput, DomainErrorCodes.VarValueListItemNull);
                }

                if (!names.Add(variable.Name))
                {
                    return Result<IReadOnlyList<VarValue>>.Failure(
                        ErrorsReasons.InvalidInput, DomainErrorCodes.VarValueListDuplicated, variable.Name.Value);
                }

                validated.Add(variable);
            }
        }

        IReadOnlyList<VarValue> readOnly = Array.AsReadOnly(validated.ToArray());
        return Result<IReadOnlyList<VarValue>>.Success(readOnly);
    }
}
