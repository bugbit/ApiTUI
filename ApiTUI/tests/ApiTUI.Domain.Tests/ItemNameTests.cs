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

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ItemNameEmpty, result.Error.Code);
        Assert.Empty(result.Error.Args);
    }

    [Fact]
    public void Create_TrimsNameAndFormatsAsValue()
    {
        var result = ItemName.Create("  Login v1.2  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Login v1.2", result.Value.Value);
        Assert.Equal("Login v1.2", result.Value.ToString());
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

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ItemNameInvalidChars, result.Error.Code);
        Assert.Equal("<>:\"/\\|?*", Assert.Single(result.Error.Args));
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

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        if (value.EndsWith('.'))
        {
            Assert.Equal(DomainErrorCodes.ItemNameEndsWithDot, result.Error.Code);
            Assert.Empty(result.Error.Args);
        }
        else
        {
            Assert.Equal(DomainErrorCodes.ItemNameReserved, result.Error.Code);
            Assert.Equal(value.Split('.')[0].TrimEnd(), Assert.Single(result.Error.Args));
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

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }
}
