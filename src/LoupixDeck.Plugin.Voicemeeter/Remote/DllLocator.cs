using System.Runtime.Versioning;
using Microsoft.Win32;

namespace LoupixDeck.Plugin.Voicemeeter.Remote;

/// <summary>
/// Finds the Voicemeeter install folder the same way the original plugin did: the VB uninstall
/// registry key (64-bit view, then WOW6432Node) holds the path of the setup exe, whose folder
/// also holds VoicemeeterRemote64.dll. Falls back to the default install folder.
/// </summary>
internal static class DllLocator
{
    internal const string UninstallKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}";
    internal const string UninstallKey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}";
    internal const string DefaultDir = @"C:\Program Files (x86)\VB\Voicemeeter";

    internal static string DllName => Environment.Is64BitProcess ? "VoicemeeterRemote64.dll" : "VoicemeeterRemote.dll";

    /// <summary>
    /// Returns the DLL paths to try, in order. <paramref name="readUninstallString"/> gets a full
    /// registry key path and returns its UninstallString value or null.
    /// </summary>
    internal static IReadOnlyList<string> Candidates(Func<string, string?> readUninstallString, string dllName)
    {
        var result = new List<string>();
        foreach (var key in new[] { UninstallKey, UninstallKey32 })
        {
            var uninstall = readUninstallString(key);
            if (string.IsNullOrWhiteSpace(uninstall)) continue;
            var dir = Path.GetDirectoryName(uninstall.Trim().Trim('"'));
            if (string.IsNullOrEmpty(dir)) continue;
            var path = Path.Combine(dir, dllName);
            if (!result.Contains(path, StringComparer.OrdinalIgnoreCase)) result.Add(path);
        }

        var fallback = Path.Combine(DefaultDir, dllName);
        if (!result.Contains(fallback, StringComparer.OrdinalIgnoreCase)) result.Add(fallback);
        return result;
    }

    /// <summary>Installer file name heuristic from the original (only used before Voicemeeter runs).</summary>
    internal static Edition EditionFromInstaller(string? uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString)) return Edition.Unknown;
        var exe = Path.GetFileName(uninstallString.Trim().Trim('"'));
        if (exe.Contains('8')) return Edition.Potato;
        if (exe.Contains("pro", StringComparison.OrdinalIgnoreCase)) return Edition.Banana;
        return Edition.Standard;
    }

    [SupportedOSPlatform("windows")]
    internal static string? ReadRegistry(string key)
    {
        try
        {
            return Registry.GetValue(key, "UninstallString", null) as string;
        }
        catch
        {
            return null;
        }
    }
}
