using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class VarValueListTests
{
    [Fact]
    public void Create_Null_ReturnsEmptyReadOnlyList()
    {
        var result = VarValueList.Create(null);

        var value = result.AssertSuccess();
        Assert.Empty(value);
        AssertReadOnly(value);
    }

    [Fact]
    public void Create_Empty_ReturnsEmptyList()
    {
        var result = VarValueList.Create([]);

        Assert.Empty(result.AssertSuccess());
    }

    [Fact]
    public void Create_ValidVariables_PreservesOrderAndInstances()
    {
        var first = Variable("baseUrl", "https://localhost");
        var second = Variable("token", "secret");

        var result = VarValueList.Create([first, second]);

        Assert.Collection(result.AssertSuccess(),
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    [Fact]
    public void Create_NullElement_ReturnsInvalidInput()
    {
        var result = VarValueList.Create([Variable("first", "1"), null]);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListItemNull, error.Code);
        Assert.Empty(error.Args);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_DuplicateName_ReturnsNameAndInvalidInput(bool duplicateEnabled)
    {
        var first = Variable("token", "first", enabled: false);
        var duplicate = Variable("token", "second", enabled: duplicateEnabled);

        var result = VarValueList.Create([first, duplicate]);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListDuplicated, error.Code);
        Assert.Equal("token", Assert.Single(error.Args));
    }

    [Fact]
    public void Create_NamesDifferingByCase_AreAllowed()
    {
        var result = VarValueList.Create([Variable("token", "lower"), Variable("Token", "upper")]);

        Assert.Equal(2, result.AssertSuccess().Count);
    }

    [Fact]
    public void Create_CopiesSourceCollection()
    {
        var first = Variable("first", "1");
        var second = Variable("second", "2");
        var source = new List<VarValue?> { first, second };

        var result = VarValueList.Create(source);
        source.Clear();
        source.Add(Variable("replacement", "3"));

        Assert.Collection(result.AssertSuccess(),
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    [Fact]
    public void Create_ResultCannotBeModifiedThroughListInterface()
    {
        var result = VarValueList.Create([Variable("token", "secret")]);
        var list = Assert.IsAssignableFrom<IList<VarValue>>(result.AssertSuccess());

        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = Variable("other", "value"));
    }

    [Fact]
    public void ReplaceValues_ReplacesEnabledVariablesInOrderAndReportsMissingNames()
    {
        var first = Variable("first", "old");
        var disabled = Variable("disabled", "original", enabled: false);
        var last = Variable("last", "old");
        var original = VarValueList.Create([first, disabled, last]).AssertSuccess();
        var missing = VarName.Create("missing").AssertSuccess();
        var updates = new Dictionary<VarName, StringWithVars>
        {
            [last.Name] = new("new last"),
            [first.Name] = new("new first"),
            [disabled.Name] = new("ignored"),
            [missing] = new("ignored"),
        };

        var replacement = VarValueList.ReplaceValues(original, updates);

        Assert.Equal(new[] { first, disabled, last }, original);
        Assert.Equal(new[] { "new first", "original", "new last" }, replacement.Variables.Select(v => v.Value.Text));
        Assert.Equal(new[] { true, false, true }, replacement.Variables.Select(v => v.Enabled));
        Assert.Equal(new[] { disabled.Name, missing }, replacement.Missing);
        AssertReadOnly(replacement.Variables);
    }

    private static void AssertReadOnly(IReadOnlyList<VarValue> variables)
    {
        var list = Assert.IsAssignableFrom<IList<VarValue>>(variables);
        Assert.True(list.IsReadOnly);
    }

    private static VarValue Variable(string name, string? value, bool enabled = true)
    {
        var varName = VarName.Create(name);
        var variable = VarValue.Create(varName.AssertSuccess(), new StringWithVars(value), enabled);
        return variable.AssertSuccess();
    }
}
