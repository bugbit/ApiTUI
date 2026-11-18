using ApiTUI.Adapters.Persistence;
using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class InMemoryApiStoreTests
{
    [Fact]
    public async Task Environment_FindReturnsNullThenTracksReplacement()
    {
        var store = new InMemoryApiStore();
        var id = Value(ApiEnvironmentId.Create("id"));
        var first = Value(ApiEnvironment.Create(id, "First"));
        var second = Value(ApiEnvironment.Create(id, "Second"));

        Assert.Null(await store.FindAsync(id, CancellationToken.None));
        store.Add(first);
        Assert.Same(first, await store.FindAsync(id, CancellationToken.None));
        store.Add(second);
        Assert.Same(second, await store.FindAsync(id, CancellationToken.None));
        Assert.Throws<OperationCanceledException>(() => { _ = store.FindAsync(id, new CancellationToken(true)); });
    }

    [Fact]
    public async Task Collection_FindReturnsNullThenTracksReplacement()
    {
        var store = new InMemoryApiStore();
        var id = Value(ApiCollectionId.Create("id"));
        var first = Value(ApiCollection.Create(id, "First"));
        var second = Value(ApiCollection.Create(id, "Second"));

        Assert.Null(await store.FindAsync(id, CancellationToken.None));
        store.Add(first);
        Assert.Same(first, await store.FindAsync(id, CancellationToken.None));
        store.Add(second);
        Assert.Same(second, await store.FindAsync(id, CancellationToken.None));
        Assert.Throws<OperationCanceledException>(() => { _ = store.FindAsync(id, new CancellationToken(true)); });
    }

    [Fact]
    public async Task Folder_FindReturnsNullThenTracksReplacement()
    {
        var store = new InMemoryApiStore();
        var id = Value(ApiFolderId.Create("id"));
        var collectionId = Value(ApiCollectionId.Create("collection"));
        var first = Value(ApiFolder.CreateInCollection(id, collectionId, "First"));
        var second = Value(ApiFolder.CreateInCollection(id, collectionId, "Second"));

        Assert.Null(await store.FindAsync(id, CancellationToken.None));
        store.Add(first);
        Assert.Same(first, await store.FindAsync(id, CancellationToken.None));
        store.Add(second);
        Assert.Same(second, await store.FindAsync(id, CancellationToken.None));
        Assert.Throws<OperationCanceledException>(() => { _ = store.FindAsync(id, new CancellationToken(true)); });
    }

    private static T Value<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
