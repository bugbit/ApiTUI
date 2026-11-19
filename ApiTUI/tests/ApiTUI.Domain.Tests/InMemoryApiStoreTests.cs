using ApiTUI.Adapters.Persistence;
using ApiTUI.Application;
using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class InMemoryApiStoreTests
{
    [Fact]
    public async Task Collection_UpdateVariables_UsesCurrentEntityAndReportsMissingNames()
    {
        var store = new InMemoryApiStore();
        var id = ApiCollectionId.Create("id").AssertSuccess();
        var kept = VarName.Create("kept").AssertSuccess();
        var disabled = VarName.Create("disabled").AssertSuccess();
        var missing = VarName.Create("missing").AssertSuccess();
        store.Add(ApiCollection.Create(id, "Initial").AssertSuccess());
        store.Add(ApiCollection.Create(id, "Current",
            [VarValue.Create(kept, new StringWithVars("old")).AssertSuccess(),
                VarValue.Create(disabled, new StringWithVars("disabled"), enabled: false).AssertSuccess()]).AssertSuccess());

        var result = await store.UpdateVariablesAsync(id, new Dictionary<VarName, StringWithVars>
        {
            [kept] = new("new"),
            [missing] = new("ignored"),
            [disabled] = new("ignored"),
        }, CancellationToken.None);

        Assert.Equal(new[] { missing, disabled }, result.AssertSuccess());
        var current = Assert.IsType<ApiCollection>(await store.FindAsync(id, CancellationToken.None));
        Assert.Equal("Current", current.Name.Value);
        Assert.Equal(new[] { "new", "disabled" }, current.Variables.Select(v => v.Value.Text));
    }

    [Fact]
    public async Task UpdateVariables_MissingScope_ReturnsScopeSpecificError()
    {
        var store = new InMemoryApiStore();
        var values = new Dictionary<VarName, StringWithVars>();
        var collection = await store.UpdateVariablesAsync(ApiCollectionId.Create("id").AssertSuccess(), values, CancellationToken.None);
        var environment = await store.UpdateVariablesAsync(ApiEnvironmentId.Create("id").AssertSuccess(), values, CancellationToken.None);
        var folder = await store.UpdateVariablesAsync(ApiFolderId.Create("id").AssertSuccess(), values, CancellationToken.None);

        Assert.Equal(ApplicationErrorCodes.CollectionNotFound, collection.AssertFailure().Code);
        Assert.Equal(ApplicationErrorCodes.EnvironmentNotFound, environment.AssertFailure().Code);
        Assert.Equal(ApplicationErrorCodes.FolderNotFound, folder.AssertFailure().Code);
    }

    [Fact]
    public async Task Collection_UpdateVariables_ConcurrentBatchesPreserveAllChanges()
    {
        var store = new InMemoryApiStore();
        var id = ApiCollectionId.Create("id").AssertSuccess();
        var names = Enumerable.Range(0, 100).Select(n => VarName.Create($"var{n}").AssertSuccess()).ToArray();
        store.Add(ApiCollection.Create(id, "Collection",
            names.Select(name => VarValue.Create(name, new StringWithVars("old")).AssertSuccess())).AssertSuccess());

        await Parallel.ForEachAsync(names, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (name, token) =>
        {
            var updated = await store.UpdateVariablesAsync(id,
                new Dictionary<VarName, StringWithVars> { [name] = new(name.Value) }, token);
            Assert.Empty(updated.AssertSuccess());
        });

        var collection = Assert.IsType<ApiCollection>(await store.FindAsync(id, CancellationToken.None));
        Assert.Equal(names.Select(name => name.Value), collection.Variables.Select(v => v.Value.Text));
    }

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
