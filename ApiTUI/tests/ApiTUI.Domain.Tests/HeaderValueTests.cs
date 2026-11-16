using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class HeaderValueTests
{
    [Fact]
    public void Create_NameAndValue_PreservesPropertiesAndEnablesByDefault()
    {
        var name = Name("Content-Type");
        var value = new StringWithVars("application/json");

        var result = HeaderValue.Create(name, value);

        Assert.True(result.IsSuccess);
        Assert.Same(name, result.Value.Name);
        Assert.Same(value, result.Value.Value);
        Assert.True(result.Value.Enabled);
    }

    [Fact]
    public void Create_Disabled_PreservesEnabled()
    {
        var result = HeaderValue.Create(Name("Accept"), new StringWithVars("application/json"), enabled: false);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Enabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{{token}}")]
    public void Create_ValueText_PreservesText(string? text)
    {
        var result = HeaderValue.Create(Name("Authorization"), new StringWithVars(text));

        Assert.True(result.IsSuccess);
        Assert.Equal(text, result.Value.Value.Text);
    }

    [Fact]
    public void Create_NullValue_NormalizesToNonNullWrapper()
    {
        var result = HeaderValue.Create(Name("Accept"), null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Value);
        Assert.Null(result.Value.Value.Text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_NullName_ReturnsInvalidInput(bool enabled)
    {
        var result = HeaderValue.Create(null, new StringWithVars("value"), enabled);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
    }

    [Fact]
    public void With_Disabled_OnlyChangesEnabledAndPreservesOriginal()
    {
        var result = HeaderValue.Create(Name("Accept"), new StringWithVars("application/json"));
        Assert.True(result.IsSuccess);
        var original = result.Value;

        var disabled = original with { Enabled = false };

        Assert.False(disabled.Enabled);
        Assert.True(original.Enabled);
        Assert.Same(original.Name, disabled.Name);
        Assert.Same(original.Value, disabled.Value);
    }

    [Fact]
    public void Equality_SameValueAndNameIgnoringCase_HasSameEqualityAndHash()
    {
        var first = HeaderValue.Create(Name("Content-Type"), new StringWithVars("application/json"));
        var second = HeaderValue.Create(Name("content-type"), new StringWithVars("application/json"));
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.Equal(first.Value, second.Value);
        Assert.True(first.Value == second.Value);
        Assert.Equal(first.Value.GetHashCode(), second.Value.GetHashCode());
    }

    [Theory]
    [InlineData("Accept", "application/json", true)]
    [InlineData("Content-Type", "text/plain", true)]
    [InlineData("Content-Type", "Application/Json", true)]
    [InlineData("Content-Type", "application/json", false)]
    public void Equality_DifferentProperty_IsNotEqual(string name, string text, bool enabled)
    {
        var first = HeaderValue.Create(Name("Content-Type"), new StringWithVars("application/json"));
        var second = HeaderValue.Create(Name(name), new StringWithVars(text), enabled);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.NotEqual(first.Value, second.Value);
    }

    private static HeaderName Name(string value)
    {
        var result = HeaderName.Create(value);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
