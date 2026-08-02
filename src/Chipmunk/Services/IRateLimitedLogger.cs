namespace Chipmunk.Services;

public interface IRateLimitedLogger : IDisposable
{
    void Info(string message);
    void Debug(string message);
    void Error(string key, string message, Exception? exception = null);
}
