using Chipmunk.Models;
using Chipmunk.Services;

namespace Chipmunk.Tests;

public sealed class WindowPositionCalculatorTests
{
    [Fact]
    public void BottomTaskbar_PositionsAboveRightEdge()
    {
        var bounds = new PixelRect(0, 0, 1920, 1080);
        var working = new PixelRect(0, 0, 1920, 1040);

        var point = WindowPositionCalculator.CalculateDefault(
            bounds,
            working,
            TaskbarEdge.Bottom,
            300,
            70,
            8);

        Assert.Equal(new PixelPoint(1612, 962), point);
    }

    [Fact]
    public void TopAndLeftTaskbars_PositionAdjacentToTaskbar()
    {
        var top = WindowPositionCalculator.CalculateDefault(
            new PixelRect(0, 0, 1920, 1080),
            new PixelRect(0, 40, 1920, 1080),
            TaskbarEdge.Top,
            300,
            70,
            8);
        var left = WindowPositionCalculator.CalculateDefault(
            new PixelRect(-1280, 0, 0, 1024),
            new PixelRect(-1240, 0, 0, 1024),
            TaskbarEdge.Left,
            300,
            70,
            8);

        Assert.Equal(48, top.Y);
        Assert.Equal(-1232, left.X);
    }

    [Fact]
    public void CustomPosition_IsClampedIntoWorkingArea()
    {
        var result = WindowPositionCalculator.Clamp(
            new PixelPoint(5000, -100),
            new PixelRect(0, 0, 1920, 1040),
            300,
            70);

        Assert.Equal(new PixelPoint(1620, 0), result);
    }

    [Theory]
    [InlineData(0, 0, 1920, 1040, TaskbarEdge.Bottom)]
    [InlineData(0, 40, 1920, 1080, TaskbarEdge.Top)]
    [InlineData(40, 0, 1920, 1080, TaskbarEdge.Left)]
    [InlineData(0, 0, 1880, 1080, TaskbarEdge.Right)]
    public void InfersTaskbarEdge(
        double left,
        double top,
        double right,
        double bottom,
        TaskbarEdge expected)
    {
        var result = WindowPositionCalculator.InferTaskbarEdge(
            new PixelRect(0, 0, 1920, 1080),
            new PixelRect(left, top, right, bottom));

        Assert.Equal(expected, result);
    }
}
