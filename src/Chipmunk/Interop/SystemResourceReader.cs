namespace Chipmunk.Interop;

internal sealed class SystemResourceReader
{
    private readonly object _cpuLock = new();
    private ulong? _previousIdle;
    private ulong? _previousKernel;
    private ulong? _previousUser;

    public (double? UsedBytes, double? TotalBytes) ReadPhysicalMemory()
    {
        var status = NativeMethods.MemoryStatusEx.Create();
        if (!NativeMethods.GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
        {
            return (null, null);
        }

        return (status.TotalPhysical - status.AvailablePhysical, status.TotalPhysical);
    }

    /// <summary>
    /// Computes total CPU utilization from the delta between consecutive
    /// GetSystemTimes samples. The first call establishes the baseline.
    /// </summary>
    public double? ReadCpuUsage()
    {
        lock (_cpuLock)
        {
            if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
            {
                return null;
            }

            var currentIdle = idle.ToUInt64();
            var currentKernel = kernel.ToUInt64();
            var currentUser = user.ToUInt64();

            if (_previousIdle is null || _previousKernel is null || _previousUser is null)
            {
                _previousIdle = currentIdle;
                _previousKernel = currentKernel;
                _previousUser = currentUser;
                return null;
            }

            if (currentIdle < _previousIdle ||
                currentKernel < _previousKernel ||
                currentUser < _previousUser)
            {
                _previousIdle = currentIdle;
                _previousKernel = currentKernel;
                _previousUser = currentUser;
                return null;
            }

            var idleDelta = currentIdle - _previousIdle.Value;
            var kernelDelta = currentKernel - _previousKernel.Value;
            var userDelta = currentUser - _previousUser.Value;
            var totalDelta = kernelDelta + userDelta;

            _previousIdle = currentIdle;
            _previousKernel = currentKernel;
            _previousUser = currentUser;

            if (totalDelta == 0 || idleDelta > totalDelta)
            {
                return null;
            }

            return Math.Clamp(100d * (totalDelta - idleDelta) / totalDelta, 0, 100);
        }
    }

    public void ResetCpuBaseline()
    {
        lock (_cpuLock)
        {
            _previousIdle = null;
            _previousKernel = null;
            _previousUser = null;
        }
    }
}
