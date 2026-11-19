using ApiTUI.Adapters.Persistence;
using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class InMemoryApiStoreTests
{
    [Fact]
    public async Task Environment_FindReturnsNullThenTracksReplacement()
    {
        var store = new InMemoryApiStore();
        var id = ApiEnvironmentId.Create("id").AssertSuccess();
        var first = ApiEnvironment.Create(id, "First").AssertSuccess();
        var second = ApiEnvironment.Create(id, "Second").AssertSuccess();

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
        var id = ApiCollectionId.Create("id").AssertSuccess();
        var first = ApiCollection.Create(id, "First").AssertSuccess();
        var second = ApiCollection.Create(id, "Second").AssertSuccess();

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
        var id = ApiFolderId.Create("id").AssertSuccess();
        var collectionId = ApiCollectionId.Create("collection").AssertSuccess();
        var first = ApiFolder.CreateInCollection(id, collectionId, "First").AssertSuccess();
        var second = ApiFolder.CreateInCollection(id, collectionId, "Second").AssertSuccess();

        Assert.Null(await store.FindAsync(id, CancellationToken.None));
        store.Add(first);
        Assert.Same(first, await store.FindAsync(id, CancellationToken.None));
        store.Add(second);
        Assert.Same(second, await store.FindAsync(id, CancellationToken.None));
        Assert.Throws<OperationCanceledException>(() => { _ = store.FindAsync(id, new CancellationToken(true)); });
    }

}
