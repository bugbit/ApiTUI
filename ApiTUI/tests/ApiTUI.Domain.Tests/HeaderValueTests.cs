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

        var header = result.AssertSuccess();
        Assert.Same(name, header.Name);
        Assert.Same(value, header.Value);
        Assert.True(header.Enabled);
    }

    [Fact]
    public void Create_Disabled_PreservesEnabled()
    {
        var result = HeaderValue.Create(Name("Accept"), new StringWithVars("application/json"), enabled: false);

        Assert.False(result.AssertSuccess().Enabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{{token}}")]
    public void Create_ValueText_PreservesText(string? text)
    {
        var result = HeaderValue.Create(Name("Authorization"), new StringWithVars(text));

        Assert.Equal(text, result.AssertSuccess().Value.Text);
    }

    [Fact]
    public void Create_NullValue_NormalizesToNonNullWrapper()
    {
        var result = HeaderValue.Create(Name("Accept"), null);

        var value = result.AssertSuccess();
        Assert.NotNull(value.Value);
        Assert.Null(value.Value.Text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_NullName_ReturnsInvalidInput(bool enabled)
    {
        var result = HeaderValue.Create(null, new StringWithVars("value"), enabled);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.HeaderValueNameRequired, error.Code);
    }

    [Fact]
    public void With_Disabled_OnlyChangesEnabledAndPreservesOriginal()
    {
        var result = HeaderValue.Create(Name("Accept"), new StringWithVars("application/json"));
        var original = result.AssertSuccess();

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

        Assert.Equal(first.AssertSuccess(), second.AssertSuccess());
        Assert.True(first.AssertSuccess() == second.AssertSuccess());
        Assert.Equal(first.AssertSuccess().GetHashCode(), second.AssertSuccess().GetHashCode());
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

        Assert.NotEqual(first.AssertSuccess(), second.AssertSuccess());
    }

    private static HeaderName Name(string value)
    {
        var result = HeaderName.Create(value);
        return result.AssertSuccess();
    }
}
