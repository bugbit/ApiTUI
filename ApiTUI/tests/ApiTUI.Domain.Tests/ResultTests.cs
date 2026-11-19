using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class ResultTests
{
    [Fact]
    public void ImplicitConversion_FromError_HoldsTheError()
    {
        var error = new Error(ErrorsReasons.InvalidInput, DomainErrorCodes.ItemNameReserved, "CON");

        Result<string> result = error;

        Assert.Same(error, result.AssertFailure());
        Assert.False(result is string);
    }

    [Fact]
    public void ImplicitConversion_FromValue_HoldsTheValue()
    {
        Result<string> result = "ok";

        Assert.Equal("ok", result.AssertSuccess());
        Assert.False(result is Error);
    }

    [Fact]
    public void Default_HasNoValue()
    {
        Result<string> result = default;

        Assert.Null(result.Value);
        Assert.True(result is null);
    }

    [Fact]
    public void Switch_CoversBothCases()
    {
        static string Describe(Result<int> result) => result switch
        {
            Error error => error.Code,
            int value => $"value {value}",
        };

        Assert.Equal("value 42", Describe(42));
        Assert.Equal("Code", Describe(new Error(ErrorsReasons.InvalidInput, "Code")));
    }
}
