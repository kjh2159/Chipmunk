using Chipmunk.Services;

namespace Chipmunk.Tests;

public sealed class WidgetResizeCalculatorTests
{
    [Fact]
    public void CalculateMinimumSize_UsesLayoutContentWidthAndLineHeight()
    {
        var oneLine = WidgetResizeCalculator.CalculateMinimumSize(600, 18, 13, 9, 50, 38, 180, 54);
        var twoLines = WidgetResizeCalculator.CalculateMinimumSize(350, 36, 13, 9, 50, 38, 180, 54);
        var threeLines = WidgetResizeCalculator.CalculateMinimumSize(220, 54, 13, 9, 50, 38, 180, 54);

        Assert.True(oneLine.Width > twoLines.Width);
        Assert.True(twoLines.Width > threeLines.Width);
        Assert.True(threeLines.Height > twoLines.Height);
        Assert.True(twoLines.Height > oneLine.Height);
    }

    [Fact]
    public void Calculate_GrowsDimensionsAndFontAtTheSameRatio()
    {
        var result = WidgetResizeCalculator.Calculate(
            400,
            80,
            13,
            200,
            40,
            260,
            1600,
            54,
            320,
            9,
            30);

        Assert.Equal(1.5, result.Scale, 6);
        Assert.Equal(600, result.Width, 6);
        Assert.Equal(120, result.Height, 6);
        Assert.Equal(19.5, result.FontSize, 6);
    }

    [Fact]
    public void Calculate_ClampsShrinkToTheMostRestrictiveMinimum()
    {
        var result = WidgetResizeCalculator.Calculate(
            420,
            82,
            13,
            -1000,
            -1000,
            260,
            1600,
            54,
            320,
            9,
            30);

        Assert.Equal(9, result.FontSize, 6);
        Assert.Equal(420d / 13 * 9, result.Width, 6);
        Assert.Equal(82d / 13 * 9, result.Height, 6);
    }

    [Fact]
    public void Calculate_WithFixedChrome_ReachesMinimumFontWithoutClippingContent()
    {
        var result = WidgetResizeCalculator.Calculate(
            400,
            100,
            13,
            -1000,
            -1000,
            50 + 350d / 13 * 9,
            1600,
            38 + 62d / 13 * 9,
            320,
            9,
            30,
            50,
            38);

        Assert.Equal(9, result.FontSize, 6);
        Assert.Equal(50 + 350d / 13 * 9, result.Width, 6);
        Assert.Equal(38 + 62d / 13 * 9, result.Height, 6);
    }
}
