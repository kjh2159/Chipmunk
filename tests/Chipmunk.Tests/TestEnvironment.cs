namespace Chipmunk.Tests;

internal sealed class TestEnvironment : IDisposable
{
    public TestEnvironment()
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "Chipmunk.Tests",
            Guid.NewGuid().ToString("N"));
        SettingsDirectory = Path.Combine(Root, "Settings");
        LogDirectory = Path.Combine(Root, "Logs");
    }

    public string Root { get; }
    public string SettingsDirectory { get; }
    public string LogDirectory { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
        catch
        {
            // Antivirus/indexer locks are irrelevant to assertion outcomes.
        }
    }
}
