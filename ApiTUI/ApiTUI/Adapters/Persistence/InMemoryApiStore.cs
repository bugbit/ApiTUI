using System.Collections.Concurrent;
using ApiTUI.Application;
using ApiTUI.Application.Ports;
using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Adapters.Persistence;

// Almacén provisional hasta que exista la persistencia JSON.
public sealed class InMemoryApiStore : IApiEnvironmentReader, IApiCollectionReader, IApiFolderReader,
    IApiEnvironmentWriter, IApiCollectionWriter, IApiFolderWriter
{
    private readonly ConcurrentDictionary<ApiEnvironmentId, Entry<ApiEnvironment>> _environments = new();
    private readonly ConcurrentDictionary<ApiCollectionId, Entry<ApiCollection>> _collections = new();
    private readonly ConcurrentDictionary<ApiFolderId, Entry<ApiFolder>> _folders = new();

    public void Add(ApiEnvironment entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _environments[entity.Id] = new Entry<ApiEnvironment>(entity);
    }

    public Task<ApiEnvironment?> FindAsync(ApiEnvironmentId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_environments.GetValueOrDefault(id)?.Entity);
    }

    public Task<Result<IReadOnlyList<VarName>>> UpdateVariablesAsync(ApiEnvironmentId id,
        IReadOnlyDictionary<VarName, StringWithVars> values, CancellationToken cancellationToken) =>
        Task.FromResult(UpdateVariables(_environments, id, values, ApplicationErrorCodes.EnvironmentNotFound,
            static (entity, updates) => entity.WithVariableValues(updates), cancellationToken));

    public void Add(ApiCollection entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _collections[entity.Id] = new Entry<ApiCollection>(entity);
    }

    public Task<ApiCollection?> FindAsync(ApiCollectionId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_collections.GetValueOrDefault(id)?.Entity);
    }

    public Task<Result<IReadOnlyList<VarName>>> UpdateVariablesAsync(ApiCollectionId id,
        IReadOnlyDictionary<VarName, StringWithVars> values, CancellationToken cancellationToken) =>
        Task.FromResult(UpdateVariables(_collections, id, values, ApplicationErrorCodes.CollectionNotFound,
            static (entity, updates) => entity.WithVariableValues(updates), cancellationToken));

    public void Add(ApiFolder entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _folders[entity.Id] = new Entry<ApiFolder>(entity);
    }

    public Task<ApiFolder?> FindAsync(ApiFolderId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_folders.GetValueOrDefault(id)?.Entity);
    }

    public Task<Result<IReadOnlyList<VarName>>> UpdateVariablesAsync(ApiFolderId id,
        IReadOnlyDictionary<VarName, StringWithVars> values, CancellationToken cancellationToken) =>
        Task.FromResult(UpdateVariables(_folders, id, values, ApplicationErrorCodes.FolderNotFound,
            static (entity, updates) => entity.WithVariableValues(updates), cancellationToken));

    private static Result<IReadOnlyList<VarName>> UpdateVariables<TId, TEntity>(
        ConcurrentDictionary<TId, Entry<TEntity>> entities, TId id,
        IReadOnlyDictionary<VarName, StringWithVars> values, string missingCode,
        Func<TEntity, IReadOnlyDictionary<VarName, StringWithVars>, (TEntity Entity, IReadOnlyList<VarName> Missing)> replace,
        CancellationToken cancellationToken) where TId : Identifier
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entities.TryGetValue(id, out var current))
            {
                return new Error(ErrorsReasons.InvalidInput, missingCode, id.Id);
            }

            var (updated, missing) = replace(current.Entity, values);
            cancellationToken.ThrowIfCancellationRequested();
            if (entities.TryUpdate(id, new Entry<TEntity>(updated), current))
            {
                return missing;
            }
        }
    }

    // TryUpdate compares entries by reference. The domain entities compare by Id.
    private sealed class Entry<TEntity>(TEntity entity)
    {
        public TEntity Entity { get; } = entity;
    }
}
