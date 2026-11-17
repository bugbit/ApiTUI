using ApiTUI.Adapters.Identity;

namespace ApiTUI.Domain.Tests;

public sealed class GuidIdGeneratorTests
{
    private readonly GuidIdGenerator _generator = new();

    [Fact]
    public void NextApiRequestId_ReturnsGuidInNFormat()
    {
        var result = _generator.NextApiRequestId();

        Assert.True(result.IsSuccess);
        Assert.True(Guid.TryParseExact(result.Value.Id, "N", out _));
    }

    [Fact]
    public void NextApiCollectionId_ReturnsGuidInNFormat()
    {
        var result = _generator.NextApiCollectionId();

        Assert.True(result.IsSuccess);
        Assert.True(Guid.TryParseExact(result.Value.Id, "N", out _));
    }

    [Fact]
    public void NextApiRequestId_RepeatedCalls_ReturnsDistinctIds()
    {
        var ids = Enumerable.Range(0, 1000)
            .Select(_ => _generator.NextApiRequestId())
            .Select(result => result.IsSuccess ? result.Value.Id : null)
            .ToList();

        Assert.DoesNotContain(null, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void NextApiCollectionId_RepeatedCalls_ReturnsDistinctIds()
    {
        var ids = Enumerable.Range(0, 1000)
            .Select(_ => _generator.NextApiCollectionId())
            .Select(result => result.IsSuccess ? result.Value.Id : null)
            .ToList();

        Assert.DoesNotContain(null, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
