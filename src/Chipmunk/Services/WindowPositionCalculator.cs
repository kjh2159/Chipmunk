using Chipmunk.Models;

namespace Chipmunk.Services;

public readonly record struct PixelRect(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;
}

public readonly record struct PixelPoint(double X, double Y);

/// <summary>
/// Pure physical-pixel positioning policy. It is deliberately independent from
/// WPF and Screen so mixed-DPI and multi-monitor edge cases are unit-testable.
/// </summary>
public static class WindowPositionCalculator
{
    public static PixelPoint CalculateDefault(
        PixelRect monitorBounds,
        PixelRect workingArea,
        TaskbarEdge taskbarEdge,
        double widgetWidth,
        double widgetHeight,
        double margin)
    {
        margin = Math.Max(0, margin);
        var x = workingArea.Right - widgetWidth - margin;
        var y = workingArea.Bottom - widgetHeight - margin;

        if (taskbarEdge == TaskbarEdge.Top)
        {
            y = workingArea.Top + margin;
        }
        else if (taskbarEdge == TaskbarEdge.Left)
        {
            x = workingArea.Left + margin;
        }

        return Clamp(new PixelPoint(x, y), workingArea, widgetWidth, widgetHeight);
    }

    public static PixelPoint Clamp(
        PixelPoint requested,
        PixelRect workingArea,
        double widgetWidth,
        double widgetHeight)
    {
        var maxX = Math.Max(workingArea.Left, workingArea.Right - widgetWidth);
        var maxY = Math.Max(workingArea.Top, workingArea.Bottom - widgetHeight);
        return new PixelPoint(
            Math.Clamp(requested.X, workingArea.Left, maxX),
            Math.Clamp(requested.Y, workingArea.Top, maxY));
    }

    public static TaskbarEdge InferTaskbarEdge(PixelRect bounds, PixelRect workingArea)
    {
        const double tolerance = 2;
        if (workingArea.Bottom < bounds.Bottom - tolerance)
        {
            return TaskbarEdge.Bottom;
        }

        if (workingArea.Top > bounds.Top + tolerance)
        {
            return TaskbarEdge.Top;
        }

        if (workingArea.Left > bounds.Left + tolerance)
        {
            return TaskbarEdge.Left;
        }

        if (workingArea.Right < bounds.Right - tolerance)
        {
            return TaskbarEdge.Right;
        }

        return TaskbarEdge.Bottom;
    }
}
