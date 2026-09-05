using Chipmunk.Models;

namespace Chipmunk.Tests;

public sealed class ColorUtilitiesTests
{
    [Fact]
    public void HexInput_AcceptsRgbAndArgbAndAlwaysFormatsAsArgb()
    {
        Assert.True(ArgbColorHex.TryParseUserInput("#1a2b3c", out var rgb));
        Assert.Equal("#FF1A2B3C", ArgbColorHex.Format(rgb));

        Assert.True(ArgbColorHex.TryParseUserInput("#801A2B3C", out var argb));
        Assert.Equal("#801A2B3C", ArgbColorHex.Format(argb));
    }

    [Theory]
    [InlineData("1A2B3C")]
    [InlineData("#12345")]
    [InlineData("#GG112233")]
    [InlineData("")]
    public void HexInput_RejectsMalformedValues(string value)
    {
        Assert.False(ArgbColorHex.TryParseUserInput(value, out _));
    }

    [Theory]
    [InlineData(0, 255, 0, 0)]
    [InlineData(120, 0, 255, 0)]
    [InlineData(240, 0, 0, 255)]
    [InlineData(60, 255, 255, 0)]
    public void HsvConversion_MapsPrimaryAndSecondaryColors(
        double hue,
        byte expectedRed,
        byte expectedGreen,
        byte expectedBlue)
    {
        var result = HsvColorConverter.ToRgb(hue, 1, 1);

        Assert.Equal(new RgbColorValue(expectedRed, expectedGreen, expectedBlue), result);
    }

    [Theory]
    [InlineData(255, 200, 87)]
    [InlineData(128, 26, 218)]
    [InlineData(40, 180, 170)]
    [InlineData(92, 92, 92)]
    public void HsvConversion_RoundTripsRgbWithinOneByte(byte red, byte green, byte blue)
    {
        var hsv = HsvColorConverter.ToHsv(red, green, blue);
        var result = HsvColorConverter.ToRgb(hsv.Hue, hsv.Saturation, hsv.Value);

        Assert.InRange(Math.Abs(result.Red - red), 0, 1);
        Assert.InRange(Math.Abs(result.Green - green), 0, 1);
        Assert.InRange(Math.Abs(result.Blue - blue), 0, 1);
    }
}
