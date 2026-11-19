using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class ApiEnvironmentTests
{
    [Fact]
    public void WithVariableValues_PreservesIdentityAndLeavesOriginalUnchanged()
    {
        var id = ApiEnvironmentId.Create("id").AssertSuccess();
        var selected = VarName.Create("selected").AssertSuccess();
        var disabled = VarName.Create("disabled").AssertSuccess();
        var variables = new[]
        {
            VarValue.Create(selected, new StringWithVars("old")).AssertSuccess(),
            VarValue.Create(disabled, new StringWithVars("disabled"), enabled: false).AssertSuccess(),
        };
        var original = ApiEnvironment.Create(id, "Scope", variables).AssertSuccess();

        var (updated, missing) = original.WithVariableValues(new Dictionary<VarName, StringWithVars>
        {
            [selected] = new("new"),
            [disabled] = new("ignored"),
        });

        Assert.NotSame(original, updated);
        Assert.Same(original.Id, updated.Id);
        Assert.Same(original.Name, updated.Name);
        Assert.Equal(variables, original.Variables);
        Assert.Equal(new[] { selected, disabled }, updated.Variables.Select(v => v.Name));
        Assert.Equal("new", updated.Variables[0].Value.Text);
        Assert.True(updated.Variables[0].Enabled);
        Assert.Same(variables[1], updated.Variables[1]);
        Assert.Equal(disabled, Assert.Single(missing));
    }

    [Fact]
    public void Create_ValidEnvironment_PreservesIdTrimsNameAndDefaultsVariables()
    {
        var id = EnvironmentId();

        var result = ApiEnvironment.Create(id, "  Local  ");

        var value = result.AssertSuccess();
        Assert.Same(id, value.Id);
        Assert.Equal("Local", value.Name.Value);
        Assert.Empty(value.Variables);
    }

    [Fact]
    public void Create_Variables_PreservesOrderAndInstances()
    {
        var first = Variable("baseUrl", "https://localhost");
        var second = Variable("token", "secret");

        var result = ApiEnvironment.Create(EnvironmentId(), "Local", [first, second]);

        Assert.Collection(result.AssertSuccess().Variables,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    [Fact]
    public void Create_NullId_ReturnsIdNull()
    {
        var result = ApiEnvironment.Create(null, "Local");

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.IdNull, error.Reason);
        Assert.Equal(DomainErrorCodes.ApiEnvironmentIdRequired, error.Code);
        Assert.Empty(error.Args);
    }

    [Fact]
    public void Create_InvalidName_PropagatesItemNameError()
    {
        var expected = ItemName.Create("CON");

        var result = ApiEnvironment.Create(EnvironmentId(), "CON");

        Assert.Equal(expected.AssertFailure().Code, result.AssertFailure().Code);
        Assert.Equal(expected.AssertFailure().Args, result.AssertFailure().Args);
    }

    [Fact]
    public void Create_DuplicateVariable_ReturnsDuplicateError()
    {
        var result = ApiEnvironment.Create(EnvironmentId(), "Local",
            [Variable("token", "first"), Variable("token", "second")]);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListDuplicated, error.Code);
        Assert.Equal("token", Assert.Single(error.Args));
    }

    [Fact]
    public void Create_NullVariable_ReturnsItemNullError()
    {
        var result = ApiEnvironment.Create(EnvironmentId(), "Local", [null]);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.VarValueListItemNull, error.Code);
    }

    [Fact]
    public void Equality_SameIdDifferentNameAndVariables_AreEqual()
    {
        var first = ApiEnvironment.Create(EnvironmentId("environment-1"), "Local",
            [Variable("baseUrl", "https://localhost")]);
        var second = ApiEnvironment.Create(EnvironmentId("environment-1"), "Production",
            [Variable("baseUrl", "https://example.com")]);

        Assert.Equal(first.AssertSuccess(), second.AssertSuccess());
        Assert.True(first.AssertSuccess() == second.AssertSuccess());
        Assert.False(first.AssertSuccess() != second.AssertSuccess());
        Assert.Equal(first.AssertSuccess().GetHashCode(), second.AssertSuccess().GetHashCode());
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var first = ApiEnvironment.Create(EnvironmentId("environment-1"), "Local");
        var second = ApiEnvironment.Create(EnvironmentId("environment-2"), "Local");

        Assert.NotEqual(first.AssertSuccess(), second.AssertSuccess());
        Assert.True(first.AssertSuccess() != second.AssertSuccess());
    }

    private static ApiEnvironmentId EnvironmentId(string value = "environment-1")
    {
        var result = ApiEnvironmentId.Create(value);
        return result.AssertSuccess();
    }

    private static VarValue Variable(string name, string value)
    {
        var varName = VarName.Create(name);
        var variable = VarValue.Create(varName.AssertSuccess(), new StringWithVars(value));
        return variable.AssertSuccess();
    }
}
