using LoupixDeck.Plugin.Voicemeeter.Actions;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// Opens a touch folder with the knobs and toggles of one channel: Voicemeeter.ChannelFolder(1)
/// for a strip, Voicemeeter.ChannelFolder(A1) for a bus. A number is a strip, a name (A1, B2) is
/// a bus, like the level strip's channel list.
/// </summary>
internal sealed class ChannelFolderCommand : VmCommandBase
{
    public const string CommandName = Prefix + "ChannelFolder";

    public ChannelFolderCommand(VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
        // Watch owner of the folder's reads: no button redraws under this name, the open folder listens for it.
        vm.RegisterCommand(CommandName);
    }

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = CommandName,
        DisplayName = "Channel Folder",
        Group = Group,
        Icon = CommandLooks.Icon("ChannelFolder"),
        ButtonLayout = CommandLooks.IconAndCaption,
        Description = "Opens a folder with the knobs and toggles of a strip (number) or bus (A1, B2)",
        ParameterTemplate = "({Channel})",
        Parameters = [new CommandParameter("Channel", typeof(string)) { DefaultValue = "1" }]
    };

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    /// <summary>Resolves the Channel parameter: a leading letter means a bus name, anything else a strip number.</summary>
    internal static bool TryResolve(string? text, Edition edition, out Channel channel, out string error)
    {
        var raw = (text ?? string.Empty).Trim();
        var kind = raw.Length > 0 && char.IsLetter(raw[0]) ? ChannelKind.Bus : ChannelKind.Strip;
        return VmParam.TryResolve(kind, raw, edition, out channel, out error);
    }

    protected override Task Run(CommandContext ctx)
    {
        if (!Vm.IsConnected)
        {
            Fail(ctx, Vm.Status, "offline");
            return Task.CompletedTask;
        }

        if (!TryResolve(ctx.Parameters.Length > 0 ? ctx.Parameters[0] : null, Vm.Edition, out var channel, out var error))
        {
            Fail(ctx, error);
            return Task.CompletedTask;
        }

        ctx.Host.OpenFolder(new ChannelFolder(channel, Vm.Edition, Toggles.All, Adjustments.All, Vm, ctx.Host, Log));
        return Task.CompletedTask;
    }
}

/// <summary>
/// The folder behind <see cref="ChannelFolderCommand"/>, generated from Actions/Toggles.cs and
/// Actions/Adjustments.cs for the specs that apply to the channel.
/// On a grid with 3 rows and at least 3 columns the outer columns are value keys for the knobs
/// next to them (left column rows 1-2, right column rows 1-3; the bottom-left key is the host's
/// back key, so the knob beside it does nothing) and the columns in between are toggles. When the
/// toggles do not fit, the last toggle key is "More" and shows the next ones, round and round; the
/// knobs stay.
/// On any other grid the folder is every toggle in table order (StripA / StripB once per bus) in
/// reading order around the back key, without knobs; what does not fit is left out.
/// </summary>
internal sealed class ChannelFolder : FolderProviderBase
{
    /// <summary>Knobs that get an adjustment, in assignment order: left 1-2, right 1-3 (host order: 0-2 left, 3-5 right, top to bottom).</summary>
    private static readonly int[] KnobRotaries = [0, 1, 3, 4, 5];

    /// <summary>Adjustments by spec name, most wanted first; the rest follow in table order.</summary>
    private static readonly string[] KnobPriority =
    [
        "StripGain", "BusGain", "StripComp", "StripGate", "StripReverb", "StripDelay",
        "StripEQGain1", "StripEQGain2", "StripEQGain3", "StripPanX", "StripPanY", "StripFx1", "StripFx2"
    ];

    /// <summary>Toggles by field, most wanted first; the rest follow in table order.</summary>
    private static readonly string[] TogglePriority = ["Mute", "Solo", "A1", "A2", "B1", "B2"];

    private readonly record struct Item(ToggleSpec Spec, string Field, string Text, string Param);

    private delegate bool Write(out string error);

    private readonly Channel _channel;
    private readonly Edition _edition;
    private readonly VoicemeeterService _vm;
    private readonly IPluginHost _host;
    private readonly IPluginLogger _log;

    /// <summary>Every toggle of the channel, in table order.</summary>
    private readonly List<Item> _toggles = [];

    private readonly List<Item> _togglesByPriority;

    /// <summary>One adjustment per entry of <see cref="KnobRotaries"/>, fewer when the channel has fewer.</summary>
    private readonly List<AdjustmentSpec> _knobs;

    private readonly Dictionary<int, RotaryOverride> _overrides = [];

    /// <summary>Toggle page of the knob layout, advanced by "More".</summary>
    private int _page;

    public ChannelFolder(Channel channel, Edition edition, IReadOnlyList<ToggleSpec> toggles,
        IReadOnlyList<AdjustmentSpec> adjustments, VoicemeeterService vm, IPluginHost host, IPluginLogger log)
    {
        _channel = channel;
        _edition = edition;
        _vm = vm;
        _host = host;
        _log = log;

        var isBus = channel.Kind == ChannelKind.Bus;
        foreach (var spec in toggles)
        {
            if (!spec.AvailableIn(edition)) continue;
            if (MenuBuilder.ParameterValue(spec.Kind, isBus, channel.ApiIndex, edition) == null) continue;
            if (spec.Sub is { } sub)
            {
                for (var n = 1; n <= sub.Count(edition); n++)
                    AddToggle(spec, sub.FieldPrefix + n);
            }
            else
            {
                AddToggle(spec, spec.Field);
            }
        }

        _togglesByPriority = _toggles.OrderBy(t => Rank(TogglePriority, t.Field)).ToList();

        // Same applicability rules as the menu.
        _knobs = adjustments
            .Where(spec => !spec.PotatoOnly || edition == Edition.Potato)
            .Where(spec => MenuBuilder.ParameterValue(spec.Kind, isBus, channel.ApiIndex, edition) != null)
            .OrderBy(spec => Rank(KnobPriority, spec.Name))
            .Take(KnobRotaries.Length)
            .ToList();

        for (var i = 0; i < _knobs.Count; i++)
        {
            var spec = _knobs[i];
            _overrides[KnobRotaries[i]] = new RotaryOverride
            {
                OnLeft = () => Adjust(spec, AdjustmentSpec.Turn(-1, spec.Step)),
                OnRight = () => Adjust(spec, AdjustmentSpec.Turn(1, spec.Step)),
                OnPress = () => Adjust(spec, spec.Reset)
            };
        }

    }

    private void AddToggle(ToggleSpec spec, string field) =>
        _toggles.Add(new Item(spec, field, spec.ButtonText ?? field, _channel.Param(field)));

    /// <summary>Position in <paramref name="priority"/>, or behind it; OrderBy is stable, so equal ranks keep table order.</summary>
    private static int Rank(string[] priority, string name)
    {
        var index = Array.IndexOf(priority, name);
        return index < 0 ? priority.Length : index;
    }

    public override string Title =>
        MenuBuilder.ChannelTitle(_channel.Kind == ChannelKind.Bus, _channel.ApiIndex, _edition, _vm.PeekLabel);

    /// <summary>The host asks on every knob event, on its device input thread.</summary>
    public override IReadOnlyDictionary<int, RotaryOverride> RotaryOverrides =>
        HasKnobs(_host.FolderGrid) ? _overrides : base.RotaryOverrides;

    public override void OnEnter()
    {
        Volatile.Write(ref _page, 0);
        _vm.CommandRefreshed += OnRefreshed;
    }

    public override void OnExit() => _vm.CommandRefreshed -= OnRefreshed;

    private void OnRefreshed(string commandName)
    {
        if (commandName == ChannelFolderCommand.CommandName) RaiseEntriesChanged();
    }

    /// <summary>Knob layout: 3 rows, an outer column on each side and the back key bottom left.</summary>
    private static bool HasKnobs(FolderGridInfo grid) =>
        grid.Rows == 3 && grid.Columns >= 3 && grid.BackSlotIndex == grid.Columns * 2;

    /// <summary>The key next to a knob: rotary 0-2 in the first column, 3-5 in the last, top to bottom.</summary>
    private static int KnobSlot(FolderGridInfo grid, int rotary) =>
        rotary < 3 ? rotary * grid.Columns : (rotary - 3) * grid.Columns + grid.Columns - 1;

    public override IReadOnlyList<FolderEntry> BuildEntries()
    {
        var grid = _host.FolderGrid;
        var entries = new List<FolderEntry>();
        // The channel is named on the top-left key only.
        var channelLabel = _vm.GetLabel(_channel, ChannelFolderCommand.CommandName);
        string Label(int slot) => slot == 0 ? channelLabel : string.Empty;
        var reason = Localization.Tr(_vm.IsConnected ? "n/a" : "offline");

        if (!HasKnobs(grid))
        {
            for (var i = 0; i < _toggles.Count; i++)
            {
                var slot = grid.SlotForIndex(i);
                if (slot < 0) break;
                entries.Add(ToggleEntry(slot, _toggles[i], Label(slot), reason));
            }

            return entries;
        }

        for (var i = 0; i < _knobs.Count; i++)
                    {
            var slot = KnobSlot(grid, KnobRotaries[i]);
            entries.Add(ValueEntry(slot, _knobs[i], Label(slot), reason));
        }

        var slots = new List<int>();
        for (var row = 0; row < grid.Rows; row++)
        for (var column = 1; column < grid.Columns - 1; column++)
            slots.Add(row * grid.Columns + column);

        var more = _togglesByPriority.Count > slots.Count;
        var perPage = more ? slots.Count - 1 : slots.Count;
        var pages = Math.Max(1, (_togglesByPriority.Count + perPage - 1) / perPage);
        var first = Volatile.Read(ref _page) % pages * perPage;
        for (var i = 0; i < perPage && first + i < _togglesByPriority.Count; i++)
            entries.Add(ToggleEntry(slots[i], _togglesByPriority[first + i], string.Empty, reason));

        if (more)
        {
            var text = Localization.Tr("More");
            entries.Add(new FolderEntry
            {
                SlotIndex = slots[^1],
                Render = canvas => Render.Toggle(canvas, string.Empty, text, false, Palette.Inactive, Palette.Inactive),
                OnPress = NextPage
            });
        }

        return entries;
    }

    // Values are read in the two builders below, not in Render: the host calls Render under its render lock.

    private FolderEntry ToggleEntry(int slot, Item item, string label, string reason)
    {
        var readable = _vm.TryGetFloat(item.Param, ChannelFolderCommand.CommandName, out var value);
        var on = value >= 0.5f;
        return new FolderEntry
        {
            SlotIndex = slot,
            Render = canvas =>
            {
                if (readable) Render.Toggle(canvas, label, item.Text, on, item.Spec.ActiveColor, item.Spec.InactiveColor);
                else Render.Unavailable(canvas, item.Text, reason);
            },
            OnPress = () => Toggle(item)
        };
    }

    /// <summary>The adjustment's own value bar. No OnPress: a touch must not reset a fader.</summary>
    private FolderEntry ValueEntry(int slot, AdjustmentSpec spec, string label, string reason)
    {
        var readable = AdjustmentCommandBase.TryReadBar(spec, _vm, _channel, ChannelFolderCommand.CommandName, out var bar);
        return new FolderEntry
        {
            SlotIndex = slot,
            Render = canvas =>
            {
                if (readable) (bar with { Label = label }).Draw(canvas);
                else Render.Unavailable(canvas, spec.Text, reason);
            }
        };
    }

    private Task NextPage()
    {
        Interlocked.Increment(ref _page);
        RaiseEntriesChanged();
        return Task.CompletedTask;
    }

    private Task Toggle(Item item) => Act((out string error) =>
    {
        var on = _vm.TryGetFloat(item.Param, ChannelFolderCommand.CommandName, out var current) && current >= 0.5f;
        return _vm.TrySetFloat(item.Param, on ? 0f : 1f, out error);
    });

    /// <summary>Knob turn or press. The write redraws the folder: its value key watches the same parameter.</summary>
    private Task Adjust(AdjustmentSpec spec, Func<float, float, float, float> compute) => Act((out string error) =>
        AdjustmentCommandBase.TryWrite(spec, _vm, _channel, ChannelFolderCommand.CommandName, compute, out _, out error));

    /// <summary>Runs a key or knob write. Failures are logged, never thrown: the host does not catch a knob press.</summary>
    private Task Act(Write write)
    {
        try
        {
            if (!_vm.IsConnected)
            {
                _log.Warn($"{ChannelFolderCommand.CommandName}: {_vm.Status}");
                return Task.CompletedTask;
            }

            if (!write(out var error))
                _log.Warn($"{ChannelFolderCommand.CommandName}: {error}");
        }
        catch (Exception ex)
        {
            _log.Error($"{ChannelFolderCommand.CommandName}: failed", ex);
        }

        return Task.CompletedTask;
    }
}
