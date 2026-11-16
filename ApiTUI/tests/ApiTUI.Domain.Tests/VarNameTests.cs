using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class VarNameTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("baseUrl")]
    [InlineData("api-key")]
    [InlineData("user_id")]
    [InlineData("env.prod")]
    [InlineData("A1-b_2.c")]
    [InlineData("123")]
    [InlineData("-_.")]
    public void Create_AllowedName_PreservesValue(string value)
    {
        var result = VarName.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Create_BlankName_ReturnsInvalidInput(string? value)
    {
        var result = VarName.Create(value);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
    }

    [Theory]
    [InlineData("my var")]
    [InlineData("a/b")]
    [InlineData("a{b")]
    [InlineData("ñandu")]
    [InlineData("café")]
    [InlineData("a$")]
    [InlineData("a@b")]
    [InlineData(" a")]
    [InlineData("a ")]
    [InlineData("a\n")]
    [InlineData("变量")]
    public void Create_DisallowedCharacter_ReturnsInvalidInput(string value)
    {
        var result = VarName.Create(value);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
    }

    [Fact]
    public void Create_SeveralDisallowedCharacters_ListsEachOnceInMessage()
    {
        var result = VarName.Create("a$b$c@");

        Assert.False(result.IsSuccess);
        Assert.Contains("'$', '@'", result.Error.Message);
    }

    [Fact]
    public void Equality_SameText_IsEqualAndFormatsAsValue()
    {
        var first = VarName.Create("baseUrl");
        var second = VarName.Create("baseUrl");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(first.Value.GetHashCode(), second.Value.GetHashCode());
        Assert.Equal("baseUrl", first.Value.ToString());
    }

    [Fact]
    public void Equality_DifferentCase_IsNotEqual()
    {
        var lower = VarName.Create("baseUrl");
        var upper = VarName.Create("BaseUrl");

        Assert.True(lower.IsSuccess);
        Assert.True(upper.IsSuccess);
        Assert.NotEqual(lower.Value, upper.Value);
    }
}
