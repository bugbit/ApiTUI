namespace ApiTUI.Domain.Tests;

public sealed class RequestMethodTests
{
    [Fact]
    public void Values_ContainsExactlySupportedMethods()
    {
        RequestMethod[] expected =
        [
            RequestMethod.Get, RequestMethod.Post, RequestMethod.Put, RequestMethod.Patch,
            RequestMethod.Delete, RequestMethod.Head, RequestMethod.Options, RequestMethod.Query,
        ];

        Assert.Equal(expected, Enum.GetValues<RequestMethod>());
    }

    [Fact]
    public void Default_IsGet()
    {
        Assert.Equal(RequestMethod.Get, default(RequestMethod));
    }
}
