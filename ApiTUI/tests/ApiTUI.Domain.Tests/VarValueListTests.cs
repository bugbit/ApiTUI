using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class VarValueListTests
{
    [Fact]
    public void Create_Null_ReturnsEmptyReadOnlyList()
    {
        var result = VarValueList.Create(null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        AssertReadOnly(result.Value);
    }

    [Fact]
    public void Create_Empty_ReturnsEmptyList()
    {
        var result = VarValueList.Create([]);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void Create_ValidVariables_PreservesOrderAndInstances()
    {
        var first = Variable("baseUrl", "https://localhost");
        var second = Variable("token", "secret");

        var result = VarValueList.Create([first, second]);

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    [Fact]
    public void Create_NullElement_ReturnsInvalidInput()
    {
        var result = VarValueList.Create([Variable("first", "1"), null]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListItemNull, result.Error.Code);
        Assert.Empty(result.Error.Args);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_DuplicateName_ReturnsNameAndInvalidInput(bool duplicateEnabled)
    {
        var first = Variable("token", "first", enabled: false);
        var duplicate = Variable("token", "second", enabled: duplicateEnabled);

        var result = VarValueList.Create([first, duplicate]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListDuplicated, result.Error.Code);
        Assert.Equal("token", Assert.Single(result.Error.Args));
    }

    [Fact]
    public void Create_NamesDifferingByCase_AreAllowed()
    {
        var result = VarValueList.Create([Variable("token", "lower"), Variable("Token", "upper")]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public void Create_CopiesSourceCollection()
    {
        var first = Variable("first", "1");
        var second = Variable("second", "2");
        var source = new List<VarValue?> { first, second };

        var result = VarValueList.Create(source);
        Assert.True(result.IsSuccess);
        source.Clear();
        source.Add(Variable("replacement", "3"));

        Assert.Collection(result.Value,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    [Fact]
    public void Create_ResultCannotBeModifiedThroughListInterface()
    {
        var result = VarValueList.Create([Variable("token", "secret")]);
        Assert.True(result.IsSuccess);
        var list = Assert.IsAssignableFrom<IList<VarValue>>(result.Value);

        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = Variable("other", "value"));
    }

    private static void AssertReadOnly(IReadOnlyList<VarValue> variables)
    {
        var list = Assert.IsAssignableFrom<IList<VarValue>>(variables);
        Assert.True(list.IsReadOnly);
    }

    private static VarValue Variable(string name, string? value, bool enabled = true)
    {
        var varName = VarName.Create(name);
        Assert.True(varName.IsSuccess);
        var variable = VarValue.Create(varName.Value, new StringWithVars(value), enabled);
        Assert.True(variable.IsSuccess);
        return variable.Value;
    }
}
