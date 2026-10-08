using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// Builds the channel-centric picker menu from the spec lists: Voicemeeter > Strips / Buses >
/// "1: Mic" > every toggle and adjustment that applies to that channel, plus its level meter and
/// folder. The raw commands address no channel and stay in the host's flat list of the group.
/// </summary>
internal static class MenuBuilder
{
    public static IReadOnlyList<MenuNode> Build(Edition edition,
        IReadOnlyList<ToggleSpec> toggles, IReadOnlyList<AdjustmentSpec> adjustments,
        Func<Channel, string?> label, Func<string, string>? tr = null, bool touch = true)
    {
        tr ??= static s => s;
        var strips = new List<MenuNode>();
        for (var i = 0; i < EditionInfo.Strips(edition); i++)
        {
            var node = ChannelNode(false, i, edition, toggles, adjustments, label, tr, touch);
            if (node != null) strips.Add(node);
        }

        var buses = new List<MenuNode>();
        for (var i = 0; i < EditionInfo.Buses(edition); i++)
        {
            var node = ChannelNode(true, i, edition, toggles, adjustments, label, tr, touch);
            if (node != null) buses.Add(node);
        }

        var children = new List<MenuNode>();
        if (strips.Count > 0) children.Add(new MenuNode { Name = tr("Strips"), CommandName = string.Empty, Children = strips });
        if (buses.Count > 0) children.Add(new MenuNode { Name = tr("Buses"), CommandName = string.Empty, Children = buses });
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
        IReadOnlyList<ToggleSpec> toggles, IReadOnlyList<AdjustmentSpec> adjustments, Func<Channel, string?> label, Func<string, string> tr,
        bool touch)
    {
        var items = new List<MenuNode>();

        foreach (var spec in adjustments)
        {
            if (spec.PotatoOnly && edition != Edition.Potato) continue;
            var value = ParameterValue(spec.Kind, isBus, apiIndex, edition);
            if (value == null) continue;
            var p = new Dictionary<string, string>(StringComparer.Ordinal) { [VmParam.ParameterName(spec.Kind)] = value };
            items.Add(new MenuNode { Name = tr(spec.DisplayName), CommandName = spec.CommandName, Parameters = p });
        }

        foreach (var spec in toggles)
        {
            if (!spec.AvailableIn(edition)) continue;
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
                    items.Add(new MenuNode { Name = $"{tr(spec.DisplayName)} {sub.FieldPrefix}{n}", CommandName = spec.CommandName, Parameters = p });
                }
            }
            else
            {
                var p = new Dictionary<string, string>(StringComparer.Ordinal) { [channelParam] = value };
                items.Add(new MenuNode { Name = tr(spec.DisplayName), CommandName = spec.CommandName, Parameters = p });
            }
        }

        // Meter and folder are touch-button only (their SupportedTargets); the caller leaves them out elsewhere.
        if (touch)
        {
            var channel = isBus ? EditionInfo.BusNames(edition)[apiIndex] : (apiIndex + 1).ToString();
            var meter = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Channel"] = channel,
                ["Type"] = isBus ? "Output" : "PostFader"
            };
            items.Add(new MenuNode { Name = tr("Level Meter"), CommandName = LevelCommand.CommandName, Parameters = meter });
            var folder = new Dictionary<string, string>(StringComparer.Ordinal) { ["Channel"] = channel };
            items.Add(new MenuNode { Name = tr("Channel Folder"), CommandName = ChannelFolderCommand.CommandName, Parameters = folder });
        }

        if (items.Count == 0) return null;

        return new MenuNode { Name = ChannelTitle(isBus, apiIndex, edition, label), CommandName = string.Empty, Children = items };
    }

    /// <summary>"Strip 1" / "A1", with the label set in Voicemeeter appended: "Strip 1: Mic".</summary>
    internal static string ChannelTitle(bool isBus, int apiIndex, Edition edition, Func<Channel, string?> label)
    {
        var name = isBus ? EditionInfo.BusNames(edition)[apiIndex] : $"Strip {apiIndex + 1}";
        var channel = new Channel(isBus ? ChannelKind.Bus : ChannelKind.Strip, apiIndex, name);
        var custom = label(channel);
        return string.IsNullOrWhiteSpace(custom) || custom == name ? name : $"{name}: {custom}";
    }
}
