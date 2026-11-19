using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class IdentifiersTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void ApiRequestIdCreate_ValidId_PreservesValue(string value)
    {
        var result = ApiRequestId.Create(value);

        Assert.Equal(value, result.AssertSuccess().Id);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void ApiCollectionIdCreate_ValidId_PreservesValue(string value)
    {
        var result = ApiCollectionId.Create(value);

        Assert.Equal(value, result.AssertSuccess().Id);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void ApiEnvironmentIdCreate_ValidId_PreservesValue(string value)
    {
        var result = ApiEnvironmentId.Create(value);

        Assert.Equal(value, result.AssertSuccess().Id);
    }

    [Theory]
    [InlineData(" abc", "abc")]
    [InlineData("abc ", "abc")]
    [InlineData("\tabc\n", "abc")]
    public void Create_SurroundingWhitespace_TrimsValue(string value, string expected)
    {
        var result = ApiRequestId.Create(value);

        Assert.Equal(expected, result.AssertSuccess().Id);
    }

    [Fact]
    public void ApiEnvironmentIdCreate_SurroundingWhitespace_TrimsValue()
    {
        var result = ApiEnvironmentId.Create("  local  ");

        Assert.Equal("local", result.AssertSuccess().Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void ApiRequestIdCreate_NullOrWhitespace_ReturnsIdNull(string? value)
    {
        var result = ApiRequestId.Create(value);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.IdNull, error.Reason);
        Assert.Equal(DomainErrorCodes.IdentifierRequired, error.Code);
        Assert.Equal(nameof(ApiRequestId), Assert.Single(error.Args));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void ApiCollectionIdCreate_NullOrWhitespace_ReturnsIdNull(string? value)
    {
        var result = ApiCollectionId.Create(value);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.IdNull, error.Reason);
        Assert.Equal(DomainErrorCodes.IdentifierRequired, error.Code);
        Assert.Equal(nameof(ApiCollectionId), Assert.Single(error.Args));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void ApiEnvironmentIdCreate_NullOrWhitespace_ReturnsIdNull(string? value)
    {
        var result = ApiEnvironmentId.Create(value);

        var error = result.AssertFailure();
        Assert.Equal(ErrorsReasons.IdNull, error.Reason);
        Assert.Equal(DomainErrorCodes.IdentifierRequired, error.Code);
        Assert.Equal(nameof(ApiEnvironmentId), Assert.Single(error.Args));
    }

    [Fact]
    public void Equals_SameTypeAndValue_AreEqual()
    {
        var first = CreateRequestId("abc");
        var second = CreateRequestId(" abc ");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_SameTypeDifferentValue_AreNotEqual()
    {
        Assert.NotEqual(CreateRequestId("abc"), CreateRequestId("abd"));
    }

    [Fact]
    public void Equals_DifferentTypesSameValue_AreNotEqual()
    {
        Identifier requestId = CreateRequestId("abc");
        Identifier collectionId = CreateCollectionId("abc");

        Assert.NotEqual(requestId, collectionId);
    }

    [Fact]
    public void ToString_ReturnsIdForEveryIdentifierType()
    {
        Assert.Equal("abc", CreateRequestId("abc").ToString());
        Assert.Equal("abc", CreateCollectionId("abc").ToString());
        Assert.Equal("abc", CreateEnvironmentId("abc").ToString());
    }

    private static ApiRequestId CreateRequestId(string value)
    {
        var result = ApiRequestId.Create(value);
        return result.AssertSuccess();
    }

    private static ApiCollectionId CreateCollectionId(string value)
    {
        var result = ApiCollectionId.Create(value);
        return result.AssertSuccess();
    }

    private static ApiEnvironmentId CreateEnvironmentId(string value)
    {
        var result = ApiEnvironmentId.Create(value);
        return result.AssertSuccess();
    }
}
