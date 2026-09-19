using LoupixDeck.Plugin.Voicemeeter.Remote;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

public class ValueMathTests
{
    [Theory]
    [InlineData(0f, 1, 1f, 1f)]
    [InlineData(0f, -1, 1f, -1f)]
    [InlineData(11.5f, 1, 1f, 12f)]
    [InlineData(12f, 1, 1f, 12f)]
    [InlineData(-59f, -1, 3f, -60f)]
    [InlineData(-60f, -1, 1f, -60f)]
    [InlineData(-3.3f, 1, 0.1f, -3.2f)]
    public void Step_ClampsToGainRange(float current, int ticks, float step, float expected) =>
        Assert.Equal(expected, ValueMath.Step(current, ticks, step, -60, 12), 3);

    [Fact]
    public void Step_NoFloatDrift()
    {
        var v = 0f;
        for (var i = 0; i < 30; i++) v = ValueMath.Step(v, 1, 0.1f, -60, 12);
        Assert.Equal("+3.0 dB", ValueMath.FormatDb(v));
    }

    [Theory]
    [InlineData(0f, "0.0 dB")]
    [InlineData(-0.04f, "0.0 dB")]
    [InlineData(3f, "+3.0 dB")]
    [InlineData(-12.5f, "-12.5 dB")]
    [InlineData(-60f, "-60.0 dB")]
    [InlineData(12f, "+12.0 dB")]
    public void FormatDb(float value, string expected) => Assert.Equal(expected, ValueMath.FormatDb(value));

    [Fact]
    public void Format_UnsignedNoUnit() => Assert.Equal("5", ValueMath.Format(5f, 0, "", signed: false));

    [Theory]
    [InlineData(-60f, 0f)]
    [InlineData(12f, 1f)]
    [InlineData(-24f, 0.5f)]
    [InlineData(100f, 1f)]
    public void Fraction(float value, float expected) => Assert.Equal(expected, ValueMath.Fraction(value, -60, 12), 3);

    [Theory]
    [InlineData(null, 1f)]
    [InlineData("", 1f)]
    [InlineData("0.5", 0.5f)]
    [InlineData("3", 3f)]
    [InlineData("-1", 1f)]
    [InlineData("0", 1f)]
    [InlineData("abc", 1f)]
    public void ParseStep(string? text, float expected) => Assert.Equal(expected, ValueMath.ParseStep(text, 1f));
}
