using System.Collections.Concurrent;
using ApiTUI.Application.Ports;
using ApiTUI.Domain;

namespace ApiTUI.Adapters.Persistence;

// Almacén provisional hasta que exista la persistencia JSON.
public sealed class InMemoryApiStore : IApiEnvironmentReader, IApiCollectionReader, IApiFolderReader
{
    private readonly ConcurrentDictionary<ApiEnvironmentId, ApiEnvironment> _environments = new();
    private readonly ConcurrentDictionary<ApiCollectionId, ApiCollection> _collections = new();
    private readonly ConcurrentDictionary<ApiFolderId, ApiFolder> _folders = new();

    public void Add(ApiEnvironment entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _environments[entity.Id] = entity;
    }

    public Task<ApiEnvironment?> FindAsync(ApiEnvironmentId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_environments.GetValueOrDefault(id));
    }

    public void Add(ApiCollection entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _collections[entity.Id] = entity;
    }

    public Task<ApiCollection?> FindAsync(ApiCollectionId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_collections.GetValueOrDefault(id));
    }

    public void Add(ApiFolder entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _folders[entity.Id] = entity;
    }

    public Task<ApiFolder?> FindAsync(ApiFolderId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_folders.GetValueOrDefault(id));
    }
}
