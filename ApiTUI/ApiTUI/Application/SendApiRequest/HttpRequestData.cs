using ApiTUI.Domain;

namespace ApiTUI.Application.SendApiRequest;

public sealed record HttpRequestData(
    RequestMethod Method,
    Uri Url,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string? Body);
