using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Actions;

/// <summary>
/// Family C: channel-less commands (Show, Eject, Load, Reset, Restart, Shutdown) and the raw
/// API commands. Add command classes (derive from Commands.VmCommandBase) and yield them here.
/// </summary>
internal static class Globals
{
    public static IEnumerable<IPluginCommand> Create(VoicemeeterService vm, IPluginLogger log)
    {
        foreach (var c in GlobalCommands.Create(vm, log)) yield return c;
        yield return new RawCommand(vm, log);
        foreach (var c in RawAdjustmentCommands.Create(vm, log)) yield return c;
    }
}
