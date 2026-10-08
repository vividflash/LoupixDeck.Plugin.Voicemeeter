using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;

namespace LoupixDeck.Plugin.Voicemeeter.Actions;

/// <summary>
/// Family A: on/off parameters per strip or bus (original BooleanBaseCommand subclasses).
/// Append one ToggleSpec per action; nothing else needs registering (commands and menu entries
/// are generated from this list).
/// </summary>
internal static class Toggles
{
    public static IReadOnlyList<ToggleSpec> All { get; } =
    [
        // Original StripMuteCommand: Strip[i].Mute, active colour Danger.
        new("StripMute", "Strip Mute", ChannelKind.Strip, "Mute") { ActiveColor = Palette.Danger, Description = "Mutes or unmutes an input strip" },
        // Original BusMuteCommand: Bus[i].Mute, active colour Danger.
        new("BusMute", "Bus Mute", ChannelKind.Bus, "Mute") { ActiveColor = Palette.Danger, Description = "Mutes or unmutes an output bus" },

        // Original StripSoloCommand: Strip[i].Solo.
        new("StripSolo", "Strip Solo", ChannelKind.Strip, "Solo") { Description = "Solos an input strip" },
        // Original StripACommand: Strip[i].A1..A5 (count per edition).
        new("StripA", "Strip A", ChannelKind.Strip, "A")
        {
            Sub = new SubChannelSpec("Bus", "A", EditionInfo.ABuses),
            Description = "Assigns an input strip to a physical (A) output bus"
        },
        // Original StripBCommand: Strip[i].B1..B3 (count per edition).
        new("StripB", "Strip B", ChannelKind.Strip, "B")
        {
            Sub = new SubChannelSpec("Bus", "B", EditionInfo.BBuses),
            Description = "Assigns an input strip to a virtual (B) output bus"
        },
        // Original HardwareInputMonoCommand: Strip[i].Mono. Takes the global strip number; hardware
        // inputs only is an applicability restriction (draws "n/a" on a virtual strip).
        new("StripMono", "Strip Mono", ChannelKind.HardwareInput, "Mono") { Description = "Toggles mono on a hardware input strip" },
        // Original HardwareInputPostReverbCommand: Strip[i].PostReverb, hardware inputs only, Potato only.
        new("StripPostReverb", "Strip Post Reverb", ChannelKind.HardwareInput, "PostReverb") { PotatoOnly = true, Description = "Toggles the post-reverb send on a hardware input strip" },
        // Original HardwareInputPostDelayCommand: Strip[i].PostDelay, hardware inputs only, Potato only.
        new("StripPostDelay", "Strip Post Delay", ChannelKind.HardwareInput, "PostDelay") { PotatoOnly = true, Description = "Toggles the post-delay send on a hardware input strip" },
        // Original HardwareInputPostFx1Command (PostFx file): Strip[i].PostFx1, hardware inputs only, Potato only.
        new("StripPostFx1", "Strip Post Fx1", ChannelKind.HardwareInput, "PostFx1") { PotatoOnly = true, Description = "Toggles the post-Fx1 send on a hardware input strip" },
        // Original HardwareInputPostFx2Command (PostFx file): Strip[i].PostFx2, hardware inputs only, Potato only.
        new("StripPostFx2", "Strip Post Fx2", ChannelKind.HardwareInput, "PostFx2") { PotatoOnly = true, Description = "Toggles the post-Fx2 send on a hardware input strip" },
        // Original BusMonoCommand: Bus[i].Mono. Original passed the strip count to CreateCommands (a
        // bug); this port uses the correct Bus channel kind and count per PORTING.md.
        new("BusMono", "Bus Mono", ChannelKind.Bus, "Mono") { Description = "Toggles mono on an output bus" },
        // Original BusEqCommand (BusEQCommand.cs): Bus[i].EQ.on. Same strip-count bug as BusMono; corrected to Bus here.
        new("BusEQ", "Bus EQ", ChannelKind.Bus, "EQ.on") { ButtonText = "EQ", MinEdition = Edition.Banana, Description = "Toggles the EQ on an output bus" },
        // Original BusSelCommand: Bus[i].Sel, active colour SelActive. Same strip-count bug as BusMono; corrected to Bus here.
        new("BusSel", "Bus Sel", ChannelKind.Bus, "Sel") { ActiveColor = Palette.SelActive, PotatoOnly = true, Description = "Selects an output bus" },
        // Stage 2 toggles go here.
    ];
}
