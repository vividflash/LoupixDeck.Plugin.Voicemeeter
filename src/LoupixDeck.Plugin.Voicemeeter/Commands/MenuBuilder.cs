using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// Builds the channel-centric picker menu from the spec lists: Voicemeeter > Strips / Buses >
/// "1: Mic" > every toggle and adjustment that applies to that channel.
/// </summary>
internal static class MenuBuilder
{
    public static IReadOnlyList<MenuNode> Build(Edition edition,
        IReadOnlyList<ToggleSpec> toggles, IReadOnlyList<AdjustmentSpec> adjustments,
        Func<Channel, string?> label)
    {
        var strips = new List<MenuNode>();
        for (var i = 0; i < EditionInfo.Strips(edition); i++)
        {
            var node = ChannelNode(false, i, edition, toggles, adjustments, label);
            if (node != null) strips.Add(node);
        }

        var buses = new List<MenuNode>();
        for (var i = 0; i < EditionInfo.Buses(edition); i++)
        {
            var node = ChannelNode(true, i, edition, toggles, adjustments, label);
            if (node != null) buses.Add(node);
        }

        var children = new List<MenuNode>();
        if (strips.Count > 0) children.Add(new MenuNode { Name = "Strips", CommandName = string.Empty, Children = strips });
        if (buses.Count > 0) children.Add(new MenuNode { Name = "Buses", CommandName = string.Empty, Children = buses });
        if (children.Count == 0) return [];
        return [new MenuNode { Name = VmCommandBase.Group, CommandName = string.Empty, Children = children }];
    }

    /// <summary>
    /// The parameter value that addresses API channel <paramref name="apiIndex"/> for a spec of
    /// <paramref name="kind"/>, or null when the spec does not cover that channel. Strip,
    /// HardwareInput and VirtualInput all use the same global 1-based strip number; for
    /// HardwareInput/VirtualInput this is null when <paramref name="apiIndex"/> is not of the
    /// required kind (see <see cref="VmParam.KindApplies"/>), which is how a strip's folder only
    /// lists the commands that apply to it.
    /// </summary>
    internal static string? ParameterValue(ChannelKind kind, bool isBus, int apiIndex, Edition edition)
    {
        if (isBus) return kind == ChannelKind.Bus ? EditionInfo.BusNames(edition)[apiIndex] : null;
        if (kind == ChannelKind.Bus) return null;
        return VmParam.KindApplies(kind, apiIndex, edition) ? (apiIndex + 1).ToString() : null;
    }

    private static MenuNode? ChannelNode(bool isBus, int apiIndex, Edition edition,
        IReadOnlyList<ToggleSpec> toggles, IReadOnlyList<AdjustmentSpec> adjustments, Func<Channel, string?> label)
    {
        var items = new List<MenuNode>();

        foreach (var spec in adjustments)
        {
            if (spec.PotatoOnly && edition != Edition.Potato) continue;
            var value = ParameterValue(spec.Kind, isBus, apiIndex, edition);
            if (value == null) continue;
            var p = new Dictionary<string, string>(StringComparer.Ordinal) { [VmParam.ParameterName(spec.Kind)] = value };
            items.Add(new MenuNode { Name = spec.DisplayName, CommandName = spec.CommandName, Parameters = p });
        }

        foreach (var spec in toggles)
        {
            if (spec.PotatoOnly && edition != Edition.Potato) continue;
            var value = ParameterValue(spec.Kind, isBus, apiIndex, edition);
            if (value == null) continue;
            var channelParam = VmParam.ParameterName(spec.Kind);
            if (spec.Sub is { } sub)
            {
                for (var n = 1; n <= sub.Count(edition); n++)
                {
                    var p = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [channelParam] = value,
                        [sub.ParameterName] = n.ToString()
                    };
                    items.Add(new MenuNode { Name = $"{spec.DisplayName} {sub.FieldPrefix}{n}", CommandName = spec.CommandName, Parameters = p });
                }
            }
            else
            {
                var p = new Dictionary<string, string>(StringComparer.Ordinal) { [channelParam] = value };
                items.Add(new MenuNode { Name = spec.DisplayName, CommandName = spec.CommandName, Parameters = p });
            }
        }

        if (items.Count == 0) return null;

        var name = isBus ? EditionInfo.BusNames(edition)[apiIndex] : $"Strip {apiIndex + 1}";
        var channel = new Channel(isBus ? ChannelKind.Bus : ChannelKind.Strip, apiIndex, name);
        var custom = label(channel);
        var title = string.IsNullOrWhiteSpace(custom) || custom == name ? name : $"{name}: {custom}";
        return new MenuNode { Name = title, CommandName = string.Empty, Children = items };
    }
}
