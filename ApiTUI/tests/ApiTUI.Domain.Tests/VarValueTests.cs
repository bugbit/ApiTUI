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

        var variable = result.AssertSuccess();
        Assert.Same(name, variable.Name);
        Assert.Same(value, variable.Value);
        Assert.True(variable.Enabled);
    }

    [Fact]
    public void Create_Disabled_PreservesEnabled()
    {
        var result = VarValue.Create(Name("baseUrl"), new StringWithVars("https://localhost"), enabled: false);

        Assert.False(result.AssertSuccess().Enabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{{token}}")]
    public void Create_ValueText_PreservesText(string? text)
    {
        var result = VarValue.Create(Name("authorization"), new StringWithVars(text));

        Assert.Equal(text, result.AssertSuccess().Value.Text);
    }

    [Fact]
    public void Create_NullValue_NormalizesToNonNullWrapper()
    {
        var result = VarValue.Create(Name("baseUrl"), null);

        var value = result.AssertSuccess();
        Assert.NotNull(value.Value);
        Assert.Null(value.Value.Text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_NullName_ReturnsInvalidInput(bool enabled)
    {
        var result = VarValue.Create(null, new StringWithVars("value"), enabled);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueNameRequired, error.Code);
    }

    [Fact]
    public void With_Disabled_OnlyChangesEnabledAndPreservesOriginal()
    {
        var result = VarValue.Create(Name("baseUrl"), new StringWithVars("https://localhost"));
        var original = result.AssertSuccess();

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

        Assert.Equal(first.AssertSuccess(), second.AssertSuccess());
        Assert.True(first.AssertSuccess() == second.AssertSuccess());
        Assert.Equal(first.AssertSuccess().GetHashCode(), second.AssertSuccess().GetHashCode());
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

        Assert.NotEqual(first.AssertSuccess(), second.AssertSuccess());
    }

    private static VarName Name(string value)
    {
        var result = VarName.Create(value);
        return result.AssertSuccess();
    }
}
