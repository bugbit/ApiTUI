using System.Globalization;

namespace ApiTUI.Domain.Tests;

public sealed class StringWithVarsTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain text", "")]
    [InlineData("{{b}}/{{a}}/{{b}}/{{A}}", "b,a,A")]
    [InlineData("{{ a }}{{a b}}{{ñ}}{{}}{{valid}}", "valid")]
    [InlineData("{{api-key}}/{{user_id}}/{{env.prod}}", "api-key,user_id,env.prod")]
    public void GetVarNames_ReturnsValidDistinctNamesInOrder(string? text, string expected)
    {
        var names = new StringWithVars(text).GetVarNames();

        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), names.Select(name => name.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain text {{valid}}/{{valid.name}}")]
    public void GetInvalidPlaceholders_WithoutInvalidMarkers_ReturnsEmpty(string? text)
    {
        Assert.Empty(new StringWithVars(text).GetInvalidPlaceholders());
    }

    [Fact]
    public void GetInvalidPlaceholders_ReturnsDistinctInvalidContentsInOrder()
    {
        var text = new StringWithVars("{{valid}}{{ response }}{{a$b}}{{ response }}{{}}{{café}}{{also-valid}}{{}}");

        Assert.Equal(new[] { " response ", "a$b", "", "café" }, text.GetInvalidPlaceholders());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Texto sin marcadores")]
    public void Resolve_WithoutMarkers_PreservesText(string? text)
    {
        var value = new StringWithVars(text);

        Assert.Equal(text, value.Resolve(Vars()));
    }

    [Fact]
    public void Resolve_OneVariable_ReplacesMarker()
    {
        var value = new StringWithVars("{{host}}/api");

        Assert.Equal("localhost/api", value.Resolve(Vars(("host", "localhost"))));
    }

    [Fact]
    public void Resolve_SeveralAndRepeatedVariables_ReplacesEveryOccurrence()
    {
        var value = new StringWithVars("{{a}}-{{b}}-{{a}}");

        Assert.Equal("first-second-first", value.Resolve(Vars(("a", "first"), ("b", "second"))));
    }

    [Theory]
    [InlineData("{{missing}}", "{{missing}}")]
    [InlineData("{{host}}/{{missing}}", "localhost/{{missing}}")]
    [InlineData("{{Host}}", "{{Host}}")]
    public void Resolve_MissingOrDifferentCaseVariable_PreservesMarker(string text, string expected)
    {
        Assert.Equal(expected, new StringWithVars(text).Resolve(Vars(("host", "localhost"))));
    }

    [Fact]
    public void Resolve_NullValue_ReplacesWithEmptyString()
    {
        Assert.Equal("prefix/suffix", new StringWithVars("prefix{{a}}/suffix").Resolve(Vars(("a", null))));
    }

    [Theory]
    [InlineData(42, "42")]
    [InlineData(1.5, "1.5")]
    [InlineData(true, "True")]
    [InlineData(false, "False")]
    public void Resolve_NonStringValue_FormatsAsText(object value, string expected)
    {
        Assert.Equal(expected, new StringWithVars("{{value}}").Resolve(Vars(("value", value))));
    }

    [Fact]
    public void Resolve_Guid_FormatsAsText()
    {
        var guid = Guid.Parse("c958178d-5ac6-4dd2-846a-8ae1f5d31028");

        Assert.Equal("c958178d-5ac6-4dd2-846a-8ae1f5d31028",
            new StringWithVars("{{id}}").Resolve(Vars(("id", guid))));
    }

    [Fact]
    public void Resolve_Number_UsesInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var commaCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        commaCulture.NumberFormat.NumberDecimalSeparator = ",";

        try
        {
            CultureInfo.CurrentCulture = commaCulture;
            Assert.Equal("1,5", 1.5.ToString(CultureInfo.CurrentCulture));
            Assert.Equal("1.5", new StringWithVars("{{number}}").Resolve(Vars(("number", 1.5))));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("api-key")]
    [InlineData("user_id")]
    [InlineData("env.prod")]
    [InlineData("123")]
    [InlineData("-_.")]
    public void Resolve_AllowedVarNameCharacters_ReplacesMarker(string name)
    {
        var value = new StringWithVars("{{" + name + "}}");

        Assert.Equal("resolved", value.Resolve(Vars((name, "resolved"))));
    }

    [Theory]
    [InlineData("{{ a }}")]
    [InlineData("{{}}")]
    [InlineData("{a}")]
    [InlineData("{{a b}}")]
    [InlineData("{{café}}")]
    [InlineData("{{$guid}}")]
    public void Resolve_InvalidMarker_PreservesText(string text)
    {
        Assert.Equal(text, new StringWithVars(text).Resolve(Vars(("a", "resolved"))));
    }

    [Fact]
    public void Resolve_ValueContainsMarker_DoesNotResolveRecursively()
    {
        Assert.Equal("{{b}}", new StringWithVars("{{a}}").Resolve(Vars(("a", "{{b}}"), ("b", "x"))));
    }

    [Fact]
    public void Resolve_ValueContainsReplacementSyntax_PreservesLiteralValue()
    {
        Assert.Equal("$1$&", new StringWithVars("{{a}}").Resolve(Vars(("a", "$1$&"))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{{host}}/api")]
    public void ToString_ReturnsOriginalText(string? text)
    {
        Assert.Equal(text, new StringWithVars(text).ToString());
    }

    private static IReadOnlyDictionary<VarName, object?> Vars(params (string name, object? value)[] variables)
    {
        var result = new Dictionary<VarName, object?>();
        foreach (var (name, value) in variables)
        {
            var varName = VarName.Create(name);
            result.Add(varName.AssertSuccess(), value);
        }

        return result;
    }
}
