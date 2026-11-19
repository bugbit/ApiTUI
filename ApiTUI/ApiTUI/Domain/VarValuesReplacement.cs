namespace ApiTUI.Domain;

public sealed record VarValuesReplacement(IReadOnlyList<VarValue> Variables, IReadOnlyList<VarName> Missing);
