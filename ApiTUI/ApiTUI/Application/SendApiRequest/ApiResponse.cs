using ApiTUI.Domain;

namespace ApiTUI.Application.SendApiRequest;

public sealed record ApiResponse(
    int StatusCode,
    string? ReasonPhrase,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string Body,
    TimeSpan Duration)
{
    public IReadOnlyList<VarValue> AssignedVars { get; init; } = [];
    public IReadOnlyList<VarOutError> VarOutErrors { get; init; } = [];
}
