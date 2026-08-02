using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Chipmunk.Services;

/// <summary>
/// Writes on one background worker and suppresses duplicate error keys for five
/// minutes. Sensor failures therefore cannot grow the log once per polling tick.
/// </summary>
public sealed class RateLimitedFileLogger : IRateLimitedLogger
{
    private static readonly TimeSpan ErrorRepeatWindow = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastErrors = new();
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly string _logPath;
    private readonly Task _writerTask;
    private bool _disposed;

    public RateLimitedFileLogger(string? baseDirectory = null)
    {
        var directory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Chipmunk",
            "Logs");
        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, $"monitor-{DateTime.Now:yyyyMMdd}.log");
        RotateIfNeeded(_logPath);
        _writerTask = Task.Run(WriteLoopAsync);
    }

    public void Info(string message) => Enqueue("INFO", message);

    public void Debug(string message)
    {
#if DEBUG
        Enqueue("DEBUG", message);
#endif
    }

    public void Error(string key, string message, Exception? exception = null)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastErrors.TryGetValue(key, out var previous) && now - previous < ErrorRepeatWindow)
        {
            return;
        }

        _lastErrors[key] = now;
        var detail = exception is null
            ? message
            : $"{message} ({exception.GetType().Name}: {exception.Message})";
#if DEBUG
        if (exception?.StackTrace is not null)
        {
            detail += Environment.NewLine + exception.StackTrace;
        }
#endif
        Enqueue("ERROR", detail);
    }

    private void Enqueue(string level, string message)
    {
        if (_disposed)
        {
            return;
        }

        _queue.Writer.TryWrite($"{DateTimeOffset.Now:O} [{level}] {message}");
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var entry in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                await File.AppendAllTextAsync(_logPath, entry + Environment.NewLine).ConfigureAwait(false);
            }
        }
        catch
        {
            // Logging is diagnostic-only and must never terminate the monitor.
        }
    }

    private static void RotateIfNeeded(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Length > 5 * 1024 * 1024)
            {
                File.Move(path, path + ".1", true);
            }
        }
        catch
        {
            // Rotation failure is non-fatal.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.Writer.TryComplete();
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort flush during process shutdown.
        }
    }
}
