using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// Picker icons (Material Design Icons code points) and default button layers per command. One
/// table, keyed by the command name without prefix; every code point is in the host's mdi-catalog.
/// </summary>
internal static class CommandLooks
{
    /// <summary>
    /// For commands that draw the whole touch button themselves (IDisplayImageCommand): the host
    /// adds no icon and caption underneath.
    /// </summary>
    public static readonly ButtonLayoutDescriptor SelfDrawn = new() { Mode = ButtonLayoutMode.None };

    /// <summary>For commands that draw nothing: the icon with the display name below it.</summary>
    public static readonly ButtonLayoutDescriptor IconAndCaption = new() { Mode = ButtonLayoutMode.IconAndCaption };

    private static readonly Dictionary<string, int> Glyphs = new(StringComparer.Ordinal)
    {
        ["StripMute"] = 0xF0581, ["BusMute"] = 0xF0581,   // volume-off
        ["StripSolo"] = 0xF02CB,                          // headphones
        ["StripA"] = 0xF0B08, ["StripB"] = 0xF0B09,       // alpha-a-box, alpha-b-box
        ["StripMono"] = 0xF00F8, ["BusMono"] = 0xF00F8,   // call-merge
        ["StripPostReverb"] = 0xF095B, ["StripPostDelay"] = 0xF095B,
        ["StripPostFx1"] = 0xF095B, ["StripPostFx2"] = 0xF095B, // sine-wave
        ["BusEQ"] = 0xF0EA2,                              // equalizer
        ["BusSel"] = 0xF04FE,                             // target
        ["StripGain"] = 0xF057E, ["BusGain"] = 0xF057E,   // volume-high
        ["StripComp"] = 0xF066A, ["StripGate"] = 0xF066A, ["StripReverb"] = 0xF066A,
        ["StripDelay"] = 0xF066A, ["StripFx1"] = 0xF066A, ["StripFx2"] = 0xF066A, // tune-vertical
        ["StripPanX"] = 0xF04E1, ["StripPanY"] = 0xF04E1, // swap-horizontal
        ["StripEQGain1"] = 0xF0EA2, ["StripEQGain2"] = 0xF0EA2, ["StripEQGain3"] = 0xF0EA2,
        ["Show"] = 0xF05B2,                               // window-restore
        ["Eject"] = 0xF01EA,                              // eject
        ["Load"] = 0xF0770,                               // folder-open
        ["Reset"] = 0xF099B,                              // restore
        ["Restart"] = 0xF0709,                            // restart
        ["Shutdown"] = 0xF0425,                           // power
        ["Raw"] = 0xF0169,                                // code-braces
        ["RawAdjustment"] = 0xF062E,                      // tune
        ["Level"] = 0xF0128                               // chart-bar
    };

    /// <summary>The glyph for a command name without prefix (a trailing Up/Down/Reset is ignored).</summary>
    public static string? Icon(string name)
    {
        if (Glyphs.TryGetValue(name, out var cp)) return char.ConvertFromUtf32(cp);
        foreach (var suffix in new[] { "Up", "Down", "Reset" })
            if (name.EndsWith(suffix, StringComparison.Ordinal) && Glyphs.TryGetValue(name[..^suffix.Length], out cp))
                return char.ConvertFromUtf32(cp);
        return null;
    }
}
