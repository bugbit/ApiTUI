using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class VarValueTests
{
    [Fact]
    public void Create_NameAndValue_PreservesPropertiesAndEnablesByDefault()
    {
        var name = Name("baseUrl");
        var value = new StringWithVars("https://localhost");

        var result = VarValue.Create(name, value);

        Assert.True(result.IsSuccess);
        Assert.Same(name, result.Value.Name);
        Assert.Same(value, result.Value.Value);
        Assert.True(result.Value.Enabled);
    }

    [Fact]
    public void Create_Disabled_PreservesEnabled()
    {
        var result = VarValue.Create(Name("baseUrl"), new StringWithVars("https://localhost"), enabled: false);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Enabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{{token}}")]
    public void Create_ValueText_PreservesText(string? text)
    {
        var result = VarValue.Create(Name("authorization"), new StringWithVars(text));

        Assert.True(result.IsSuccess);
        Assert.Equal(text, result.Value.Value.Text);
    }

    [Fact]
    public void Create_NullValue_NormalizesToNonNullWrapper()
    {
        var result = VarValue.Create(Name("baseUrl"), null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Value);
        Assert.Null(result.Value.Value.Text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_NullName_ReturnsInvalidInput(bool enabled)
    {
        var result = VarValue.Create(null, new StringWithVars("value"), enabled);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueNameRequired, result.Error.Code);
    }

    [Fact]
    public void With_Disabled_OnlyChangesEnabledAndPreservesOriginal()
    {
        var result = VarValue.Create(Name("baseUrl"), new StringWithVars("https://localhost"));
        Assert.True(result.IsSuccess);
        var original = result.Value;

        var disabled = original with { Enabled = false };

        Assert.False(disabled.Enabled);
        Assert.True(original.Enabled);
        Assert.Same(original.Name, disabled.Name);
        Assert.Same(original.Value, disabled.Value);
    }

    [Fact]
    public void Equality_SameNameAndValue_HasSameEqualityAndHash()
    {
        var first = VarValue.Create(Name("baseUrl"), new StringWithVars("https://localhost"));
        var second = VarValue.Create(Name("baseUrl"), new StringWithVars("https://localhost"));
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.Equal(first.Value, second.Value);
        Assert.True(first.Value == second.Value);
        Assert.Equal(first.Value.GetHashCode(), second.Value.GetHashCode());
    }

    [Theory]
    [InlineData("host", "https://localhost", true)]
    [InlineData("BaseUrl", "https://localhost", true)]
    [InlineData("baseUrl", "https://example.com", true)]
    [InlineData("baseUrl", "https://localhost", false)]
    public void Equality_DifferentProperty_IsNotEqual(string name, string text, bool enabled)
    {
        var first = VarValue.Create(Name("baseUrl"), new StringWithVars("https://localhost"));
        var second = VarValue.Create(Name(name), new StringWithVars(text), enabled);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.NotEqual(first.Value, second.Value);
    }

    private static VarName Name(string value)
    {
        var result = VarName.Create(value);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
