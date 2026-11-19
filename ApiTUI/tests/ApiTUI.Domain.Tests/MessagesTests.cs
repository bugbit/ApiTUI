using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using ApiTUI.Common;
using ApiTUI.Adapters.Persistence;
using ApiTUI.Application.Ports;
using ApiTUI.Application;
using ApiTUI.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiTUI.Domain.Tests;

[Collection("Language configuration")]
public sealed class MessagesTests
{
    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public void Catalog_ContainsExactlyDomainErrorCodesAndUiKeys(string language)
    {
        var codes = new[] { typeof(DomainErrorCodes), typeof(UiMessageKeys), typeof(ApplicationErrorCodes) }
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue())).ToArray();
        var catalog = Catalog(language);

        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.Equal(codes.Order(), catalog.Keys.Order());
        Assert.All(catalog.Values, template => Assert.False(string.IsNullOrWhiteSpace(template)));
    }

    [Fact]
    public void Catalogs_PlaceholdersMatchDomainArgumentsAndFormatSuccessfully()
    {
        var spanish = Catalog("es");
        var english = Catalog("en");
        string[] withArgument =
        [
            DomainErrorCodes.IdentifierRequired, DomainErrorCodes.ItemNameInvalidChars,
            DomainErrorCodes.ItemNameReserved, DomainErrorCodes.HeaderNameInvalidChars,
            DomainErrorCodes.VarNameInvalidChars, DomainErrorCodes.VarValueListDuplicated,
            ApplicationErrorCodes.UnresolvedVariables, ApplicationErrorCodes.UrlInvalid,
            ApplicationErrorCodes.UrlSchemeNotSupported, ApplicationErrorCodes.HeaderInvalid,
            ApplicationErrorCodes.HeaderValueInvalid, ApplicationErrorCodes.HttpTimeout,
            ApplicationErrorCodes.HttpRequestFailed,
            ApplicationErrorCodes.EnvironmentNotFound, ApplicationErrorCodes.CollectionNotFound,
            ApplicationErrorCodes.FolderNotFound, ApplicationErrorCodes.FolderCycle,
            ApplicationErrorCodes.VarOutPathNotFound, ApplicationErrorCodes.VarOutValueNotScalar,
            ApplicationErrorCodes.VarOutPlaceholderNotSupported, ApplicationErrorCodes.VarOutTargetNotFound,
            UiMessageKeys.Request, UiMessageKeys.ResponseStatus, UiMessageKeys.ResponseDuration,
            UiMessageKeys.RequestFailed,
        ];

        foreach (var (code, template) in spanish)
        {
            var spanishFormat = CompositeFormat.Parse(template);
            var englishFormat = CompositeFormat.Parse(english[code]);
            string[] expectedMarkers = code is UiMessageKeys.VariableValue or UiMessageKeys.VarOutFailed
                ? ["0", "1"]
                : withArgument.Contains(code) ? ["0"] : [];
            Assert.Equal(expectedMarkers, Markers(template));
            Assert.Equal(expectedMarkers, Markers(english[code]));
            Assert.Equal(expectedMarkers.Length, spanishFormat.MinimumArgumentCount);
            Assert.Equal(expectedMarkers.Length, englishFormat.MinimumArgumentCount);
            var args = Enumerable.Repeat<object>("sample", expectedMarkers.Length).ToArray();
            Assert.NotEmpty(new Messages("es").Format(new Error(ErrorsReasons.InvalidInput, code, args)));
            Assert.NotEmpty(new Messages("en").Format(new Error(ErrorsReasons.InvalidInput, code, args)));
        }
    }

    [Theory]
    [InlineData("es", "es")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData(" EN-us ", "en")]
    [InlineData("es-ES", "es")]
    [InlineData("fr", "es")]
    [InlineData("!!", "es")]
    [InlineData(null, "es")]
    [InlineData("", "es")]
    [InlineData("  ", "es")]
    public void Constructor_NormalizesLanguageAndFallsBackToSpanish(string? language, string expected)
    {
        var messages = new Messages(language);

        Assert.Equal(expected, messages.Language);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("es", "")]
    [InlineData("fr", "")]
    [InlineData("!!", "")]
    public void Constructor_UsesSatelliteCultureOrInvariantNeutralCulture(string language, string expectedCulture)
    {
        Assert.Equal(expectedCulture, new Messages(language).Culture.Name);
    }

    [Theory]
    [InlineData("es", "El nombre no puede ser un nombre reservado del sistema (CON).")]
    [InlineData("en", "The name cannot be a reserved system name (CON).")]
    [InlineData("fr", "El nombre no puede ser un nombre reservado del sistema (CON).")]
    public void Format_WithArguments_UsesSelectedLanguage(string language, string expected)
    {
        var error = new Error(ErrorsReasons.InvalidInput, DomainErrorCodes.ItemNameReserved, "CON");

        Assert.Equal(expected, new Messages(language).Format(error));
    }

    [Theory]
    [InlineData("es", "Duración: 42 ms")]
    [InlineData("en", "Duration: 42 ms")]
    [InlineData("fr", "Duración: 42 ms")]
    public void Get_UiKey_UsesSelectedLanguage(string language, string expected)
    {
        Assert.Equal(expected, new Messages(language).Get(UiMessageKeys.ResponseDuration, 42L));
    }

    [Fact]
    public void Get_UnknownKey_ReturnsKey()
    {
        Assert.Equal("Ui.Unknown", new Messages("en").Get("Ui.Unknown"));
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public void Format_UnknownCode_ReturnsCode(string language)
    {
        var error = new Error(ErrorsReasons.InvalidInput, "Unknown.Code", "ignored");

        Assert.Equal("Unknown.Code", new Messages(language).Format(error));
    }

    [Fact]
    public void Format_UsesInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        var commaCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        commaCulture.NumberFormat.NumberDecimalSeparator = ",";
        try
        {
            CultureInfo.CurrentCulture = commaCulture;
            var error = new Error(ErrorsReasons.InvalidInput, DomainErrorCodes.ItemNameReserved, 1.5);

            Assert.Equal("The name cannot be a reserved system name (1.5).", new Messages("en").Format(error));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("es", "es")]
    public void GenericHost_UsesEnvironmentLanguageAndCommandLineTakesPrecedence(string? commandLineLanguage, string expected)
    {
        var originalPrefixed = Environment.GetEnvironmentVariable("DOTNET_Language");
        var originalUnprefixed = Environment.GetEnvironmentVariable("Language");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_Language", "en");
            Environment.SetEnvironmentVariable("Language", null);
            string[] args = commandLineLanguage is null ? [] : ["--Language", commandLineLanguage];
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.ClearProviders();
            builder.Services.AddApiTui(builder.Configuration["Language"]);
            builder.ConfigureContainer(new DefaultServiceProviderFactory(
                new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
            using var host = builder.Build();
            var store = host.Services.GetRequiredService<InMemoryApiStore>();
            Assert.Same(store, host.Services.GetRequiredService<IApiEnvironmentReader>());
            Assert.Same(store, host.Services.GetRequiredService<IApiCollectionReader>());
            Assert.Same(store, host.Services.GetRequiredService<IApiFolderReader>());
            Assert.Same(store, host.Services.GetRequiredService<IApiEnvironmentWriter>());
            Assert.Same(store, host.Services.GetRequiredService<IApiCollectionWriter>());
            Assert.Same(store, host.Services.GetRequiredService<IApiFolderWriter>());

            var messages = host.Services.GetRequiredService<Messages>();
            Assert.Equal(expected, messages.Language);
            Assert.Same(messages, host.Services.GetRequiredService<Messages>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_Language", originalPrefixed);
            Environment.SetEnvironmentVariable("Language", originalUnprefixed);
        }
    }

    [Fact]
    public void Error_PreservesCodeAndArgumentsWithoutSharingMutableArray()
    {
        object[] args = ["CON"];
        var error = new Error(ErrorsReasons.InvalidInput, DomainErrorCodes.ItemNameReserved, args);
        args[0] = "changed";

        Assert.Equal(ErrorsReasons.InvalidInput, error.Reason);
        Assert.Equal(DomainErrorCodes.ItemNameReserved, error.Code);
        Assert.Equal("CON", Assert.Single(error.Args));
    }

    private static string[] Markers(string template) => Regex.Matches(template, @"(?<!\{)\{(\d+)[^{}]*\}(?!\})")
        .Select(match => match.Groups[1].Value).Distinct().Order().ToArray();

    private static Dictionary<string, string> Catalog(string language)
    {
        var resources = new ResourceManager("ApiTUI.Localization.Messages", typeof(Messages).Assembly);
        var culture = language == "es" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(language);
        var resourceSet = resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(resourceSet);
        return resourceSet.Cast<DictionaryEntry>().ToDictionary(entry => Assert.IsType<string>(entry.Key),
            entry => Assert.IsType<string>(entry.Value));
    }
}

[CollectionDefinition("Language configuration", DisableParallelization = true)]
public sealed class LanguageConfigurationCollection;
