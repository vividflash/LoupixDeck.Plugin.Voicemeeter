using System.Globalization;

namespace LoupixDeck.Plugin.Voicemeeter.Remote;

/// <summary>VBVMR_GetLevel type (original Library/Voicemeeter/Defines.cs LevelType).</summary>
public enum LevelType
{
    PreFaderInput = 0,
    PostFaderInput = 1,
    PostMuteInput = 2,
    Output = 3
}

/// <summary>
/// Maps strips and buses to VBVMR_GetLevel channel indices and converts levels to dB.
/// Input levels (types 0-2): 2 channels per hardware strip, then 8 per virtual strip.
/// Output levels (type 3): 8 channels per bus. Standard: 12 in / 16 out, Banana 22 / 40,
/// Potato 34 / 64. Levels are addressed by strip or bus; the loudest of its
/// channels is metered.
/// </summary>
internal static class LevelMap
{
    public const float MinDb = -60f;
    public const float MaxDb = 12f;

    public static int InputChannels(Edition e) => EditionInfo.HardwareInputs(e) * 2 + EditionInfo.VirtualInputs(e) * 8;

    public static int OutputChannels(Edition e) => EditionInfo.Buses(e) * 8;

    /// <summary>Strip levels for input types, bus levels for Output.</summary>
    public static ChannelKind KindFor(LevelType type) => type == LevelType.Output ? ChannelKind.Bus : ChannelKind.Strip;

    /// <summary>First API level channel and channel count of <paramref name="channel"/>.</summary>
    public static (int First, int Count) Range(LevelType type, Channel channel, Edition edition)
    {
        if (type == LevelType.Output) return (channel.ApiIndex * 8, 8);
        var hw = EditionInfo.HardwareInputs(edition);
        return channel.ApiIndex < hw
            ? (channel.ApiIndex * 2, 2)
            : (hw * 2 + (channel.ApiIndex - hw) * 8, 8);
    }

    /// <summary>Accepts PreFader/PostFader/PostMute/Output, the original enum names, or 0-3.</summary>
    public static bool TryParseType(string? text, out LevelType type)
    {
        var trimmed = (text ?? string.Empty).AsSpan().Trim();
        type = LevelType.PostFaderInput;
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            if (n is < 0 or > 3) return false;
            type = (LevelType)n;
            return true;
        }

        // strip '-' and ' ', lower-case, without allocating
        Span<char> buffer = trimmed.Length <= 64 ? stackalloc char[64] : new char[trimmed.Length];
        var length = 0;
        foreach (var c in trimmed)
            if (c != '-' && c != ' ') buffer[length++] = char.ToLowerInvariant(c);

        switch ((ReadOnlySpan<char>)buffer[..length])
        {
            case "pre" or "prefader" or "prefaderinput":
                type = LevelType.PreFaderInput;
                return true;
            case "post" or "postfader" or "postfaderinput" or "":
                type = LevelType.PostFaderInput;
                return true;
            case "postmute" or "postmuteinput":
                type = LevelType.PostMuteInput;
                return true;
            case "out" or "output" or "bus":
                type = LevelType.Output;
                return true;
            default:
                return false;
        }
    }

    public static string ShortName(LevelType type) => type switch
    {
        LevelType.PreFaderInput => "Pre-fader",
        LevelType.PostFaderInput => "Post-fader",
        LevelType.PostMuteInput => "Post-mute",
        _ => "Output"
    };

    /// <summary>Linear amplitude (1 = 0 dBFS) to dB; negative infinity for silence.</summary>
    public static float ToDb(float amplitude) =>
        amplitude <= 0 ? float.NegativeInfinity : 20f * MathF.Log10(amplitude);

    /// <summary>Position of <paramref name="db"/> on the meter scale <see cref="MinDb"/>..<see cref="MaxDb"/>, 0..1.</summary>
    public static float Fraction(float db) =>
        float.IsNegativeInfinity(db) ? 0f : Math.Clamp((db - MinDb) / (MaxDb - MinDb), 0f, 1f);

    /// <summary>"-12 dB", or "-inf" below the meter floor.</summary>
    public static string FormatDb(float db)
    {
        if (db < MinDb) return "-inf";
        var rounded = MathF.Round(db) + 0f;
        if (rounded >= MinDb && rounded <= MaxDb) return DbTexts[(int)rounded - (int)MinDb];
        return DbText(rounded);
    }

    private static string DbText(float rounded) => string.Create(CultureInfo.InvariantCulture, $"{rounded:0} dB");

    // one text per whole dB step on the meter scale, built once
    private static readonly string[] DbTexts = Enumerable.Range((int)MinDb, (int)(MaxDb - MinDb) + 1).Select(i => DbText(i)).ToArray();
}
