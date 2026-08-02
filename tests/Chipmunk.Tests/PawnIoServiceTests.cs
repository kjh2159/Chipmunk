using Chipmunk.Models;
using Chipmunk.Services;

namespace Chipmunk.Tests;

public sealed class PawnIoServiceTests
{
    [Fact]
    public void BundledOfficialInstaller_MatchesPinnedSha256()
    {
        using var environment = new TestEnvironment();
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var installer = Path.Combine(
            AppContext.BaseDirectory,
            "Dependencies",
            "PawnIO_setup.exe");
        var service = new PawnIoService(logger, installer);

        Assert.True(File.Exists(installer));
        Assert.True(service.VerifyBundledInstaller());
    }

    [Fact]
    public async Task TamperedInstaller_IsRejected()
    {
        using var environment = new TestEnvironment();
        Directory.CreateDirectory(environment.Root);
        var installer = Path.Combine(environment.Root, "PawnIO_setup.exe");
        await File.WriteAllTextAsync(installer, "not the official installer");
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var service = new PawnIoService(logger, installer);

        Assert.False(service.VerifyBundledInstaller());
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void PromptPolicy_RequiresMissingTemperatureAndExplicitlyAllowedPrompt(
        bool installed,
        bool hasTemperature,
        bool suppressed,
        bool expected)
    {
        var settings = new AppSettings
        {
            SuppressPawnIoInstallPrompt = suppressed
        };
        var snapshot = new MonitoringSnapshot(
            DateTimeOffset.Now,
            hasTemperature ? 50 : null,
            10,
            [],
            1,
            2);

        var result = PawnIoPromptPolicy.ShouldOfferInstallation(
            new PawnIoStatus(installed, null),
            snapshot,
            settings);

        Assert.Equal(expected, result);
    }
}
