using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter;

/// <summary>
/// Thin wrapper around <see cref="IPluginHost.Tr"/> for texts the plugin draws or flashes itself
/// (button captions, dial overlays, menu titles built from parts). Descriptor texts that are fixed
/// strings (display names, descriptions, settings, requirements) need no call: the host looks
/// them up in strings.&lt;code&gt;.json by their exact English text.
/// </summary>
internal static class Localization
{
    private static IPluginHost? _host;

    /// <summary>Called from <see cref="VoicemeeterPlugin.Initialize"/>.</summary>
    internal static void SetHost(IPluginHost? host) => _host = host;

    /// <summary>Test-only: undoes <see cref="SetHost"/>.</summary>
    internal static void ResetForTests() => _host = null;

    /// <summary>Translates <paramref name="english"/>; unchanged when there is no host yet.</summary>
    internal static string Tr(string english) => _host?.Tr(english) ?? english;
}
