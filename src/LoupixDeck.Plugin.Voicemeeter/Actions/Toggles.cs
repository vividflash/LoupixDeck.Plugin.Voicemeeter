using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;

namespace LoupixDeck.Plugin.Voicemeeter.Actions;

/// <summary>
/// On/off parameters per strip or bus.
/// Append one ToggleSpec per action; nothing else needs registering (commands and menu entries
/// are generated from this list).
/// </summary>
internal static class Toggles
{
    public static IReadOnlyList<ToggleSpec> All { get; } =
    [
        new("StripMute", "Strip Mute", ChannelKind.Strip, "Mute") { ActiveColor = Palette.Danger, Description = "Mutes or unmutes an input strip" },
        new("BusMute", "Bus Mute", ChannelKind.Bus, "Mute") { ActiveColor = Palette.Danger, Description = "Mutes or unmutes an output bus" },

        new("StripSolo", "Strip Solo", ChannelKind.Strip, "Solo") { Description = "Solos an input strip" },
        new("StripA", "Strip A", ChannelKind.Strip, "A")
        {
            Sub = new SubChannelSpec("Bus", "A", EditionInfo.ABuses),
            Description = "Assigns an input strip to a physical (A) output bus"
        },
        new("StripB", "Strip B", ChannelKind.Strip, "B")
        {
            Sub = new SubChannelSpec("Bus", "B", EditionInfo.BBuses),
            Description = "Assigns an input strip to a virtual (B) output bus"
        },
        // Takes the global strip number; hardware inputs only is an applicability restriction (draws
        // "n/a" on a virtual strip).
        new("StripMono", "Strip Mono", ChannelKind.HardwareInput, "Mono") { Description = "Toggles mono on a hardware input strip" },
        // Strip[i].MC, the M.C (mute center) button a virtual input has in place of Mono.
        new("StripMC", "Strip M.C", ChannelKind.VirtualInput, "MC") { ButtonText = "M.C", Description = "Mutes the center (voice) of a virtual input strip" },
        new("StripPostReverb", "Strip Post Reverb", ChannelKind.HardwareInput, "PostReverb") { PotatoOnly = true, Description = "Toggles the post-reverb send on a hardware input strip" },
        new("StripPostDelay", "Strip Post Delay", ChannelKind.HardwareInput, "PostDelay") { PotatoOnly = true, Description = "Toggles the post-delay send on a hardware input strip" },
        new("StripPostFx1", "Strip Post Fx1", ChannelKind.HardwareInput, "PostFx1") { PotatoOnly = true, Description = "Toggles the post-Fx1 send on a hardware input strip" },
        new("StripPostFx2", "Strip Post Fx2", ChannelKind.HardwareInput, "PostFx2") { PotatoOnly = true, Description = "Toggles the post-Fx2 send on a hardware input strip" },
        new("BusMono", "Bus Mono", ChannelKind.Bus, "Mono") { Description = "Toggles mono on an output bus" },
        new("BusEQ", "Bus EQ", ChannelKind.Bus, "EQ.on") { ButtonText = "EQ", MinEdition = Edition.Banana, Description = "Toggles the EQ on an output bus" },
        new("BusSel", "Bus Sel", ChannelKind.Bus, "Sel") { ActiveColor = Palette.SelActive, PotatoOnly = true, Description = "Selects an output bus" },
    ];
}
