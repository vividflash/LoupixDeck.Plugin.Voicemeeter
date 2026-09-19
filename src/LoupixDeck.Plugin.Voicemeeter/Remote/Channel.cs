using System.Globalization;

namespace LoupixDeck.Plugin.Voicemeeter.Remote;

/// <summary>Which channels a command addresses. Inputs are all "Strip[i]" in the API.</summary>
public enum ChannelKind
{
    /// <summary>Any input strip, 1..Strips (hardware first, then virtual).</summary>
    Strip,

    /// <summary>Hardware input strips only, 1..HardwareInputs.</summary>
    HardwareInput,

    /// <summary>Virtual input strips only, 1..VirtualInputs (API index offset by HardwareInputs).</summary>
    VirtualInput,

    /// <summary>Output buses, 1..Buses or by name A1..A5 / B1..B3.</summary>
    Bus
}

/// <summary>A resolved channel. <see cref="ApiIndex"/> is the 0-based index used in "Strip[i]" / "Bus[i]".</summary>
internal readonly record struct Channel(ChannelKind Kind, int ApiIndex, string Name)
{
    public string Prefix => Kind == ChannelKind.Bus ? "Bus" : "Strip";

    /// <summary>API parameter name, e.g. Param("Mute") = "Strip[0].Mute".</summary>
    public string Param(string field) => VmParam.Name(Prefix, ApiIndex, field);

    public string LabelParam => Param("Label");
}

internal static class VmParam
{
    /// <summary>"Strip[0].Mute"-style parameter name.</summary>
    public static string Name(string prefix, int apiIndex, string field) =>
        string.Create(CultureInfo.InvariantCulture, $"{prefix}[{apiIndex}].{field}");

    /// <summary>
    /// Command parameter name shown in the LoupixDeck command builder for a kind. Strip,
    /// HardwareInput and VirtualInput all take the same global 1-based strip number, so they
    /// share the "Strip" label; HardwareInput/VirtualInput only restrict which strips a command
    /// applies to (see <see cref="KindApplies"/>), they no longer use their own numbering.
    /// </summary>
    public static string ParameterName(ChannelKind kind) => kind switch
    {
        ChannelKind.Bus => "Bus",
        ChannelKind.Strip or ChannelKind.HardwareInput or ChannelKind.VirtualInput => "Strip",
        _ => "Channel"
    };

    /// <summary>Lower-case noun for error/description text, e.g. "hardware input strip".</summary>
    public static string KindLabel(ChannelKind kind) => kind switch
    {
        ChannelKind.HardwareInput => "hardware input",
        ChannelKind.VirtualInput => "virtual input",
        _ => "strip"
    };

    /// <summary>User-facing name of the n-th channel (1-based) of a kind, e.g. "Strip 3", "A2".</summary>
    public static string UserName(ChannelKind kind, int number, Edition edition)
    {
        if (kind == ChannelKind.Bus)
        {
            var names = EditionInfo.BusNames(edition);
            return number >= 1 && number <= names.Count ? names[number - 1] : $"Bus {number}";
        }

        return $"Strip {number}";
    }

    /// <summary>
    /// True when a channel at API index <paramref name="apiIndex"/> matches the applicability
    /// restriction of <paramref name="kind"/>. Strip and Bus always match (no restriction);
    /// HardwareInput/VirtualInput restrict to their half of the global strip range.
    /// </summary>
    public static bool KindApplies(ChannelKind kind, int apiIndex, Edition edition) => kind switch
    {
        ChannelKind.HardwareInput => apiIndex < EditionInfo.HardwareInputs(edition),
        ChannelKind.VirtualInput => apiIndex >= EditionInfo.HardwareInputs(edition),
        _ => true
    };

    /// <summary>
    /// Resolves a command parameter to a channel. Strips (and hardware/virtual-input-restricted
    /// commands alike) take the same global 1-based strip number; buses take a 1-based number or
    /// a bus name (A1, B2, case-insensitive). The edition decides the range. This does not check
    /// the HardwareInput/VirtualInput applicability restriction; callers check that separately
    /// with <see cref="KindApplies"/> once they have the resolved channel.
    /// </summary>
    public static bool TryResolve(ChannelKind kind, string? text, Edition edition, out Channel channel, out string error)
    {
        channel = default;
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            error = $"missing {ParameterName(kind)} parameter";
            return false;
        }

        if (edition == Edition.Unknown)
        {
            error = "Voicemeeter is not connected";
            return false;
        }

        if (kind == ChannelKind.Bus)
        {
            var names = EditionInfo.BusNames(edition);
            int busNumber;
            if (char.IsLetter(raw[0]))
            {
                var idx = -1;
                for (var i = 0; i < names.Count; i++)
                {
                    if (string.Equals(names[i], raw, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
                }

                if (idx < 0)
                {
                    error = $"bus {raw} does not exist in {EditionInfo.DisplayName(edition)} ({string.Join(", ", names)})";
                    return false;
                }

                busNumber = idx + 1;
            }
            else if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out busNumber))
            {
                error = $"{ParameterName(kind)} '{raw}' is not a number";
                return false;
            }

            if (busNumber < 1 || busNumber > names.Count)
            {
                error = $"{ParameterName(kind)} {busNumber} does not exist in {EditionInfo.DisplayName(edition)} (1-{names.Count})";
                return false;
            }

            channel = new Channel(ChannelKind.Bus, busNumber - 1, names[busNumber - 1]);
            error = string.Empty;
            return true;
        }

        // Strip, HardwareInput, VirtualInput: same global 1-based strip number for all three.
        var count = EditionInfo.Strips(edition);
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            error = $"{ParameterName(kind)} '{raw}' is not a number";
            return false;
        }

        if (number < 1 || number > count)
        {
            error = $"{ParameterName(kind)} {number} does not exist in {EditionInfo.DisplayName(edition)} (1-{count})";
            return false;
        }

        channel = new Channel(ChannelKind.Strip, number - 1, UserName(ChannelKind.Strip, number, edition));
        error = string.Empty;
        return true;
    }
}
