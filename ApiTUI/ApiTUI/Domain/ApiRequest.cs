using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed class ApiRequest : IEquatable<ApiRequest>
{
    public ApiRequestId Id { get; }
    public ItemName Name { get; }
    public RequestMethod Method { get; }
    public StringWithVars Url { get; }
    public IReadOnlyList<HeaderValue> Headers { get; }
    public StringWithVars Body { get; }
    public IReadOnlyList<VarValue>? VarsOut { get; }
    public ApiCollectionId? CollectionId { get; }
    public ApiFolderId? FolderId { get; }

    private ApiRequest(ApiRequestId id, ItemName name, RequestMethod method, StringWithVars url,
        IReadOnlyList<HeaderValue> headers, StringWithVars body, ApiCollectionId? collectionId, ApiFolderId? folderId,
        IReadOnlyList<VarValue>? varsOut)
    {
        Id = id;
        Name = name;
        Method = method;
        Url = url;
        Headers = headers;
        Body = body;
        VarsOut = varsOut;
        CollectionId = collectionId;
        FolderId = folderId;
    }

    public static Result<ApiRequest> CreateInCollection(
        ApiRequestId? id,
        ApiCollectionId? collectionId,
        string? name,
        RequestMethod method = RequestMethod.Get,
        StringWithVars? url = null,
        IEnumerable<HeaderValue?>? headers = null,
        StringWithVars? body = null,
        IEnumerable<VarValue?>? varsOut = null) => Create(id, collectionId, null, name, method, url, headers, body, varsOut);

    public static Result<ApiRequest> CreateInFolder(
        ApiRequestId? id,
        ApiFolderId? folderId,
        string? name,
        RequestMethod method = RequestMethod.Get,
        StringWithVars? url = null,
        IEnumerable<HeaderValue?>? headers = null,
        StringWithVars? body = null,
        IEnumerable<VarValue?>? varsOut = null) => Create(id, null, folderId, name, method, url, headers, body, varsOut);

    private static Result<ApiRequest> Create(
        ApiRequestId? id,
        ApiCollectionId? collectionId,
        ApiFolderId? folderId,
        string? name,
        RequestMethod method,
        StringWithVars? url,
        IEnumerable<HeaderValue?>? headers,
        StringWithVars? body,
        IEnumerable<VarValue?>? varsOut)
    {
        if (id is null)
        {
            return Result<ApiRequest>.Failure(ErrorsReasons.IdNull, DomainErrorCodes.ApiRequestIdRequired);
        }

        if (collectionId is null && folderId is null)
        {
            return Result<ApiRequest>.Failure(ErrorsReasons.IdNull, DomainErrorCodes.ApiRequestLocationRequired);
        }

        var itemName = ItemName.Create(name);
        if (!itemName.IsSuccess)
        {
            return Result<ApiRequest>.Failure(itemName.Error);
        }

        if (!Enum.IsDefined(method))
        {
            return Result<ApiRequest>.Failure(ErrorsReasons.InvalidInput, DomainErrorCodes.ApiRequestMethodInvalid);
        }

        var validatedHeaders = new List<HeaderValue>();
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                if (header is null)
                {
                    return Result<ApiRequest>.Failure(ErrorsReasons.InvalidInput, DomainErrorCodes.ApiRequestHeaderNull);
                }

                validatedHeaders.Add(header);
            }
        }

        IReadOnlyList<VarValue>? validatedVarsOut = null;
        if (varsOut is not null)
        {
            var variableList = VarValueList.Create(varsOut);
            if (!variableList.IsSuccess)
            {
                return Result<ApiRequest>.Failure(variableList.Error);
            }

            validatedVarsOut = variableList.Value;
        }

        return Result<ApiRequest>.Success(new ApiRequest(id, itemName.Value, method,
            url ?? new StringWithVars(null), Array.AsReadOnly(validatedHeaders.ToArray()),
            body ?? new StringWithVars(null), collectionId, folderId, validatedVarsOut));
    }

    // Entidad: la identidad la da el Id, no el resto de propiedades.
    public bool Equals(ApiRequest? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as ApiRequest);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ApiRequest? left, ApiRequest? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ApiRequest? left, ApiRequest? right) => !(left == right);
}
