using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;

namespace LoupixDeck.Plugin.Voicemeeter.Actions;

/// <summary>
/// Continuous parameters per strip or bus. Append one AdjustmentSpec per action; the knob command,
/// its migrations and the menu entry are generated from this list. Values are in API units.
/// </summary>
internal static class Adjustments
{
    public static IReadOnlyList<AdjustmentSpec> All { get; } =
    [
        new("StripGain", "Strip Gain", ChannelKind.Strip, "Gain", Min: -60, Max: 12, Step: 1, ResetValue: 0) { ButtonText = "Gain" },
        new("BusGain", "Bus Gain", ChannelKind.Bus, "Gain", Min: -60, Max: 12, Step: 1, ResetValue: 0) { ButtonText = "Gain" },

        // Takes the global strip number; hardware inputs only is an applicability restriction.
        new("StripComp", "Strip Comp", ChannelKind.HardwareInput, "Comp", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Comp", ColorByMute = false, BarColor = Palette.Active },
        new("StripGate", "Strip Gate", ChannelKind.HardwareInput, "Gate", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Gate", ColorByMute = false, BarColor = Palette.Active },
        new("StripReverb", "Strip Reverb", ChannelKind.HardwareInput, "Reverb", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Reverb", PotatoOnly = true, ColorByMute = false, BarColor = Palette.Active },
        new("StripDelay", "Strip Delay", ChannelKind.HardwareInput, "Delay", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Delay", PotatoOnly = true, ColorByMute = false, BarColor = Palette.Active },
        new("StripFx1", "Strip Fx1", ChannelKind.HardwareInput, "Fx1", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Fx1", PotatoOnly = true, ColorByMute = false, BarColor = Palette.Active },
        new("StripFx2", "Strip Fx2", ChannelKind.HardwareInput, "Fx2", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Fx2", PotatoOnly = true, ColorByMute = false, BarColor = Palette.Active },
        new("StripPanX", "Strip Pan X", ChannelKind.Strip, "Pan_x", Min: -0.5f, Max: 0.5f, Step: 0.1f, ResetValue: 0)
            { Unit = "", Decimals = 2, Signed = true, ButtonText = "Pan X", ColorByMute = false, BarColor = Palette.Active },
        // One command for every strip (Kind = Strip, no applicability restriction); the range depends
        // on the resolved strip: hardware 0..1, virtual -0.5..0.5. Press = reset.
        new("StripPanY", "Strip Pan Y", ChannelKind.Strip, "Pan_y", Min: 0f, Max: 1f, Step: 0.1f, ResetValue: 0)
        {
            Unit = "", Decimals = 2, Signed = true, ButtonText = "Pan Y", ColorByMute = false, BarColor = Palette.Active,
            RangeByVirtual = isVirtual => isVirtual ? (-0.5f, 0.5f) : (0f, 1f)
        },
        // Takes the global strip number; virtual inputs only is an applicability restriction.
        new("StripEQGain1", "Strip Bass", ChannelKind.VirtualInput, "EQGain1", Min: -12, Max: 12, Step: 1, ResetValue: 0)
            { ButtonText = "Bass" },
        new("StripEQGain2", "Strip Mid", ChannelKind.VirtualInput, "EQGain2", Min: -12, Max: 12, Step: 1, ResetValue: 0)
            { ButtonText = "Mid" },
        new("StripEQGain3", "Strip Treble", ChannelKind.VirtualInput, "EQGain3", Min: -12, Max: 12, Step: 1, ResetValue: 0)
            { ButtonText = "Treble", PotatoOnly = true },
    ];
}
