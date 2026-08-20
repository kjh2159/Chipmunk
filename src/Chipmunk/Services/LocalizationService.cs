using System.Globalization;
using System.Windows;
using Chipmunk.Models;

namespace Chipmunk.Services;

public interface ILocalizationService
{
    AppLanguage CurrentLanguage { get; }
    event Action<AppLanguage>? LanguageChanged;
    void Apply(AppLanguage language);
    string Get(string key);
    string Format(string key, params object?[] arguments);
}

/// <summary>
/// Replaces the application's string ResourceDictionary at runtime. Views use
/// DynamicResource, while view models query this service for generated text.
/// This keeps a language change immediate and avoids recreating the application.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private const string DictionaryMarker = "/Resources/Strings.";
    private bool _isApplied;

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.English;
    public event Action<AppLanguage>? LanguageChanged;

    public void Apply(AppLanguage language)
    {
        if (!Enum.IsDefined(language))
        {
            language = AppLanguage.English;
        }

        if (_isApplied && CurrentLanguage == language)
        {
            return;
        }

        var application = System.Windows.Application.Current;
        if (application is not null && !application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.Invoke(() => Apply(language));
            return;
        }

        var culture = CultureFor(language);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        if (application is not null)
        {
            var dictionaries = application.Resources.MergedDictionaries;
            var existing = dictionaries
                .Where(dictionary =>
                    dictionary.Source?.OriginalString.Contains(
                        DictionaryMarker,
                        StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();
            foreach (var dictionary in existing)
            {
                dictionaries.Remove(dictionary);
            }

            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(
                    $"/Chipmunk;component/Resources/Strings.{ResourceSuffix(language)}.xaml",
                    UriKind.RelativeOrAbsolute)
            });
        }

        CurrentLanguage = language;
        _isApplied = true;
        LanguageChanged?.Invoke(language);
    }

    public string Get(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;

    public string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    private static string ResourceSuffix(AppLanguage language) => language switch
    {
        AppLanguage.Korean => "ko",
        AppLanguage.Japanese => "ja",
        AppLanguage.ChineseSimplified => "zh-CN",
        AppLanguage.Spanish => "es",
        _ => "en"
    };

    private static CultureInfo CultureFor(AppLanguage language) => new(language switch
    {
        AppLanguage.Korean => "ko-KR",
        AppLanguage.Japanese => "ja-JP",
        AppLanguage.ChineseSimplified => "zh-CN",
        AppLanguage.Spanish => "es-ES",
        _ => "en-US"
    });
}
