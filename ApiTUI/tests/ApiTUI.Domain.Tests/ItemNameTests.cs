using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class ItemNameTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Create_EmptyName_ReturnsInvalidInput(string? value)
    {
        var result = ItemName.Create(value);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.ItemNameEmpty, error.Code);
        Assert.Empty(error.Args);
    }

    [Fact]
    public void Create_TrimsNameAndFormatsAsValue()
    {
        var result = ItemName.Create("  Login v1.2  ");

        var value = result.AssertSuccess();
        Assert.Equal("Login v1.2", value.Value);
        Assert.Equal("Login v1.2", value.ToString());
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("a\"b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a|b")]
    [InlineData("a\tb")]
    [InlineData("a\0b")]
    [InlineData("a\nb")]
    public void Create_InvalidFileNameCharacter_ReturnsInvalidInput(string value)
    {
        var result = ItemName.Create(value);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.ItemNameInvalidChars, error.Code);
        Assert.Equal("<>:\"/\\|?*", Assert.Single(error.Args));
    }

    [Theory]
    [InlineData("Login.")]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("NUL.json")]
    [InlineData("Com1")]
    [InlineData("LPT9 .txt")]
    public void Create_ReservedNameOrTrailingDot_ReturnsInvalidInput(string value)
    {
        var result = ItemName.Create(value);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        if (value.EndsWith('.'))
        {
            Assert.Equal(DomainErrorCodes.ItemNameEndsWithDot, error.Code);
            Assert.Empty(error.Args);
        }
        else
        {
            Assert.Equal(DomainErrorCodes.ItemNameReserved, error.Code);
            Assert.Equal(value.Split('.')[0].TrimEnd(), Assert.Single(error.Args));
        }
    }

    [Theory]
    [InlineData("Login v1.2")]
    [InlineData("Obtener usuarios (paginado)")]
    [InlineData("CONsulta")]
    [InlineData("Petición #1 & más")]
    [InlineData("CONSOLE")]
    [InlineData("COM10")]
    public void Create_ValidFileName_Succeeds(string value)
    {
        var result = ItemName.Create(value);

        Assert.Equal(value, result.AssertSuccess().Value);
    }
}
