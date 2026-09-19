using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Actions;

/// <summary>
/// Family D: level meters (original LevelsCommand). Levels are read through the shared poller
/// in VoicemeeterService (TryGetLevel / SubscribeLevels), which only reads while a meter is shown.
/// </summary>
internal static class Levels
{
    public static IEnumerable<IPluginCommand> Create(VoicemeeterService vm, IPluginLogger log)
    {
        // Original LevelsCommand: Voicemeeter.Level(channel,type).
        yield return new LevelCommand(vm, log);
    }

    public static IEnumerable<ISideStripProvider> CreateSideStrips(VoicemeeterService vm, IPluginLogger log, IPluginSettings settings)
    {
        yield return new LevelStripProvider(vm, settings, log);
    }

    /// <summary>Settings page entries for the level strip (appended by VoicemeeterPlugin).</summary>
    public static IEnumerable<PluginSettingDescriptor> Settings =>
    [
        new PluginSettingDescriptor
        {
            Key = LevelStripProvider.FallbackKey,
            Label = "Level strip channels",
            Kind = PluginSettingKind.Text,
            DefaultValue = LevelStripProvider.DefaultFallback,
            Description = "Channels the Voicemeeter Levels strip shows when no dial next to it is bound to a Voicemeeter command. " +
                          "Numbers are strips, A1..B3 are buses, e.g. 1,A1,B1."
        }
    ];
}
