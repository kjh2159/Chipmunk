using System.Globalization;
using Chipmunk.Models;

namespace Chipmunk.Tests;

public sealed class LocalizationTests
{
    [Theory]
    [InlineData("en-US", AppLanguage.English)]
    [InlineData("ko-KR", AppLanguage.Korean)]
    [InlineData("ja-JP", AppLanguage.Japanese)]
    [InlineData("zh-CN", AppLanguage.ChineseSimplified)]
    [InlineData("es-ES", AppLanguage.Spanish)]
    [InlineData("fr-FR", AppLanguage.English)]
    public void SystemLanguageDetection_MapsSupportedCultures(
        string cultureName,
        AppLanguage expected)
    {
        var result = AppLanguageDefaults.Detect(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Clone_PreservesSelectedLanguage()
    {
        var settings = new AppSettings { Language = AppLanguage.Spanish };

        var clone = settings.Clone();

        Assert.Equal(AppLanguage.Spanish, clone.Language);
    }
}
