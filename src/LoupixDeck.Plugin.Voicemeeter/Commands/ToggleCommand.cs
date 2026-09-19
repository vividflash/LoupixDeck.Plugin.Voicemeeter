using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// Second index for toggles that exist several times per channel, e.g. StripA: Strip[i].A1..A5.
/// The command gets a second parameter <paramref name="ParameterName"/> (1-based) and the field
/// becomes <paramref name="FieldPrefix"/> + number.
/// </summary>
internal sealed record SubChannelSpec(string ParameterName, string FieldPrefix, Func<Edition, int> Count);

/// <summary>
/// One on/off parameter per strip or bus (the original BooleanBaseCommand). Registering a new
/// toggle is one entry in Actions/Toggles.cs.
/// </summary>
/// <param name="Name">Command name without prefix, e.g. "StripMute" -> "Voicemeeter.StripMute". Never rename once shipped.</param>
/// <param name="DisplayName">Name in the command picker, e.g. "Strip Mute".</param>
/// <param name="Kind">Which channels the command addresses.</param>
/// <param name="Field">API field after "Strip[i].", e.g. "Mute", "EQ.on". Ignored when <see cref="Sub"/> is set.</param>
internal sealed record ToggleSpec(string Name, string DisplayName, ChannelKind Kind, string Field)
{
    /// <summary>Text drawn under the channel label; defaults to <see cref="Field"/> (or the sub name, e.g. "A2").</summary>
    public string? ButtonText { get; init; }

    public PluginColor ActiveColor { get; init; } = Palette.Active;
    public PluginColor InactiveColor { get; init; } = Palette.Inactive;

    /// <summary>Parameter only exists in Voicemeeter Potato; other editions draw "n/a".</summary>
    public bool PotatoOnly { get; init; }

    public SubChannelSpec? Sub { get; init; }

    public string? Description { get; init; }

    public string CommandName => VmCommandBase.Prefix + Name;
}

/// <summary>
/// Toggle command: press flips the parameter (0/1); on a touch button it draws the live state for
/// its own channel and redraws when the value changes in Voicemeeter.
/// Command string: Voicemeeter.StripMute(1) / Voicemeeter.BusMute(A1) / Voicemeeter.StripA(1,2).
/// </summary>
internal sealed class ToggleCommand : VmCommandBase, IDisplayImageCommand
{
    private readonly ToggleSpec _spec;

    public ToggleCommand(ToggleSpec spec, VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
        _spec = spec;
        var channelParam = VmParam.ParameterName(spec.Kind);
        var parameters = new List<CommandParameter> { new(channelParam, typeof(int)) { DefaultValue = "1" } };
        var template = $"({{{channelParam}}})";
        if (spec.Sub != null)
        {
            parameters.Add(new CommandParameter(spec.Sub.ParameterName, typeof(int)) { DefaultValue = "1" });
            template = $"({{{channelParam}}},{{{spec.Sub.ParameterName}}})";
        }

        Descriptor = new CommandDescriptor
        {
            CommandName = spec.CommandName,
            DisplayName = spec.DisplayName,
            Group = Group,
            Description = spec.Description,
            ParameterTemplate = template,
            Parameters = parameters
        };
        vm.RegisterCommand(spec.CommandName);
    }

    public ToggleSpec Spec => _spec;

    public override CommandDescriptor Descriptor { get; }

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    /// <summary>Resolves the API parameter ("Strip[0].Mute") and button texts for this button's parameters.</summary>
    internal bool TryResolve(CommandContext ctx, out string param, out Channel channel, out string buttonText, out string error)
    {
        param = buttonText = string.Empty;
        if (!TryChannel(ctx, _spec.Kind, 0, out channel, out error)) return false;
        if (!VmParam.KindApplies(_spec.Kind, channel.ApiIndex, Vm.Edition))
        {
            error = $"{_spec.DisplayName} needs a {VmParam.KindLabel(_spec.Kind)} strip";
            return false;
        }

        if (_spec.PotatoOnly && Vm.Edition != Edition.Potato)
        {
            error = $"{_spec.DisplayName} needs Voicemeeter Potato";
            return false;
        }

        var field = _spec.Field;
        buttonText = _spec.ButtonText ?? _spec.Field;
        if (_spec.Sub is { } sub)
        {
            var raw = ctx.Parameters.Length > 1 ? ctx.Parameters[1].Trim() : "1";
            var count = sub.Count(Vm.Edition);
            if (!int.TryParse(raw, out var n) || n < 1 || n > count)
            {
                error = $"{sub.ParameterName} '{raw}' does not exist in {EditionInfo.DisplayName(Vm.Edition)} (1-{count})";
                return false;
            }

            field = sub.FieldPrefix + n;
            buttonText = _spec.ButtonText ?? field;
        }

        param = channel.Param(field);
        return true;
    }

    protected override Task Run(CommandContext ctx)
    {
        if (!Vm.IsConnected)
        {
            Fail(ctx, Vm.Status, "offline");
            return Task.CompletedTask;
        }

        if (!TryResolve(ctx, out var param, out var channel, out var text, out var error))
        {
            Fail(ctx, error);
            return Task.CompletedTask;
        }

        var on = Vm.TryGetFloat(param, Name, out var current) && current >= 0.5f;
        if (!Vm.TrySetFloat(param, on ? 0f : 1f, out error))
        {
            Fail(ctx, error, "Failed");
            return Task.CompletedTask;
        }

        ShowOverlay(ctx, $"{Vm.GetLabel(channel, Name)} {text} {(on ? "off" : "on")}");
        return Task.CompletedTask;
    }

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        try
        {
            if (!Vm.IsConnected || !TryResolve(ctx, out var param, out var channel, out var text, out _))
                return RenderUnavailable(canvas, _spec.ButtonText ?? _spec.DisplayName);

            if (!Vm.TryGetFloat(param, Name, out var value))
                return RenderUnavailable(canvas, text);

            Render.Toggle(canvas, Vm.GetLabel(channel, Name), text, value >= 0.5f, _spec.ActiveColor, _spec.InactiveColor);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"{Name}: render failed", ex);
            return false;
        }
    }
}
