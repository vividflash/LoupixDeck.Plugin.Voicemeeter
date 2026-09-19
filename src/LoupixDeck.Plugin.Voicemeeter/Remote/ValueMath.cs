using System.Globalization;

namespace LoupixDeck.Plugin.Voicemeeter.Remote;

/// <summary>Pure number helpers for adjustments (clamping, stepping, formatting, parsing).</summary>
internal static class ValueMath
{
    /// <summary>current + ticks * step, clamped to [min, max] and rounded to 0.01 to stop float drift.</summary>
    public static float Step(float current, int ticks, float step, float min, float max) =>
        Clamp(MathF.Round(current + ticks * step, 2), min, max);

    public static float Clamp(float value, float min, float max) => MathF.Min(max, MathF.Max(min, value));

    /// <summary>"0.0 dB", "-3.5 dB", "+2.0 dB" (invariant culture, sign on positives).</summary>
    public static string FormatDb(float value) => Format(value, 1, "dB", signed: true);

    public static string Format(float value, int decimals, string unit, bool signed)
    {
        var rounded = MathF.Round(value, decimals);
        if (rounded == 0) rounded = 0; // no "-0.0"
        var number = rounded.ToString("F" + decimals, CultureInfo.InvariantCulture);
        if (signed && rounded > 0) number = "+" + number;
        return string.IsNullOrEmpty(unit) ? number : $"{number} {unit}";
    }

    /// <summary>Fraction 0..1 of value within [min, max] (for bars).</summary>
    public static float Fraction(float value, float min, float max) =>
        max <= min ? 0 : Clamp((value - min) / (max - min), 0, 1);

    /// <summary>Parses an optional positive step parameter; falls back to <paramref name="fallback"/>.</summary>
    public static float ParseStep(string? text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 && float.IsFinite(v)
            ? v
            : fallback;
    }
}
