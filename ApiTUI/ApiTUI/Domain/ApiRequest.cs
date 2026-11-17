using ApiTUI.Common;

namespace ApiTUI.Domain;

public sealed class ApiRequest : IEquatable<ApiRequest>
{
    // El nombre se usa como nombre de fichero (requests/<Petición>.json): se rechazan los caracteres
    // y nombres reservados de Windows, que son el conjunto más restrictivo de los SO soportados.
    private const string InvalidNameChars = "<>:\"/\\|?*";

    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    public ApiRequestId Id { get; }
    public string Name { get; }
    public RequestMethod Method { get; }
    public StringWithVars Url { get; }
    public IReadOnlyList<HeaderValue> Headers { get; }
    public StringWithVars Body { get; }
    public ApiCollectionId? CollectionId { get; }
    public ApiFolderId? FolderId { get; }

    private ApiRequest(ApiRequestId id, string name, RequestMethod method, StringWithVars url,
        IReadOnlyList<HeaderValue> headers, StringWithVars body, ApiCollectionId? collectionId, ApiFolderId? folderId)
    {
        Id = id;
        Name = name;
        Method = method;
        Url = url;
        Headers = headers;
        Body = body;
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
        StringWithVars? body = null) => Create(id, collectionId, null, name, method, url, headers, body);

    public static Result<ApiRequest> CreateInFolder(
        ApiRequestId? id,
        ApiFolderId? folderId,
        string? name,
        RequestMethod method = RequestMethod.Get,
        StringWithVars? url = null,
        IEnumerable<HeaderValue?>? headers = null,
        StringWithVars? body = null) => Create(id, null, folderId, name, method, url, headers, body);

    private static Result<ApiRequest> Create(
        ApiRequestId? id,
        ApiCollectionId? collectionId,
        ApiFolderId? folderId,
        string? name,
        RequestMethod method,
        StringWithVars? url,
        IEnumerable<HeaderValue?>? headers,
        StringWithVars? body)
    {
        if (id is null)
        {
            return Result<ApiRequest>.Failure(ErrorsReasons.IdNull, "El identificador de la petición es obligatorio.");
        }

        if (collectionId is null && folderId is null)
        {
            return Result<ApiRequest>.Failure(ErrorsReasons.IdNull, "El identificador de la colección o carpeta es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<ApiRequest>.Failure(ErrorsReasons.InvalidInput, "El nombre de la petición no puede estar vacío.");
        }

        var trimmedName = name.Trim();
        if (trimmedName.Any(c => char.IsControl(c) || InvalidNameChars.Contains(c)))
        {
            return Result<ApiRequest>.Failure(ErrorsReasons.InvalidInput,
                $"El nombre de la petición no puede contener caracteres de control ni {InvalidNameChars}.");
        }

        if (trimmedName.EndsWith('.'))
        {
            return Result<ApiRequest>.Failure(ErrorsReasons.InvalidInput, "El nombre de la petición no puede terminar en punto.");
        }

        var baseName = trimmedName.Split('.')[0].TrimEnd();
        if (ReservedNames.Contains(baseName, StringComparer.OrdinalIgnoreCase))
        {
            return Result<ApiRequest>.Failure(ErrorsReasons.InvalidInput,
                $"El nombre de la petición no puede ser un nombre reservado del sistema ({baseName}).");
        }

        if (!Enum.IsDefined(method))
        {
            return Result<ApiRequest>.Failure(ErrorsReasons.InvalidInput, "El método HTTP de la petición no es válido.");
        }

        var validatedHeaders = new List<HeaderValue>();
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                if (header is null)
                {
                    return Result<ApiRequest>.Failure(ErrorsReasons.InvalidInput, "Las cabeceras de la petición no pueden contener elementos nulos.");
                }

                validatedHeaders.Add(header);
            }
        }

        return Result<ApiRequest>.Success(new ApiRequest(id, trimmedName, method,
            url ?? new StringWithVars(null), Array.AsReadOnly(validatedHeaders.ToArray()),
            body ?? new StringWithVars(null), collectionId, folderId));
    }

    // Entidad: la identidad la da el Id, no el resto de propiedades.
    public bool Equals(ApiRequest? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as ApiRequest);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ApiRequest? left, ApiRequest? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ApiRequest? left, ApiRequest? right) => !(left == right);
}
