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

        Assert.Equal(value, result.AssertSuccess().Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Create_BlankName_ReturnsInvalidInput(string? value)
    {
        var result = VarName.Create(value);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarNameEmpty, error.Code);
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

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarNameInvalidChars, error.Code);
    }

    [Fact]
    public void Create_SeveralDisallowedCharacters_ListsEachOnceInMessage()
    {
        var result = VarName.Create("a$b$c@");

        var error = result.AssertFailure();
        Assert.Equal(DomainErrorCodes.VarNameInvalidChars, error.Code);
        Assert.Equal("'$', '@'", Assert.Single(error.Args));
    }

    [Fact]
    public void Equality_SameText_IsEqualAndFormatsAsValue()
    {
        var first = VarName.Create("baseUrl");
        var second = VarName.Create("baseUrl");

        Assert.Equal(first.AssertSuccess(), second.AssertSuccess());
        Assert.Equal(first.AssertSuccess().GetHashCode(), second.AssertSuccess().GetHashCode());
        Assert.Equal("baseUrl", first.AssertSuccess().ToString());
    }

    [Fact]
    public void Equality_DifferentCase_IsNotEqual()
    {
        var lower = VarName.Create("baseUrl");
        var upper = VarName.Create("BaseUrl");

        Assert.NotEqual(lower.AssertSuccess(), upper.AssertSuccess());
    }
}
