using LoupixDeck.Plugin.Voicemeeter.Actions;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// What one knob of the channel folder carries. The host shows an enum parameter as a list of
/// these names, and the name is what the binding stores. Default first (the host's pick for an
/// unset value), then the key text of every adjustment without its spaces; strips and buses
/// share Gain.
/// </summary>
public enum KnobChoice
{
    Default,
    Gain,
    Comp,
    Gate,
    Reverb,
    Delay,
    Fx1,
    Fx2,
    PanX,
    PanY,
    Bass,
    Mid,
    Treble
}

/// <summary>
/// Opens a touch folder with the knobs and toggles of one channel: Voicemeeter.ChannelFolder(1)
/// for a strip, Voicemeeter.ChannelFolder(A1) for a bus. A number is a strip, a name (A1, B2) is
/// a bus, like the level strip's channel list. Six optional parameters pick the adjustment of
/// each knob: Voicemeeter.ChannelFolder(7,Default,Bass) puts Bass on the left middle knob.
/// </summary>
internal sealed class ChannelFolderCommand : VmCommandBase
{
    public const string CommandName = Prefix + "ChannelFolder";

    /// <summary>One parameter per knob, in host order (rotary 0-5). Appended after Channel: older bindings carry only the channel.</summary>
    internal static readonly string[] KnobParameters =
        ["Left top", "Left middle", "Left bottom", "Right top", "Right middle", "Right bottom"];

    private readonly SideStripDevices _stripDevices = new();

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
        ParameterTemplate = "({Channel}," + string.Join(",", KnobParameters.Select(name => "{" + name + "}")) + ")",
        Parameters =
        [
            new CommandParameter("Channel", typeof(string)) { DefaultValue = "1" },
            .. KnobParameters.Select(name => new CommandParameter(name, typeof(KnobChoice)) { DefaultValue = nameof(KnobChoice.Default) })
        ]
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

        var picks = ctx.Parameters.Skip(1).Take(KnobParameters.Length).ToArray();
        ctx.Host.OpenFolder(new ChannelFolder(channel, Vm.Edition, Toggles.All, Adjustments.All, Vm, ctx.Host, Log, picks, _stripDevices));
        return Task.CompletedTask;
    }
}

/// <summary>
/// The devices on which the host asked a channel folder for a side strip session. The SDK has no
/// member that says whether the host draws side strips for a folder; that request is the only
/// sign, and it comes after the folder's first paint. A folder opened later on a device from this
/// list starts without value keys.
/// </summary>
internal sealed class SideStripDevices
{
    private readonly HashSet<DeviceInfo> _devices = [];

    public bool Contains(DeviceInfo? device)
    {
        if (device == null) return false;
        lock (_devices) return _devices.Contains(device);
    }

    public void Add(DeviceInfo? device)
    {
        if (device == null) return;
        lock (_devices) _devices.Add(device);
    }
}

/// <summary>
/// The folder behind <see cref="ChannelFolderCommand"/>, generated from Actions/Toggles.cs and
/// Actions/Adjustments.cs for the specs that apply to the channel.
/// On a grid with 3 rows and at least 3 columns all six knobs get an adjustment, with label and
/// value for the host's dial indicator in the side strips. A knob carries the adjustment picked
/// for it; the others take the channel's remaining ones by <see cref="KnobPriority"/>. The outer columns are value keys for the
/// knobs next to them (left column rows 1-2, right column rows 1-3; the bottom-left key is the
/// host's back key, so the third left knob has no value key) and the columns in between are toggles. When the
/// toggles do not fit, the last toggle key is "More" and shows the next ones, round and round; the
/// knobs stay.
/// Where the host draws the side strips (it asks for a strip session, see
/// <see cref="CreateSideStripSession"/>) the value keys would repeat them, so every key but the
/// back key is a toggle: Mute, Solo, Mono or M.C, the A and B buses, then the rest.
/// On Potato a strip's Gain knob carries the gain layer of the bus whose SEL is on.
/// On any other grid the folder is every toggle in table order (StripA / StripB once per bus) in
/// reading order around the back key, without knobs; what does not fit is left out.
/// </summary>
internal sealed class ChannelFolder : FolderProviderBase, IFolderSideStripProvider
{
    /// <summary>Knobs in the order the unpicked ones are filled: left 1-2, right 1-3, then left 3 (host order: 0-2 left, 3-5 right, top to bottom).</summary>
    private static readonly int[] KnobRotaries = [0, 1, 3, 4, 5, 2];

    /// <summary>The knob next to the host's back key: no value key.</summary>
    private const int BackKeyRotary = 2;

    /// <summary>Adjustments by spec name, most wanted first; the rest follow in table order.</summary>
    private static readonly string[] KnobPriority =
    [
        "StripGain", "BusGain", "StripComp", "StripGate", "StripReverb", "StripDelay",
        "StripEQGain1", "StripEQGain2", "StripEQGain3", "StripPanX", "StripPanY", "StripFx1", "StripFx2"
    ];

    /// <summary>Toggles by field, most wanted first; the rest follow in table order.</summary>
    private static readonly string[] TogglePriority = ["Mute", "Solo", "MC", "A1", "A2", "B1", "B2"];

    /// <summary>The same for the layout without value keys: every bus fits, so they come in bus order.</summary>
    private static readonly string[] StripTogglePriority =
        ["Mute", "Solo", "Mono", "MC", "A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3"];

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

    /// <summary>The toggles in the order of the layout without value keys.</summary>
    private readonly List<Item> _togglesForStrips;

    private readonly SideStripDevices? _stripDevices;
    private readonly DeviceInfo? _device;

    /// <summary>True once the host is known to draw the side strips for this folder: no value keys.</summary>
    private volatile bool _sideStrips;

    /// <summary>The adjustment of each knob by rotary index, null for a knob without one.</summary>
    private readonly AdjustmentSpec?[] _knobs;

    private readonly Dictionary<int, RotaryOverride> _overrides = [];

    /// <summary>
    /// The strip's Gain when it sits on a knob of a Potato strip: while a bus has SEL on, that knob
    /// carries the strip's gain layer for the bus (Strip[i].GainLayer[j]). Null for buses, other
    /// editions and a strip without Gain on a knob; then no Sel parameter is read.
    /// </summary>
    private readonly AdjustmentSpec? _layerGain;

    /// <summary>Bus[j].Sel of every bus, by API index; empty without <see cref="_layerGain"/>.</summary>
    private readonly string[] _selParams = [];

    private sealed record LayerOverrides(int Bus, string Label, IReadOnlyDictionary<int, RotaryOverride> Overrides);

    /// <summary>The overrides as last built for a selected bus; the host wants a fixed label per override.</summary>
    private LayerOverrides? _layer;

    /// <summary>Toggle page of the knob layout, advanced by "More".</summary>
    private int _page;

    public ChannelFolder(Channel channel, Edition edition, IReadOnlyList<ToggleSpec> toggles,
        IReadOnlyList<AdjustmentSpec> adjustments, VoicemeeterService vm, IPluginHost host, IPluginLogger log,
        IReadOnlyList<string?>? picks = null, SideStripDevices? stripDevices = null)
    {
        _channel = channel;
        _edition = edition;
        _vm = vm;
        _host = host;
        _log = log;
        _stripDevices = stripDevices;
        _device = host.ActiveDevice;
        _sideStrips = stripDevices?.Contains(_device) == true;

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
        _togglesForStrips = _toggles.OrderBy(t => Rank(StripTogglePriority, t.Field)).ToList();

        // Same applicability rules as the menu.
        var available = adjustments
            .Where(spec => !spec.PotatoOnly || edition == Edition.Potato)
            .Where(spec => MenuBuilder.ParameterValue(spec.Kind, isBus, channel.ApiIndex, edition) != null)
            .OrderBy(spec => Rank(KnobPriority, spec.Name))
            .ToList();
        _knobs = AssignKnobs(available, picks);

        for (var rotary = 0; rotary < _knobs.Length; rotary++)
        {
            if (_knobs[rotary] is not { } spec) continue;
            _overrides[rotary] = Override(spec);
        }

        if (!isBus && edition == Edition.Potato)
        {
            _layerGain = _knobs.FirstOrDefault(spec => spec is { Name: "StripGain" });
            if (_layerGain != null)
                _selParams = Enumerable.Range(0, EditionInfo.Buses(edition)).Select(bus => VmParam.Name("Bus", bus, "Sel")).ToArray();
        }
    }

    private RotaryOverride Override(AdjustmentSpec spec) => new()
    {
        OnLeft = () => Adjust(spec, AdjustmentSpec.Turn(-1, spec.Step)),
        OnRight = () => Adjust(spec, AdjustmentSpec.Turn(1, spec.Step)),
        OnPress = () => Adjust(spec, spec.Reset),
        Label = spec.Text,
        GetValue = () => Value(spec)
    };

    /// <summary>
    /// API index of the bus whose SEL is on, the lowest when there are several, -1 for none. Reads
    /// every bus, so each Sel is watched and a change redraws the folder.
    /// </summary>
    private int SelectedBus()
    {
        var selected = -1;
        for (var bus = 0; bus < _selParams.Length; bus++)
        {
            if (_vm.TryGetFloat(_selParams[bus], ChannelFolderCommand.CommandName, out var sel) && sel >= 0.5f && selected < 0)
                selected = bus;
        }

        return selected;
    }

    /// <summary>Gain as the gain layer of <paramref name="bus"/>: same range, step and reset, named "Gain B1".</summary>
    private AdjustmentSpec LayerSpec(int bus)
    {
        var name = MenuBuilder.ChannelTitle(true, bus, _edition, channel => _vm.GetLabel(channel, ChannelFolderCommand.CommandName));
        return _layerGain! with { Field = $"GainLayer[{bus}]", ButtonText = string.Format(Localization.Tr("Gain {0}"), name) };
    }

    /// <summary>
    /// Picked knobs first, in host order: a pick the channel does not have, or one an earlier knob
    /// already took, counts as Default. The Default knobs then take what is left, most wanted first.
    /// </summary>
    private static AdjustmentSpec?[] AssignKnobs(List<AdjustmentSpec> available, IReadOnlyList<string?>? picks)
    {
        var knobs = new AdjustmentSpec?[KnobRotaries.Length];
        for (var rotary = 0; rotary < knobs.Length && picks != null && rotary < picks.Count; rotary++)
        {
            var pick = ChoiceKey(picks[rotary]);
            var index = available.FindIndex(spec => ChoiceKey(spec.Text) == pick);
            if (index < 0) continue;
            knobs[rotary] = available[index];
            available.RemoveAt(index);
        }

        foreach (var rotary in KnobRotaries)
        {
            if (knobs[rotary] != null || available.Count == 0) continue;
            knobs[rotary] = available[0];
            available.RemoveAt(0);
        }

        return knobs;
    }

    /// <summary>A pick as compared: "Pan X", "PanX" and "pan_x" are the same adjustment.</summary>
    internal static string ChoiceKey(string? text) =>
        (text ?? string.Empty).Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();

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
    public override IReadOnlyDictionary<int, RotaryOverride> RotaryOverrides
    {
        get
        {
            if (!HasKnobs(_host.FolderGrid)) return base.RotaryOverrides;
            if (_layerGain == null) return _overrides;
            try
            {
                var bus = SelectedBus();
                if (bus < 0) return _overrides;

                var spec = LayerSpec(bus);
                var layer = Volatile.Read(ref _layer);
                if (layer == null || layer.Bus != bus || layer.Label != spec.Text)
                {
                    var overrides = new Dictionary<int, RotaryOverride>(_overrides)
                    {
                        [Array.FindIndex(_knobs, knob => ReferenceEquals(knob, _layerGain))] = Override(spec)
                    };
                    layer = new LayerOverrides(bus, spec.Text, overrides);
                    Volatile.Write(ref _layer, layer);
                }

                return layer.Overrides;
            }
            catch (Exception ex)
            {
                _log.Error($"{ChannelFolderCommand.CommandName}: knobs failed", ex);
                return _overrides;
            }
        }
    }

    /// <summary>
    /// The host asks only on a device with side strips, and only a host that draws them for a
    /// folder asks at all. Null leaves the strips to the host, which draws each knob's label and
    /// value there; the folder then drops its value keys and redraws.
    /// </summary>
    public ISideStripSession? CreateSideStripSession(SideStripContext context)
    {
        try
        {
            if (!_sideStrips && HasKnobs(_host.FolderGrid))
            {
                _sideStrips = true;
                _stripDevices?.Add(_device);
                RaiseEntriesChanged();
            }
        }
        catch (Exception ex)
        {
            _log.Error($"{ChannelFolderCommand.CommandName}: side strips failed", ex);
        }

        return null;
    }

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

        if (_sideStrips)
        {
            var keys = Enumerable.Range(0, grid.TotalSlots).Where(slot => slot != grid.BackSlotIndex).ToList();
            AddToggles(entries, keys, _togglesForStrips, Label, reason);
            return entries;
        }

        var selected = SelectedBus();
        foreach (var rotary in KnobRotaries)
        {
            if (rotary == BackKeyRotary || _knobs[rotary] is not { } spec) continue;
            var slot = KnobSlot(grid, rotary);
            var shown = selected >= 0 && ReferenceEquals(spec, _layerGain) ? LayerSpec(selected) : spec;
            entries.Add(ValueEntry(slot, shown, Label(slot), reason));
        }

        var slots = new List<int>();
        for (var row = 0; row < grid.Rows; row++)
        for (var column = 1; column < grid.Columns - 1; column++)
            slots.Add(row * grid.Columns + column);

        AddToggles(entries, slots, _togglesByPriority, Label, reason);
        return entries;
    }

    /// <summary>The toggles on the given keys; when they do not fit, the last key is "More" and the page decides which ones show.</summary>
    private void AddToggles(List<FolderEntry> entries, List<int> slots, List<Item> toggles, Func<int, string> label, string reason)
    {
        var more = toggles.Count > slots.Count;
        var perPage = more ? slots.Count - 1 : slots.Count;
        var pages = Math.Max(1, (toggles.Count + perPage - 1) / perPage);
        var first = Volatile.Read(ref _page) % pages * perPage;
        for (var i = 0; i < perPage && first + i < toggles.Count; i++)
            entries.Add(ToggleEntry(slots[i], toggles[first + i], label(slots[i]), reason));

        if (!more) return;
        var text = Localization.Tr("More");
        entries.Add(new FolderEntry
        {
            SlotIndex = slots[^1],
            Render = canvas => Render.Toggle(canvas, string.Empty, text, false, Palette.Inactive, Palette.Inactive),
            OnPress = NextPage
        });
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

    /// <summary>
    /// Dial indicator in the side strip: position in the channel's range plus the formatted value,
    /// null (label only) when it cannot be read. The host pulls it on every strip repaint, outside
    /// its render lock. The read watches the parameter, so a change redraws the folder.
    /// </summary>
    private AdjustmentValue? Value(AdjustmentSpec spec)
    {
        try
        {
            if (!_vm.IsConnected) return null;
            if (!_vm.TryGetFloat(_channel.Param(spec.Field), ChannelFolderCommand.CommandName, out var value)) return null;
            var (min, max) = spec.RangeFor(_channel, _edition);
            return new AdjustmentValue(ValueMath.Fraction(value, min, max), spec.FormatValue(value));
        }
        catch (Exception ex)
        {
            _log.Error($"{ChannelFolderCommand.CommandName}: value failed", ex);
            return null;
        }
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

    /// <summary>Knob turn or press. The write redraws the folder: its value key or strip value watches the same parameter.</summary>
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
