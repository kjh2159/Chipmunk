using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Win32;
using Chipmunk.Models;

namespace Chipmunk.Services;

public sealed record PawnIoStatus(bool IsInstalled, string? Version);

public sealed record PawnIoInstallResult(
    PawnIoInstallOutcome Outcome,
    int? ExitCode = null,
    string? ErrorMessage = null);

public interface IPawnIoService
{
    string InstallerPath { get; }
    PawnIoStatus GetStatus();
    bool VerifyBundledInstaller();
    Task<PawnIoInstallResult> InstallWithConsentAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Detects PawnIO through LibreHardwareMonitor and starts only a pinned, signed
/// official installer after the application has collected explicit consent.
/// The app never downloads or installs a kernel driver silently at runtime.
/// </summary>
public sealed class PawnIoService : IPawnIoService
{
    public const string Version = "2.2.0";
    public const string OfficialSha256 =
        "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";
    public const string OfficialReleaseUrl =
        "https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0";

    private readonly IRateLimitedLogger _logger;
    private readonly string _expectedSha256;

    public PawnIoService(
        IRateLimitedLogger logger,
        string? installerPath = null,
        string? expectedSha256 = null)
    {
        _logger = logger;
        InstallerPath = installerPath ?? Path.Combine(
            AppContext.BaseDirectory,
            "Dependencies",
            "PawnIO_setup.exe");
        _expectedSha256 = expectedSha256 ?? OfficialSha256;
    }

    public string InstallerPath { get; }

    public PawnIoStatus GetStatus()
    {
        try
        {
            // PawnIO's official installer records its version in this machine-wide
            // uninstall key. Reading the registry directly avoids LibreHardwareMonitor's
            // process-lifetime version cache after an in-app installation.
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO",
                    writable: false);
                var version = key?.GetValue("DisplayVersion") as string;
                if (!string.IsNullOrWhiteSpace(version))
                {
                    return new PawnIoStatus(true, version);
                }
            }

            return new PawnIoStatus(false, null);
        }
        catch (Exception exception)
        {
            _logger.Error("pawnio-status", "PawnIO 설치 상태를 확인하지 못했습니다.", exception);
            return new PawnIoStatus(false, null);
        }
    }

    public bool VerifyBundledInstaller()
    {
        try
        {
            if (!File.Exists(InstallerPath))
            {
                return false;
            }

            using var stream = new FileStream(
                InstallerPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            var actualHash = Convert.ToHexString(SHA256.HashData(stream));
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualHash),
                Convert.FromHexString(_expectedSha256));
        }
        catch (Exception exception)
        {
            _logger.Error(
                "pawnio-verify",
                "PawnIO 설치 파일의 무결성을 확인하지 못했습니다.",
                exception);
            return false;
        }
    }

    public async Task<PawnIoInstallResult> InstallWithConsentAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(InstallerPath))
        {
            return new PawnIoInstallResult(PawnIoInstallOutcome.InstallerMissing);
        }

        if (!VerifyBundledInstaller())
        {
            _logger.Error(
                "pawnio-hash",
                "PawnIO 설치 파일의 SHA-256이 공식 고정값과 일치하지 않습니다.");
            return new PawnIoInstallResult(PawnIoInstallOutcome.VerificationFailed);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = InstallerPath,
                Arguments = "-install -silent",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(InstallerPath)
            };
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new PawnIoInstallResult(
                    PawnIoInstallOutcome.Failed,
                    ErrorMessage: "PawnIO 설치 프로세스를 시작하지 못했습니다.");
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode switch
            {
                0 => new PawnIoInstallResult(PawnIoInstallOutcome.Installed, 0),
                3010 => new PawnIoInstallResult(PawnIoInstallOutcome.RebootRequired, 3010),
                _ => new PawnIoInstallResult(
                    PawnIoInstallOutcome.Failed,
                    process.ExitCode,
                    $"PawnIO 설치기가 종료 코드 {process.ExitCode}을 반환했습니다.")
            };
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return new PawnIoInstallResult(PawnIoInstallOutcome.Cancelled);
        }
        catch (OperationCanceledException)
        {
            return new PawnIoInstallResult(PawnIoInstallOutcome.Cancelled);
        }
        catch (Exception exception)
        {
            _logger.Error("pawnio-install", "PawnIO 설치 실행에 실패했습니다.", exception);
            return new PawnIoInstallResult(
                PawnIoInstallOutcome.Failed,
                ErrorMessage: exception.Message);
        }
    }
}

public static class PawnIoPromptPolicy
{
    public static bool ShouldOfferInstallation(
        PawnIoStatus status,
        MonitoringSnapshot snapshot,
        AppSettings settings) =>
        !settings.SuppressPawnIoInstallPrompt &&
        !status.IsInstalled &&
        snapshot.CpuTemperatureCelsius is null;
}
