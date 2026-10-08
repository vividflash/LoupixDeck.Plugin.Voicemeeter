using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;

namespace LoupixDeck.Plugin.Voicemeeter.Actions;

/// <summary>
/// Family B: continuous parameters per strip or bus (original SingleBaseAdjustment subclasses).
/// Append one AdjustmentSpec per action; the knob command, its migrations and the menu entry are
/// generated from this list. Values are in API units (the original's scaleFactor is gone:
/// original min/max divided by scaleFactor = API range).
/// </summary>
internal static class Adjustments
{
    public static IReadOnlyList<AdjustmentSpec> All { get; } =
    [
        // Original StripGainAdjustment: base(hasReset: true, ..., -60, 12), press = reset to 0.
        new("StripGain", "Strip Gain", ChannelKind.Strip, "Gain", Min: -60, Max: 12, Step: 1, ResetValue: 0) { ButtonText = "Gain" },
        // Original BusGainAdjustment: same range and reset.
        new("BusGain", "Bus Gain", ChannelKind.Bus, "Gain", Min: -60, Max: 12, Step: 1, ResetValue: 0) { ButtonText = "Gain" },

        // Original HardwareInputCompAdjustment: base(true, true, true) -> 0..10, press = reset.
        // Takes the global strip number; hardware inputs only is an applicability restriction.
        new("StripComp", "Strip Comp", ChannelKind.HardwareInput, "Comp", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Comp", ColorByMute = false, BarColor = Palette.Active },
        // Original HardwareInputGateAdjustment: base(true, true, true) -> 0..10, press = reset.
        new("StripGate", "Strip Gate", ChannelKind.HardwareInput, "Gate", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Gate", ColorByMute = false, BarColor = Palette.Active },
        // Original HardwareInputReverbAdjustment: base(true, true, true) -> 0..10, Potato only, press = reset.
        new("StripReverb", "Strip Reverb", ChannelKind.HardwareInput, "Reverb", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Reverb", PotatoOnly = true, ColorByMute = false, BarColor = Palette.Active },
        // Original HardwareInputDelayAdjustment: base(true, true, true) -> 0..10, Potato only, press = reset.
        new("StripDelay", "Strip Delay", ChannelKind.HardwareInput, "Delay", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Delay", PotatoOnly = true, ColorByMute = false, BarColor = Palette.Active },
        // Original HardwareInputFx1Adjustment (Fx file): base(true, true, true) -> 0..10, Potato only, press = reset.
        new("StripFx1", "Strip Fx1", ChannelKind.HardwareInput, "Fx1", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Fx1", PotatoOnly = true, ColorByMute = false, BarColor = Palette.Active },
        // Original HardwareInputFx2Adjustment (Fx file): base(true, true, true) -> 0..10, Potato only, press = reset.
        new("StripFx2", "Strip Fx2", ChannelKind.HardwareInput, "Fx2", Min: 0, Max: 10, Step: 1, ResetValue: 0)
            { Unit = "", Signed = false, ButtonText = "Fx2", PotatoOnly = true, ColorByMute = false, BarColor = Palette.Active },
        // Original StripPanXAdjustment (StripPan file): base(true, true, true, -5, 5, 10) -> -0.5..0.5 / scale 10, press = reset.
        new("StripPanX", "Strip Pan X", ChannelKind.Strip, "Pan_x", Min: -0.5f, Max: 0.5f, Step: 0.1f, ResetValue: 0)
            { Unit = "", Decimals = 2, Signed = true, ButtonText = "Pan X", ColorByMute = false, BarColor = Palette.Active },
        // Original HardwareInputPanYAdjustment + VirtualInputPanYAdjustment (StripPan file), merged into
        // one command that applies to every strip (Kind = Strip, no applicability restriction) with a
        // range that depends on the resolved strip: hardware 0..1 (orig 0..10 / scale 10), virtual
        // -0.5..0.5 (orig -5..5 / scale 10), press = reset.
        new("StripPanY", "Strip Pan Y", ChannelKind.Strip, "Pan_y", Min: 0f, Max: 1f, Step: 0.1f, ResetValue: 0)
        {
            Unit = "", Decimals = 2, Signed = true, ButtonText = "Pan Y", ColorByMute = false, BarColor = Palette.Active,
            RangeByVirtual = isVirtual => isVirtual ? (-0.5f, 0.5f) : (0f, 1f)
        },
        // Original VirtualInputEqGain1Adjustment: base(true, true, true, -12, 12) -> -12..12 dB, press = reset.
        // Takes the global strip number; virtual inputs only is an applicability restriction.
        new("StripEQGain1", "Strip EQ Gain 1", ChannelKind.VirtualInput, "EQGain1", Min: -12, Max: 12, Step: 1, ResetValue: 0)
            { ButtonText = "Bass" },
        // Original VirtualInputEqGain2Adjustment: base(true, true, true, -12, 12) -> -12..12 dB, press = reset.
        new("StripEQGain2", "Strip EQ Gain 2", ChannelKind.VirtualInput, "EQGain2", Min: -12, Max: 12, Step: 1, ResetValue: 0)
            { ButtonText = "Mid" },
        // Original VirtualInputEqGain3Adjustment: base(true, true, true, -12, 12) -> -12..12 dB, press = reset.
        // The original guards this one with `if (Remote.Version != VoicemeeterPotato) IsRealClass = false`
        // (same Potato-only guard as EQGain3's neighbours), but PORTING.md's table row 27 doesn't mark it
        // Potato-only. Followed the original source here; see report.
        new("StripEQGain3", "Strip EQ Gain 3", ChannelKind.VirtualInput, "EQGain3", Min: -12, Max: 12, Step: 1, ResetValue: 0)
            { ButtonText = "Treble", PotatoOnly = true },
        // Stage 2 adjustments go here.
    ];
}
