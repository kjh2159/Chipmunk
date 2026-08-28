namespace Chipmunk.Services;

public readonly record struct WidgetResizeResult(
    double Width,
    double Height,
    double FontSize,
    double Scale);

public readonly record struct WidgetMinimumSize(double Width, double Height);

/// <summary>
/// Calculates proportional widget resizing from a drag vector. Projecting the
/// pointer movement onto the widget's diagonal makes horizontal and vertical
/// drag input feel natural while preserving the aspect ratio and font scale.
/// </summary>
public static class WidgetResizeCalculator
{
    /// <summary>
    /// Derives the smallest usable dimensions for the current line layout at the
    /// minimum supported font size. This lets three-line widgets become narrower
    /// while keeping every category on a single unwrapped row.
    /// </summary>
    public static WidgetMinimumSize CalculateMinimumSize(
        double naturalTextWidth,
        double naturalTextHeight,
        double currentFontSize,
        double minimumFontSize,
        double horizontalChrome,
        double verticalChrome,
        double absoluteMinimumWidth,
        double absoluteMinimumHeight)
    {
        if (!double.IsFinite(naturalTextWidth) ||
            !double.IsFinite(naturalTextHeight) ||
            !double.IsFinite(currentFontSize) ||
            currentFontSize <= 0)
        {
            return new WidgetMinimumSize(absoluteMinimumWidth, absoluteMinimumHeight);
        }

        var minimumFontScale = Math.Min(1, minimumFontSize / currentFontSize);
        return new WidgetMinimumSize(
            Math.Max(absoluteMinimumWidth, horizontalChrome + naturalTextWidth * minimumFontScale),
            Math.Max(absoluteMinimumHeight, verticalChrome + naturalTextHeight * minimumFontScale));
    }

    public static WidgetResizeResult Calculate(
        double startWidth,
        double startHeight,
        double startFontSize,
        double horizontalChange,
        double verticalChange,
        double minWidth,
        double maxWidth,
        double minHeight,
        double maxHeight,
        double minFontSize,
        double maxFontSize,
        double horizontalChrome = 0,
        double verticalChrome = 0)
    {
        if (startWidth <= 0 || startHeight <= 0 || startFontSize <= 0)
        {
            return new WidgetResizeResult(startWidth, startHeight, startFontSize, 1);
        }

        var scalableWidth = Math.Max(1, startWidth - horizontalChrome);
        var scalableHeight = Math.Max(1, startHeight - verticalChrome);
        var denominator = scalableWidth * scalableWidth + scalableHeight * scalableHeight;
        var requestedScale = 1 +
            (horizontalChange * scalableWidth + verticalChange * scalableHeight) / denominator;
        var minimumScale = Math.Max(
            Math.Max(
                Math.Max(0, minWidth - horizontalChrome) / scalableWidth,
                Math.Max(0, minHeight - verticalChrome) / scalableHeight),
            minFontSize / startFontSize);
        var maximumScale = Math.Min(
            Math.Min(
                Math.Max(0, maxWidth - horizontalChrome) / scalableWidth,
                Math.Max(0, maxHeight - verticalChrome) / scalableHeight),
            maxFontSize / startFontSize);
        maximumScale = Math.Max(minimumScale, maximumScale);
        var scale = Math.Clamp(requestedScale, minimumScale, maximumScale);

        return new WidgetResizeResult(
            horizontalChrome + scalableWidth * scale,
            verticalChrome + scalableHeight * scale,
            startFontSize * scale,
            scale);
    }
}
