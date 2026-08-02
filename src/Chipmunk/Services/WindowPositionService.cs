using System.Windows;
using System.Windows.Interop;
using Chipmunk.Interop;
using Chipmunk.Models;
using Forms = System.Windows.Forms;

namespace Chipmunk.Services;

public interface IWindowPositionService
{
    IReadOnlyList<MonitorDescriptor> GetMonitors();
    void PositionWindow(Window window, AppSettings settings, bool forceDefault = false);
    void SaveCurrentPosition(Window window, AppSettings settings);
    void ApplyClickThrough(Window window, bool enabled);
    void ApplyToolWindowStyle(Window window);
}

public sealed class WindowPositionService : IWindowPositionService
{
    public IReadOnlyList<MonitorDescriptor> GetMonitors() =>
        Forms.Screen.AllScreens
            .Select(screen => new MonitorDescriptor(
                screen.DeviceName,
                $"{(screen.Primary ? "Primary" : "Display")} · {screen.Bounds.Width}×{screen.Bounds.Height} · {screen.DeviceName}",
                screen.Primary))
            .OrderByDescending(monitor => monitor.IsPrimary)
            .ToArray();

    public void PositionWindow(Window window, AppSettings settings, bool forceDefault = false)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        var screen = SelectScreen(settings.MonitorDeviceName);
        var bounds = ToPixelRect(screen.Bounds);
        var working = ToPixelRect(screen.WorkingArea);
        var dpi = Math.Max(96u, NativeMethods.GetDpiForWindow(handle));
        var scale = dpi / 96d;
        var width = Math.Max(1, window.ActualWidth * scale);
        var height = Math.Max(1, window.ActualHeight * scale);
        var edge = WindowPositionCalculator.InferTaskbarEdge(bounds, working);

        PixelPoint point;
        if (settings.HasCustomPosition && !forceDefault)
        {
            point = WindowPositionCalculator.Clamp(
                new PixelPoint(settings.CustomLeft, settings.CustomTop),
                working,
                width,
                height);
        }
        else
        {
            point = WindowPositionCalculator.CalculateDefault(
                bounds,
                working,
                edge,
                width,
                height,
                settings.TaskbarMargin * scale);
        }

        var insertAfter = settings.AlwaysOnTop ? NativeMethods.HwndTopmost : NativeMethods.HwndNoTopmost;
        _ = NativeMethods.SetWindowPos(
            handle,
            insertAfter,
            (int)Math.Round(point.X),
            (int)Math.Round(point.Y),
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height),
            NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
    }

    public void SaveCurrentPosition(Window window, AppSettings settings)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != nint.Zero && NativeMethods.GetWindowRect(handle, out var rect))
        {
            settings.CustomLeft = rect.Left;
            settings.CustomTop = rect.Top;
            settings.HasCustomPosition = true;
        }
    }

    public void ApplyClickThrough(Window window, bool enabled)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        style |= NativeMethods.WsExToolWindow | NativeMethods.WsExLayered;
        if (enabled)
        {
            style |= NativeMethods.WsExTransparent;
        }
        else
        {
            style &= ~NativeMethods.WsExTransparent;
        }

        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new nint(style));
    }

    public void ApplyToolWindowStyle(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        style |= NativeMethods.WsExToolWindow;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new nint(style));
    }

    private static Forms.Screen SelectScreen(string? deviceName)
    {
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            var selected = Forms.Screen.AllScreens.FirstOrDefault(
                screen => string.Equals(screen.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected;
            }
        }

        return Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
    }

    private static PixelRect ToPixelRect(System.Drawing.Rectangle rect) =>
        new(rect.Left, rect.Top, rect.Right, rect.Bottom);
}
