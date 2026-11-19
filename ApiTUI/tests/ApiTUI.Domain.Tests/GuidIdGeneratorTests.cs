using ApiTUI.Adapters.Identity;

namespace ApiTUI.Domain.Tests;

public sealed class GuidIdGeneratorTests
{
    private readonly GuidIdGenerator _generator = new();

    [Fact]
    public void NextApiRequestId_ReturnsGuidInNFormat()
    {
        var result = _generator.NextApiRequestId();

        Assert.True(Guid.TryParseExact(result.AssertSuccess().Id, "N", out _));
    }

    [Fact]
    public void NextApiCollectionId_ReturnsGuidInNFormat()
    {
        var result = _generator.NextApiCollectionId();

        Assert.True(Guid.TryParseExact(result.AssertSuccess().Id, "N", out _));
    }

    [Fact]
    public void NextApiRequestId_RepeatedCalls_ReturnsDistinctIds()
    {
        var ids = Enumerable.Range(0, 1000)
            .Select(_ => _generator.NextApiRequestId())
            .Select(result => result is ApiRequestId id ? id.Id : null)
            .ToList();

        Assert.DoesNotContain(null, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void NextApiCollectionId_RepeatedCalls_ReturnsDistinctIds()
    {
        var ids = Enumerable.Range(0, 1000)
            .Select(_ => _generator.NextApiCollectionId())
            .Select(result => result is ApiCollectionId id ? id.Id : null)
            .ToList();

        Assert.DoesNotContain(null, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
