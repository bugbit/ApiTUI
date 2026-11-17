namespace ApiTUI.Application.SendApiRequest;

public sealed record HttpResponseData(
    int StatusCode,
    string? ReasonPhrase,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string Body);
