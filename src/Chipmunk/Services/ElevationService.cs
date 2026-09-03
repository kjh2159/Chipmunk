using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Chipmunk.Models;

namespace Chipmunk.Services;

public enum ElevationRestartOutcome
{
    Started,
    AlreadyElevated,
    Cancelled,
    Failed
}

public sealed record ElevationRestartResult(
    ElevationRestartOutcome Outcome,
    string? ErrorMessage = null);

public interface IElevationService
{
    bool IsProcessElevated { get; }
    ElevationRestartResult RestartAsAdministrator(int currentProcessId);
    Task WaitForParentExitAsync(
        int parentProcessId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Restarts only the current Chipmunk session with UAC elevation. The elevated
/// child waits for the original process to exit before acquiring the named
/// single-instance mutex, avoiding a race between the two processes.
/// </summary>
public sealed class ElevationService : IElevationService
{
    public const string RestartArgument = "--elevated-restart-from";
    private readonly IRateLimitedLogger _logger;

    public ElevationService(IRateLimitedLogger logger)
    {
        _logger = logger;
    }

    public bool IsProcessElevated
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity)
                    .IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception exception)
            {
                _logger.Error("elevation-status", "The current process elevation could not be determined.", exception);
                return false;
            }
        }
    }

    public ElevationRestartResult RestartAsAdministrator(int currentProcessId)
    {
        if (IsProcessElevated)
        {
            return new ElevationRestartResult(ElevationRestartOutcome.AlreadyElevated);
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return new ElevationRestartResult(
                ElevationRestartOutcome.Failed,
                "The Chipmunk executable path could not be resolved.");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"{RestartArgument} {currentProcessId}",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };
            return Process.Start(startInfo) is null
                ? new ElevationRestartResult(
                    ElevationRestartOutcome.Failed,
                    "Windows did not start the elevated process.")
                : new ElevationRestartResult(ElevationRestartOutcome.Started);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return new ElevationRestartResult(ElevationRestartOutcome.Cancelled);
        }
        catch (Exception exception)
        {
            _logger.Error("elevation-restart", "Chipmunk could not be restarted as administrator.", exception);
            return new ElevationRestartResult(
                ElevationRestartOutcome.Failed,
                exception.Message);
        }
    }

    public async Task WaitForParentExitAsync(
        int parentProcessId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (parentProcessId <= 0 || parentProcessId == Environment.ProcessId)
        {
            return;
        }

        try
        {
            using var parent = Process.GetProcessById(parentProcessId);
            using var timeoutCancellation = new CancellationTokenSource(timeout);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);
            await parent.WaitForExitAsync(linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            // The original process already exited before the elevated child opened it.
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.Error(
                "elevation-parent-timeout",
                "The elevated restart timed out while waiting for the original process to exit.");
        }
    }

    public static bool TryGetRestartParentProcessId(
        IReadOnlyList<string> arguments,
        out int processId)
    {
        processId = 0;
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], RestartArgument, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arguments[index + 1], out processId) &&
                processId > 0)
            {
                return true;
            }
        }

        processId = 0;
        return false;
    }
}

public static class ElevationPromptPolicy
{
    public static bool ShouldOfferRestart(
        PawnIoStatus pawnIoStatus,
        MonitoringSnapshot snapshot,
        bool isProcessElevated) =>
        pawnIoStatus.IsInstalled &&
        snapshot.CpuTemperatureCelsius is null &&
        !isProcessElevated;
}
