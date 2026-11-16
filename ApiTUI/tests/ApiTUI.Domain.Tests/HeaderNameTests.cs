using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class HeaderNameTests
{
    [Theory]
    [InlineData("Accept")]
    [InlineData("Content-Type")]
    [InlineData("X-Request-ID")]
    [InlineData("x_custom.header")]
    [InlineData("a")]
    [InlineData("123")]
    [InlineData("!#$%&'*+-.^_`|~")]
    public void Create_Token_PreservesOriginalText(string value)
    {
        var result = HeaderName.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
        Assert.Equal(value, result.Value.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Content Type")]
    [InlineData(" Accept")]
    [InlineData("Accept ")]
    [InlineData("Accept:")]
    [InlineData(":authority")]
    [InlineData("a\tb")]
    [InlineData("a\r\nb")]
    [InlineData("a\0b")]
    [InlineData("a\u007Fb")]
    [InlineData("Cabecera-ñ")]
    [InlineData("Größe")]
    [InlineData("{{header}}")]
    public void Create_InvalidName_ReturnsInvalidInput(string? value)
    {
        var result = HeaderName.Create(value);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
    }

    [Fact]
    public void Create_AsciiCharacters_AcceptsOnlyTokenCharacters()
    {
        const string allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&'*+-.^_`|~";
        for (var code = 0; code < 128; code++)
        {
            var character = (char)code;
            var result = HeaderName.Create(character.ToString());

            Assert.Equal(allowed.Contains(character), result.IsSuccess);
        }
    }

    [Fact]
    public void Create_RepeatedInvalidCharacters_ListsEachOnce()
    {
        var result = HeaderName.Create("a@b@c/");

        Assert.False(result.IsSuccess);
        Assert.Contains("'@', '/'", result.Error.Message);
        Assert.Equal(1, result.Error.Message.Count(c => c == '@'));
        Assert.Equal(1, result.Error.Message.Count(c => c == '/'));
    }

    [Theory]
    [InlineData(" ", "' '")]
    [InlineData("\t", "'\\t'")]
    [InlineData("\r", "'\\r'")]
    [InlineData("\n", "'\\n'")]
    [InlineData("\0", "'\\0'")]
    [InlineData("\u001B", "U+001B")]
    [InlineData("\u007F", "U+007F")]
    [InlineData("\u0085", "U+0085")]
    [InlineData("\u2028", "U+2028")]
    public void Create_ControlOrWhitespace_DescribesCharacterWithoutEmbeddingIt(string character, string description)
    {
        var result = HeaderName.Create("a" + character + "b");

        Assert.False(result.IsSuccess);
        Assert.Contains(description, result.Error.Message);
        Assert.DoesNotContain(result.Error.Message, c => char.IsControl(c) || c == '\u2028');
    }

    [Fact]
    public void Equality_DifferentCase_HasSameEqualityAndHash()
    {
        var original = CreateName("Content-Type");
        var lower = CreateName("content-type");
        var upper = CreateName("CONTENT-TYPE");

        Assert.True(original.Equals(lower));
        Assert.True(lower.Equals(upper));
        Assert.True(upper.Equals(original));
        Assert.True(original == lower);
        Assert.True(original.Equals((object)upper));
        Assert.Equal(original.GetHashCode(), lower.GetHashCode());
        Assert.Equal(original.GetHashCode(), upper.GetHashCode());
        Assert.Single(new HashSet<HeaderName> { original, lower, upper });
        var dictionary = new Dictionary<HeaderName, string> { [original] = "value" };
        Assert.Equal("value", dictionary[upper]);
        Assert.Equal("Content-Type", original.ToString());
        Assert.Equal("content-type", lower.Value);
    }

    [Fact]
    public void Equality_DifferentNameOrNull_IsNotEqual()
    {
        var accept = CreateName("Accept");
        var encoding = CreateName("Accept-Encoding");

        Assert.False(accept.Equals(encoding));
        Assert.True(accept != encoding);
        Assert.False(accept.Equals((HeaderName?)null));
        Assert.False(accept.Equals("Accept"));
    }

    private static HeaderName CreateName(string value)
    {
        var result = HeaderName.Create(value);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
