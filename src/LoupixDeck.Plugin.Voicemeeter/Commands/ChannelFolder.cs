using LoupixDeck.Plugin.Voicemeeter.Actions;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// Opens a touch folder with every toggle of one channel: Voicemeeter.ChannelFolder(1) for a
/// strip, Voicemeeter.ChannelFolder(A1) for a bus. A number is a strip, a name (A1, B2) is a bus,
/// like the level strip's channel list.
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
        Description = "Opens a folder with every toggle of a strip (number) or bus (A1, B2)",
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

        ctx.Host.OpenFolder(new ChannelFolder(channel, Vm.Edition, Toggles.All, Vm, ctx.Host, Log));
        return Task.CompletedTask;
    }
}

/// <summary>
/// The folder behind <see cref="ChannelFolderCommand"/>: one key per toggle that applies to the
/// channel (StripA / StripB once per bus), in the order of Actions/Toggles.cs, drawn like the
/// toggle's own touch button. Keys fill the grid in reading order around the host's back key; what
/// does not fit (a Potato hardware strip has 15 toggles, a 4x3 grid 11 free keys) is left out.
/// </summary>
internal sealed class ChannelFolder : FolderProviderBase
{
    private readonly record struct Item(ToggleSpec Spec, string Param, string Text);

    private readonly Channel _channel;
    private readonly Edition _edition;
    private readonly VoicemeeterService _vm;
    private readonly IPluginHost _host;
    private readonly IPluginLogger _log;
    private readonly List<Item> _items = [];

    public ChannelFolder(Channel channel, Edition edition, IReadOnlyList<ToggleSpec> toggles,
        VoicemeeterService vm, IPluginHost host, IPluginLogger log)
    {
        _channel = channel;
        _edition = edition;
        _vm = vm;
        _host = host;
        _log = log;

        var isBus = channel.Kind == ChannelKind.Bus;
        foreach (var spec in toggles)
        {
            if (spec.PotatoOnly && edition != Edition.Potato) continue;
            if (MenuBuilder.ParameterValue(spec.Kind, isBus, channel.ApiIndex, edition) == null) continue;
            if (spec.Sub is { } sub)
            {
                for (var n = 1; n <= sub.Count(edition); n++)
                    _items.Add(new Item(spec, channel.Param(sub.FieldPrefix + n), spec.ButtonText ?? sub.FieldPrefix + n));
            }
            else
            {
                _items.Add(new Item(spec, channel.Param(spec.Field), spec.ButtonText ?? spec.Field));
            }
        }
    }

    public override string Title =>
        MenuBuilder.ChannelTitle(_channel.Kind == ChannelKind.Bus, _channel.ApiIndex, _edition, _vm.PeekLabel);

    public override void OnEnter() => _vm.CommandRefreshed += OnRefreshed;

    public override void OnExit() => _vm.CommandRefreshed -= OnRefreshed;

    private void OnRefreshed(string commandName)
    {
        if (commandName == ChannelFolderCommand.CommandName) RaiseEntriesChanged();
    }

    public override IReadOnlyList<FolderEntry> BuildEntries()
    {
        var grid = _host.FolderGrid;
        var entries = new List<FolderEntry>();
        var label = _vm.GetLabel(_channel, ChannelFolderCommand.CommandName);
        for (var i = 0; i < _items.Count; i++)
        {
            var slot = grid.SlotForIndex(i);
            if (slot < 0) break;

            // Values are read here, not in Render: the host calls Render under its render lock.
            var item = _items[i];
            var readable = _vm.TryGetFloat(item.Param, ChannelFolderCommand.CommandName, out var value);
            var on = value >= 0.5f;
            var reason = Localization.Tr(_vm.IsConnected ? "n/a" : "offline");
            entries.Add(new FolderEntry
            {
                SlotIndex = slot,
                Render = canvas =>
                {
                    if (readable) Render.Toggle(canvas, label, item.Text, on, item.Spec.ActiveColor, item.Spec.InactiveColor);
                    else Render.Unavailable(canvas, item.Text, reason);
                },
                OnPress = () => Toggle(item)
            });
        }

        return entries;
    }

    private Task Toggle(Item item)
    {
        try
        {
            if (!_vm.IsConnected)
            {
                _log.Warn($"{ChannelFolderCommand.CommandName}: {_vm.Status}");
                return Task.CompletedTask;
            }

            var on = _vm.TryGetFloat(item.Param, ChannelFolderCommand.CommandName, out var current) && current >= 0.5f;
            if (!_vm.TrySetFloat(item.Param, on ? 0f : 1f, out var error))
                _log.Warn($"{ChannelFolderCommand.CommandName}: {error}");
        }
        catch (Exception ex)
        {
            _log.Error($"{ChannelFolderCommand.CommandName}: failed", ex);
        }

        return Task.CompletedTask;
    }
}
