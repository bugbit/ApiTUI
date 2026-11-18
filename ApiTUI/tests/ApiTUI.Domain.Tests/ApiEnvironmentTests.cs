using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class ApiEnvironmentTests
{
    [Fact]
    public void Create_ValidEnvironment_PreservesIdTrimsNameAndDefaultsVariables()
    {
        var id = EnvironmentId();

        var result = ApiEnvironment.Create(id, "  Local  ");

        Assert.True(result.IsSuccess);
        Assert.Same(id, result.Value.Id);
        Assert.Equal("Local", result.Value.Name.Value);
        Assert.Empty(result.Value.Variables);
    }

    [Fact]
    public void Create_Variables_PreservesOrderAndInstances()
    {
        var first = Variable("baseUrl", "https://localhost");
        var second = Variable("token", "secret");

        var result = ApiEnvironment.Create(EnvironmentId(), "Local", [first, second]);

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value.Variables,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    [Fact]
    public void Create_NullId_ReturnsIdNull()
    {
        var result = ApiEnvironment.Create(null, "Local");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.IdNull, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.ApiEnvironmentIdRequired, result.Error.Code);
        Assert.Empty(result.Error.Args);
    }

    [Fact]
    public void Create_InvalidName_PropagatesItemNameError()
    {
        var expected = ItemName.Create("CON");
        Assert.False(expected.IsSuccess);

        var result = ApiEnvironment.Create(EnvironmentId(), "CON");

        Assert.False(result.IsSuccess);
        Assert.Equal(expected.Error.Code, result.Error.Code);
        Assert.Equal(expected.Error.Args, result.Error.Args);
    }

    [Fact]
    public void Create_DuplicateVariable_ReturnsDuplicateError()
    {
        var result = ApiEnvironment.Create(EnvironmentId(), "Local",
            [Variable("token", "first"), Variable("token", "second")]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListDuplicated, result.Error.Code);
        Assert.Equal("token", Assert.Single(result.Error.Args));
    }

    [Fact]
    public void Create_NullVariable_ReturnsItemNullError()
    {
        var result = ApiEnvironment.Create(EnvironmentId(), "Local", [null]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListItemNull, result.Error.Code);
    }

    [Fact]
    public void Equality_SameIdDifferentNameAndVariables_AreEqual()
    {
        var first = ApiEnvironment.Create(EnvironmentId("environment-1"), "Local",
            [Variable("baseUrl", "https://localhost")]);
        var second = ApiEnvironment.Create(EnvironmentId("environment-1"), "Production",
            [Variable("baseUrl", "https://example.com")]);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.Equal(first.Value, second.Value);
        Assert.True(first.Value == second.Value);
        Assert.False(first.Value != second.Value);
        Assert.Equal(first.Value.GetHashCode(), second.Value.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var first = ApiEnvironment.Create(EnvironmentId("environment-1"), "Local");
        var second = ApiEnvironment.Create(EnvironmentId("environment-2"), "Local");
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.NotEqual(first.Value, second.Value);
        Assert.True(first.Value != second.Value);
    }

    private static ApiEnvironmentId EnvironmentId(string value = "environment-1")
    {
        var result = ApiEnvironmentId.Create(value);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static VarValue Variable(string name, string value)
    {
        var varName = VarName.Create(name);
        Assert.True(varName.IsSuccess);
        var variable = VarValue.Create(varName.Value, new StringWithVars(value));
        Assert.True(variable.IsSuccess);
        return variable.Value;
    }
}
