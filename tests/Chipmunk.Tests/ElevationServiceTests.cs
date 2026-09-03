using Chipmunk.Models;
using Chipmunk.Services;

namespace Chipmunk.Tests;

public sealed class ElevationServiceTests
{
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    public void PromptPolicy_RequiresPawnIoMissingTemperatureAndStandardPrivileges(
        bool pawnIoInstalled,
        bool hasTemperature,
        bool isElevated,
        bool expected)
    {
        var snapshot = new MonitoringSnapshot(
            DateTimeOffset.Now,
            hasTemperature ? 50 : null,
            10,
            [],
            1,
            2);

        var result = ElevationPromptPolicy.ShouldOfferRestart(
            new PawnIoStatus(pawnIoInstalled, null),
            snapshot,
            isElevated);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("--elevated-restart-from", "123", true, 123)]
    [InlineData("--ELEVATED-RESTART-FROM", "456", true, 456)]
    [InlineData("--elevated-restart-from", "invalid", false, 0)]
    [InlineData("--elevated-restart-from", "-1", false, 0)]
    public void RestartParentArgument_IsParsedSafely(
        string argument,
        string value,
        bool expected,
        int expectedProcessId)
    {
        var result = ElevationService.TryGetRestartParentProcessId(
            [argument, value],
            out var processId);

        Assert.Equal(expected, result);
        Assert.Equal(expectedProcessId, processId);
    }
}
