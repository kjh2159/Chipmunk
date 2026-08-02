using System.Text.Json;
using Chipmunk.Models;

namespace Chipmunk.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    string SettingsPath { get; }
    event Action<AppSettings>? SettingsChanged;
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task ExportAsync(
        AppSettings settings,
        string destinationPath,
        CancellationToken cancellationToken = default);
    Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default);
}

public sealed class SettingsService : ISettingsService
{
    private readonly IRateLimitedLogger _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsService(IRateLimitedLogger logger, string? settingsDirectory = null)
    {
        _logger = logger;
        var directory = settingsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Chipmunk");
        SettingsPath = Path.Combine(directory, "settings.json");
    }

    public AppSettings Current { get; private set; } = new();
    public string SettingsPath { get; }
    public event Action<AppSettings>? SettingsChanged;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            Current = new AppSettings();
            return Current;
        }

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            Current = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                _jsonOptions,
                cancellationToken).ConfigureAwait(false) ?? new AppSettings();
            Current.Normalize();
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.Error("settings-load", "설정 파일이 손상되었거나 읽을 수 없어 기본값으로 복구합니다.", exception);
            BackupCorruptFile();
            Current = new AppSettings();
        }

        return Current;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Normalize();
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = SettingsPath + ".tmp";

        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         4096,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                settings,
                _jsonOptions,
                cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, SettingsPath, true);
        Current = settings.Clone();
        SettingsChanged?.Invoke(Current);
    }

    public async Task ExportAsync(
        AppSettings settings,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var export = settings.Clone();
        export.Normalize();
        await using var stream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(
            stream,
            export,
            _jsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new AppSettings();
        await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
        return Current;
    }

    private void BackupCorruptFile()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var backup = $"{SettingsPath}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Copy(SettingsPath, backup, true);
            }
        }
        catch (Exception exception)
        {
            _logger.Error("settings-backup", "손상된 설정 파일 백업에 실패했습니다.", exception);
        }
    }
}
