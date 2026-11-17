using System;
using ApiTUI.Common;

namespace ApiTUI.Domain;

public abstract record Identifier
{
    public string Id { get; }

    protected Identifier(string id) { Id = id; }

    protected static Result<T> Create<T>(string? id, Func<string, T> factory) where T : Identifier
    {
        var normalizedId = id?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedId))
            return Result<T>.Failure(ErrorsReasons.IdNull, $"{typeof(T).Name} cannot be null or empty.");

        return factory(normalizedId);
    }

    override sealed public string ToString() => Id;
}

public sealed record ApiRequestId : Identifier
{
    private ApiRequestId(string id) : base(id) { }

    public static Result<ApiRequestId> Create(string? id) => Create(id, x => new ApiRequestId(x));
}

public sealed record ApiCollectionId : Identifier
{
    private ApiCollectionId(string id) : base(id) { }

    public static Result<ApiCollectionId> Create(string? id) => Create(id, x => new ApiCollectionId(x));
}

public sealed record ApiFolderId : Identifier
{
    private ApiFolderId(string id) : base(id) { }

    public static Result<ApiFolderId> Create(string? id) => Create(id, x => new ApiFolderId(x));
}