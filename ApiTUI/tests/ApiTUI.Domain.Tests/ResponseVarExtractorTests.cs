using System.Text.Json;
using ApiTUI.Application;
using ApiTUI.Application.SendApiRequest;

namespace ApiTUI.Domain.Tests;

public sealed class ResponseVarExtractorTests
{
    [Theory]
    [InlineData("{{response}}", "hola", "hola")]
    [InlineData("{{response.json.prop1}}", """{ "prop1": 2 }""", "2")]
    [InlineData("{{response.json.prop2.prop21}}", """{ "prop2": { "prop21": "world" } }""", "world")]
    [InlineData("X-API-Key {{response.json.token}}", """{ "token": "apikey1" }""", "X-API-Key apikey1")]
    [InlineData("{{response.json.x}}", """{"x":1.20e+4}""", "1.20e+4")]
    [InlineData("{{response.json.x}}", """{"x":true}""", "true")]
    [InlineData("{{response.json.x}}", """{"x":false}""", "false")]
    [InlineData("pre{{response.json.x}}post", """{"x":null}""", "prepost")]
    [InlineData("{{response.json.x}}", """{"x":"\u00f1\n\"{{x}}"}""", "ñ\n\"{{x}}")]
    [InlineData("literal", "invalid JSON", "literal")]
    [InlineData(null, "invalid JSON", "")]
    public void Evaluate_ResolvesTemplate(string? template, string body, string expected)
    {
        var json = new Lazy<JsonDocument?>(() => JsonDocument.Parse(body));
        try
        {
            var result = ResponseVarExtractor.Evaluate(new StringWithVars(template), body, json, Vars());

            Assert.Equal(expected, result.AssertSuccess());
            Assert.Equal(template?.Contains("{{response.json.", StringComparison.Ordinal) == true, json.IsValueCreated);
        }
        finally
        {
            if (json.IsValueCreated)
            {
                json.Value?.Dispose();
            }
        }
    }

    [Theory]
    [InlineData("{{response.json.x}}", null, ApplicationErrorCodes.VarOutResponseNotJson, null)]
    [InlineData("{{response.json.x}}", """{"X":1}""", ApplicationErrorCodes.VarOutPathNotFound, "x")]
    [InlineData("{{response.json.x.0}}", """{"x":[1]}""", ApplicationErrorCodes.VarOutPathNotFound, "x.0")]
    [InlineData("{{response.json.x}}", """{"x":{}}""", ApplicationErrorCodes.VarOutValueNotScalar, "x")]
    [InlineData("{{response.json.x}}", """{"x":[]}""", ApplicationErrorCodes.VarOutValueNotScalar, "x")]
    [InlineData("{{response.json.x}}", """{"x":"\uD800"}""", ApplicationErrorCodes.VarOutResponseNotJson, null)]
    [InlineData("{{token}}", null, ApplicationErrorCodes.UnresolvedVariables, "token")]
    [InlineData("{{z}}-{{a}}-{{z}}", null, ApplicationErrorCodes.UnresolvedVariables, "z, a")]
    [InlineData("{{response.json}}", null, ApplicationErrorCodes.VarOutPlaceholderNotSupported, "response.json")]
    [InlineData("{{response.json.}}", null, ApplicationErrorCodes.VarOutPlaceholderNotSupported, "response.json.")]
    [InlineData("{{response.json.my prop}}", null, ApplicationErrorCodes.VarOutPlaceholderNotSupported, "response.json.my prop")]
    [InlineData("{{response.json.x}} {{ response }} {{another invalid}}", null,
        ApplicationErrorCodes.VarOutPlaceholderNotSupported, " response ")]
    [InlineData("{{}}", null, ApplicationErrorCodes.VarOutPlaceholderNotSupported, "")]
    public void Evaluate_InvalidTemplateOrBody_ReturnsError(string template, string? jsonBody, string code, string? argument)
    {
        using var document = jsonBody is null ? null : JsonDocument.Parse(jsonBody);

        var json = new Lazy<JsonDocument?>(() => document);
        var error = ResponseVarExtractor.Evaluate(new StringWithVars(template), jsonBody ?? "invalid JSON", json,
            Vars(("response.json", "ignored"), ("response.json.", "ignored"))).AssertFailure();

        Assert.Equal(code, error.Code);
        Assert.Equal(argument is null ? [] : new object[] { argument }, error.Args);
        if (code == ApplicationErrorCodes.VarOutPlaceholderNotSupported)
        {
            Assert.False(json.IsValueCreated);
        }
    }

    [Fact]
    public void Evaluate_MixesJsonAndRequestVariables_AndPrefersJsonForReservedNames()
    {
        const string body = """{ "a": { "v": "x" } }""";
        using var document = JsonDocument.Parse(body);

        var result = ResponseVarExtractor.Evaluate(new StringWithVars("{{response.json.a.v}}_{{var10}}"), body,
            new Lazy<JsonDocument?>(() => document), Vars(("var10", "y"), ("response.json.a.v", "ignored")));

        Assert.Equal("x_y", result.AssertSuccess());
    }

    [Fact]
    public void Evaluate_RequestVariableThenJsonValueWithLiteralSuffix_ConcatenatesInOrder()
    {
        const string body = """{ "token": "token1" }""";
        using var document = JsonDocument.Parse(body);

        var result = ResponseVarExtractor.Evaluate(new StringWithVars("{{var1}}_{{response.json.token}}__json"), body,
            new Lazy<JsonDocument?>(() => document), Vars(("var1", "apikey")));

        Assert.Equal("apikey_token1__json", result.AssertSuccess());
    }

    private const string CombinationsBody =
        """{ "token": "token1", "user": { "id": 7, "active": true, "name": "José", "nick": null } }""";

    [Theory]
    [InlineData("{{response.json.token}}{{var1}}", "token1apikey")]
    [InlineData("Bearer {{var1}}.{{response.json.token}}", "Bearer apikey.token1")]
    [InlineData("{{var1}}-{{var1}}-{{response.json.token}}-{{response.json.token}}", "apikey-apikey-token1-token1")]
    [InlineData("{{var2}}/{{response.json.user.id}}/{{num}}", "B/7/42")]
    [InlineData("{{response.json.user.active}}|{{empty}}|{{var1}}", "true||apikey")]
    [InlineData("{{response.json.user.nick}}{{var1}}{{response.json.user.nick}}", "apikey")]
    [InlineData("ñ {{var1}} {{response.json.user.name}} ñ", "ñ apikey José ñ")]
    [InlineData("{{{var1}}}_{{{response.json.token}}}", "{apikey}_{token1}")]
    [InlineData("{{var1}}:{{response}}", "apikey:" + CombinationsBody)]
    [InlineData("{{response}}|{{response.json.token}}|{{var1}}", CombinationsBody + "|token1|apikey")]
    public void Evaluate_CombinationsOfRequestVariablesAndResponse_ResolvesEveryMarker(string template, string expected)
    {
        using var document = JsonDocument.Parse(CombinationsBody);

        var result = ResponseVarExtractor.Evaluate(new StringWithVars(template), CombinationsBody,
            new Lazy<JsonDocument?>(() => document),
            Vars(("var1", "apikey"), ("var2", "B"), ("num", 42), ("empty", null)));

        Assert.Equal(expected, result.AssertSuccess());
    }

    [Theory]
    [InlineData("{{var1}}_{{response.json.token}}", "not JSON", ApplicationErrorCodes.VarOutResponseNotJson, null)]
    [InlineData("{{var1}}_{{response.json.missing}}", CombinationsBody, ApplicationErrorCodes.VarOutPathNotFound, "missing")]
    [InlineData("{{var1}}_{{response.json.user}}", CombinationsBody, ApplicationErrorCodes.VarOutValueNotScalar, "user")]
    [InlineData("{{unknown}}_{{response.json.token}}", CombinationsBody, ApplicationErrorCodes.UnresolvedVariables, "unknown")]
    [InlineData("{{var1}}_{{response.token}}__json", CombinationsBody, ApplicationErrorCodes.UnresolvedVariables, "response.token")]
    [InlineData("{{var1}}_{{ response.json.token }}", CombinationsBody,
        ApplicationErrorCodes.VarOutPlaceholderNotSupported, " response.json.token ")]
    public void Evaluate_CombinationsWithOneInvalidPart_ReturnsError(string template, string body, string code, string? argument)
    {
        var json = new Lazy<JsonDocument?>(() =>
        {
            try
            {
                return JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                return null;
            }
        });
        try
        {
            var error = ResponseVarExtractor.Evaluate(new StringWithVars(template), body, json,
                Vars(("var1", "apikey"))).AssertFailure();

            Assert.Equal(code, error.Code);
            Assert.Equal(argument is null ? [] : new object[] { argument }, error.Args);
        }
        finally
        {
            if (json.IsValueCreated)
            {
                json.Value?.Dispose();
            }
        }
    }

    [Theory]
    [InlineData("{{var10}}", "y", "y")]
    [InlineData("{{var10}}", null, "")]
    [InlineData("{{var10}}", "{{other}}", "{{other}}")]
    [InlineData("{{response}}", "ignored", "plain response")]
    public void Evaluate_WithoutJsonMarkers_ResolvesInOnePass(string template, string? value, string expected)
    {
        var json = new Lazy<JsonDocument?>(() => throw new InvalidOperationException());

        var result = ResponseVarExtractor.Evaluate(new StringWithVars(template), "plain response", json,
            Vars(("var10", value), ("other", "expanded"), ("response", value)));

        Assert.Equal(expected, result.AssertSuccess());
        Assert.False(json.IsValueCreated);
    }

    private static IReadOnlyDictionary<VarName, object?> Vars(params (string Name, object? Value)[] variables) =>
        variables.ToDictionary(variable => VarName.Create(variable.Name).AssertSuccess(), variable => variable.Value);
}
