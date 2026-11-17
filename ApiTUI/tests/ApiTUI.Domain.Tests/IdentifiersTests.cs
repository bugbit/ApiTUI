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

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Id);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void ApiCollectionIdCreate_ValidId_PreservesValue(string value)
    {
        var result = ApiCollectionId.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Id);
    }

    [Theory]
    [InlineData(" abc", "abc")]
    [InlineData("abc ", "abc")]
    [InlineData("\tabc\n", "abc")]
    public void Create_SurroundingWhitespace_TrimsValue(string value, string expected)
    {
        var result = ApiRequestId.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void ApiRequestIdCreate_NullOrWhitespace_ReturnsIdNull(string? value)
    {
        var result = ApiRequestId.Create(value);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.IdNull, result.Error.Reason);
        Assert.Contains(nameof(ApiRequestId), result.Error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void ApiCollectionIdCreate_NullOrWhitespace_ReturnsIdNull(string? value)
    {
        var result = ApiCollectionId.Create(value);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.IdNull, result.Error.Reason);
        Assert.Contains(nameof(ApiCollectionId), result.Error.Message);
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
    }

    private static ApiRequestId CreateRequestId(string value)
    {
        var result = ApiRequestId.Create(value);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ApiCollectionId CreateCollectionId(string value)
    {
        var result = ApiCollectionId.Create(value);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
