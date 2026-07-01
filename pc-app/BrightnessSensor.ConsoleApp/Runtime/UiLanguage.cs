using System.Globalization;
using BrightnessSensor.ConsoleApp.Configuration;

namespace BrightnessSensor.ConsoleApp.Runtime;

internal enum UiLanguage
{
    English,
    Russian,
    Spanish
}

internal static class UiLanguageResolver
{
    public static UiLanguage Resolve(string configuredLanguage)
    {
        var normalized = string.IsNullOrWhiteSpace(configuredLanguage)
            ? UiSettings.AutoLanguage
            : configuredLanguage.Trim().ToLower(CultureInfo.InvariantCulture);

        return normalized == UiSettings.AutoLanguage
            ? ResolveCulture(CultureInfo.CurrentUICulture)
            : ResolveCode(normalized) ?? UiLanguage.English;
    }

    internal static UiLanguage ResolveCulture(CultureInfo culture)
    {
        return ResolveCode(culture.TwoLetterISOLanguageName) ?? UiLanguage.English;
    }

    private static UiLanguage? ResolveCode(string code)
    {
        return code switch
        {
            "en" => UiLanguage.English,
            "ru" => UiLanguage.Russian,
            "es" => UiLanguage.Spanish,
            _ => null
        };
    }
}
