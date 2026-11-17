using System.Globalization;
using System.Resources;
using ApiTUI.Common;

namespace ApiTUI.Localization;

public sealed class Messages
{
    private static readonly ResourceManager Resources = new("ApiTUI.Localization.Messages", typeof(Messages).Assembly);

    public CultureInfo Culture { get; }
    public string Language { get; }

    public Messages(string? language)
    {
        Culture = CultureInfo.InvariantCulture;
        Language = "es";
        var normalized = language?.Trim().ToLowerInvariant().Split('-', 2)[0];
        if (string.IsNullOrEmpty(normalized) || normalized == "es")
        {
            return;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(normalized);
            if (Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false) is not null)
            {
                Culture = culture;
                Language = normalized;
            }
        }
        catch (CultureNotFoundException)
        {
            // Un nombre de cultura no válido usa el recurso neutro español.
        }
    }

    public string Format(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Get(error.Code, error.Args.ToArray());
    }

    public string Get(string key, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(key);
        var template = Resources.GetString(key, Culture);
        if (template is null)
        {
            return key;
        }

        return string.Format(CultureInfo.InvariantCulture, template, args);
    }

}
