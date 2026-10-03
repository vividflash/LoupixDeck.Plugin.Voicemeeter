using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// One dial preset per strip and bus: turn = gain, press = mute. The gain command is an
/// IAdjustmentCommand (its press would reset the gain), so the press slot is set explicitly and
/// both turn slots carry the gain command.
/// </summary>
internal static class DialPresets
{
    private const string VolumeHigh = "\U000F057E";

    public static IReadOnlyList<DialPresetDescriptor> Build(Edition edition, Func<Channel, string?> label)
    {
        var presets = new List<DialPresetDescriptor>();
        for (var i = 0; i < EditionInfo.Strips(edition); i++)
            presets.Add(Preset(false, i, edition, label));
        for (var i = 0; i < EditionInfo.Buses(edition); i++)
            presets.Add(Preset(true, i, edition, label));
        return presets;
    }

    private static DialPresetDescriptor Preset(bool isBus, int apiIndex, Edition edition, Func<Channel, string?> label)
    {
        var kind = isBus ? ChannelKind.Bus : ChannelKind.Strip;
        var value = isBus ? EditionInfo.BusNames(edition)[apiIndex] : (apiIndex + 1).ToString();
        var parameter = VmParam.ParameterName(kind);
        var gain = new MenuCommandRef
        {
            CommandName = VmCommandBase.Prefix + (isBus ? "BusGain" : "StripGain"),
            Parameters = new Dictionary<string, string> { [parameter] = value, ["Step"] = "1" }
        };
        var mute = new MenuCommandRef
        {
            CommandName = VmCommandBase.Prefix + (isBus ? "BusMute" : "StripMute"),
            Parameters = new Dictionary<string, string> { [parameter] = value }
        };

        return new DialPresetDescriptor
        {
            Id = isBus ? $"bus-{value}-gain-mute" : $"strip-{value}-gain-mute",
            Name = $"{MenuBuilder.ChannelTitle(isBus, apiIndex, edition, label)} – Gain + Mute",
            Glyph = VolumeHigh,
            Actions = new Dictionary<RotaryAction, MenuCommandRef>
            {
                [RotaryAction.CounterClockwise] = gain,
                [RotaryAction.Clockwise] = gain,
                [RotaryAction.Press] = mute
            }
        };
    }
}
