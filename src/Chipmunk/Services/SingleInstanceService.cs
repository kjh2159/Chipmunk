namespace Chipmunk.Services;

public interface ISingleInstanceService : IDisposable
{
    event Action? ActivationRequested;
    bool TryAcquire();
    void SignalFirstInstance();
    void StartListening(CancellationToken cancellationToken);
}

public sealed class SingleInstanceService : ISingleInstanceService
{
    private const string MutexName = @"Local\Chipmunk-6B875506-5BE0-44D0-A404-9C53CE2159B0";
    private const string EventName = @"Local\Chipmunk-Activate-6B875506-5BE0-44D0-A404-9C53CE2159B0";
    private Mutex? _mutex;
    private EventWaitHandle? _activationEvent;
    private bool _ownsMutex;

    public event Action? ActivationRequested;

    public bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        _ownsMutex = createdNew;
        if (createdNew)
        {
            _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        }

        return createdNew;
    }

    public void SignalFirstInstance()
    {
        try
        {
            using var activationEvent = EventWaitHandle.OpenExisting(EventName);
            activationEvent.Set();
        }
        catch
        {
            // The first instance may still be initializing; duplicate prevention
            // remains valid even when activation cannot be delivered.
        }
    }

    public void StartListening(CancellationToken cancellationToken)
    {
        if (_activationEvent is null)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            var handles = new WaitHandle[] { _activationEvent, cancellationToken.WaitHandle };
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = WaitHandle.WaitAny(handles);
                if (result == 0)
                {
                    ActivationRequested?.Invoke();
                }
                else
                {
                    break;
                }
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        _activationEvent?.Dispose();
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex was already released.
            }
        }

        _mutex?.Dispose();
    }
}
