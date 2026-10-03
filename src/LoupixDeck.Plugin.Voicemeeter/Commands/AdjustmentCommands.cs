using System.Globalization;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// One continuous parameter per strip or bus (the original SingleBaseAdjustment). Values are in
/// API units (dB for gain). Registering a new adjustment is one entry in Actions/Adjustments.cs;
/// it yields one <see cref="AdjustmentCommand"/> (dial turn = change, press = reset, touch button =
/// live value bar) plus the hidden pre-1.0 {Name}Down / {Name}Up / {Name}Reset commands.
/// </summary>
/// <param name="Name">Command name without prefix, e.g. "StripGain" -> Voicemeeter.StripGain. Never rename once shipped.</param>
/// <param name="DisplayName">Picker name stem, e.g. "Strip Gain".</param>
/// <param name="Kind">Which channels the command addresses.</param>
/// <param name="Field">API field after "Strip[i].", e.g. "Gain", "Comp", "Pan_x", "EQGain1".</param>
/// <param name="Min">Lowest value (clamped).</param>
/// <param name="Max">Highest value (clamped).</param>
/// <param name="Step">Default change per dial tick (overridable by the Step parameter).</param>
/// <param name="ResetValue">Value set by a dial or button press.</param>
internal sealed record AdjustmentSpec(string Name, string DisplayName, ChannelKind Kind, string Field,
    float Min, float Max, float Step, float ResetValue)
{
    /// <summary>Unit shown after the value ("dB", "ms", "" ...).</summary>
    public string Unit { get; init; } = "dB";

    public int Decimals { get; init; } = 1;

    /// <summary>Show "+" on positive values.</summary>
    public bool Signed { get; init; } = true;

    /// <summary>Text drawn under the channel label; defaults to <see cref="Field"/>.</summary>
    public string? ButtonText { get; init; }

    public bool PotatoOnly { get; init; }

    /// <summary>
    /// Per-kind override of (Min, Max) for a spec that applies to every strip but whose range
    /// depends on whether the resolved strip is hardware or virtual (StripPanY: hardware 0..1,
    /// virtual -0.5..0.5). Argument is true for a virtual strip. Null for every other spec, which
    /// just uses <see cref="Min"/>/<see cref="Max"/> everywhere.
    /// </summary>
    public Func<bool, (float Min, float Max)>? RangeByVirtual { get; init; }

    /// <summary>Effective (Min, Max) for <paramref name="channel"/>.</summary>
    public (float Min, float Max) RangeFor(Channel channel, Edition edition) =>
        RangeByVirtual is { } byVirtual ? byVirtual(channel.ApiIndex >= EditionInfo.HardwareInputs(edition)) : (Min, Max);

    /// <summary>
    /// Bar colour like the original: grey when the channel is muted, red above 0, green for strips,
    /// grey for buses. Set false to always use <see cref="BarColor"/>.
    /// </summary>
    public bool ColorByMute { get; init; } = true;

    public PluginColor BarColor { get; init; } = Palette.Active;

    public string CommandName => VmCommandBase.Prefix + Name;

    public string UpName => VmCommandBase.Prefix + Name + "Up";
    public string DownName => VmCommandBase.Prefix + Name + "Down";
    public string ResetName => VmCommandBase.Prefix + Name + "Reset";

    public string FormatValue(float v) => ValueMath.Format(v, Decimals, Unit, Signed);

    public string Text => ButtonText ?? Field;
}

/// <summary>Shared resolution, write path and value bar for the adjustment commands.</summary>
internal abstract class AdjustmentCommandBase : VmCommandBase
{
    protected AdjustmentCommandBase(AdjustmentSpec spec, VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
        Spec = spec;
    }

    public AdjustmentSpec Spec { get; }

    /// <summary>Command name whose button redraws when the value changes.</summary>
    protected virtual string WatchName => Name;

    protected static List<CommandParameter> ChannelParameter(AdjustmentSpec spec) =>
        [new CommandParameter(VmParam.ParameterName(spec.Kind), typeof(int)) { DefaultValue = "1" }];

    protected static CommandParameter StepParameter(AdjustmentSpec spec) =>
        new("Step", typeof(double)) { DefaultValue = spec.Step.ToString(CultureInfo.InvariantCulture) };

    protected static string RangeText(AdjustmentSpec spec) => spec.RangeByVirtual is { } byVirtual
        ? $"{spec.FormatValue(byVirtual(false).Min)} to {spec.FormatValue(byVirtual(false).Max)} on hardware strips, " +
          $"{spec.FormatValue(byVirtual(true).Min)} to {spec.FormatValue(byVirtual(true).Max)} on virtual strips"
        : $"{spec.FormatValue(spec.Min)} to {spec.FormatValue(spec.Max)}";

    protected float StepOf(CommandContext ctx) =>
        ValueMath.ParseStep(ctx.Parameters.Length > 1 ? ctx.Parameters[1] : null, Spec.Step);

    internal bool TryResolve(CommandContext ctx, out string param, out Channel channel, out string error)
    {
        param = string.Empty;
        if (!TryChannel(ctx, Spec.Kind, 0, out channel, out error)) return false;
        if (!VmParam.KindApplies(Spec.Kind, channel.ApiIndex, Vm.Edition))
        {
            error = $"{Spec.DisplayName} needs a {VmParam.KindLabel(Spec.Kind)} strip";
            return false;
        }

        if (Spec.PotatoOnly && Vm.Edition != Edition.Potato)
        {
            error = $"{Spec.DisplayName} needs Voicemeeter Potato";
            return false;
        }

        param = channel.Param(Spec.Field);
        return true;
    }

    /// <summary>
    /// Resolves, clamps and writes <paramref name="compute"/>(current, min, max); flashes the new
    /// value on the dial's slot. min/max are the resolved channel's effective range (see
    /// <see cref="AdjustmentSpec.RangeFor"/>), which for most specs is just Min/Max but for
    /// StripPanY depends on whether the strip is hardware or virtual.
    /// </summary>
    protected Task Apply(CommandContext ctx, Func<float, float, float, float> compute)
    {
        if (!Vm.IsConnected)
        {
            Fail(ctx, Vm.Status, "offline");
            return Task.CompletedTask;
        }

        if (!TryResolve(ctx, out var param, out var channel, out var error))
        {
            Fail(ctx, error);
            return Task.CompletedTask;
        }

        var (min, max) = Spec.RangeFor(channel, Vm.Edition);

        if (!Vm.TryGetFloat(param, WatchName, out var current))
        {
            Fail(ctx, $"could not read {param}", "Failed");
            return Task.CompletedTask;
        }

        var next = ValueMath.Clamp(compute(current, min, max), min, max);
        if (next != current && !Vm.TrySetFloat(param, next, out error))
        {
            Fail(ctx, error, "Failed");
            return Task.CompletedTask;
        }

        ShowOverlay(ctx, $"{Vm.GetLabel(channel, WatchName)} {Spec.FormatValue(next)}");
        return Task.CompletedTask;
    }

    /// <summary>Live value bar for a touch button.</summary>
    protected bool RenderBar(CommandContext ctx, IRenderCanvas canvas)
    {
        try
        {
            if (!Vm.IsConnected || !TryResolve(ctx, out var param, out var channel, out _))
                return RenderUnavailable(canvas, Spec.Text);

            if (!Vm.TryGetFloat(param, Name, out var value))
                return RenderUnavailable(canvas, Spec.Text);

            var (min, max) = Spec.RangeFor(channel, Vm.Edition);
            var color = Spec.BarColor;
            if (Spec.ColorByMute)
            {
                var muted = Vm.TryGetFloat(channel.Param("Mute"), Name, out var m) && m >= 0.5f;
                color = muted ? Palette.Inactive
                    : value > 0 ? Palette.Danger
                    : channel.Kind == ChannelKind.Bus ? Palette.Inactive : Palette.Active;
            }

            Render.Bar(canvas, Vm.GetLabel(channel, Name), Spec.Text, Spec.FormatValue(value),
                ValueMath.Fraction(value, min, max), color);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"{Name}: render failed", ex);
            return false;
        }
    }
}

/// <summary>
/// The knob: Voicemeeter.StripGain(1) / Voicemeeter.StripGain(1,0.5). On a dial a turn changes the
/// value by Step per tick and a press sets <see cref="AdjustmentSpec.ResetValue"/> (original:
/// press = reset to 0 dB). On a touch button it draws the live value bar and a press resets.
/// </summary>
internal sealed class AdjustmentCommand : AdjustmentCommandBase, IAdjustmentCommand, IDisplayImageCommand
{
    public AdjustmentCommand(AdjustmentSpec spec, VoicemeeterService vm, IPluginLogger log) : base(spec, vm, log)
    {
        var channelParam = VmParam.ParameterName(spec.Kind);
        var parameters = ChannelParameter(spec);
        parameters.Add(StepParameter(spec));
        Descriptor = new CommandDescriptor
        {
            CommandName = spec.CommandName,
            DisplayName = spec.DisplayName,
            Group = Group,
            Icon = CommandLooks.Icon(spec.Name),
            ButtonLayout = CommandLooks.SelfDrawn,
            Description = $"Dial: turn changes {spec.Field} by Step ({RangeText(spec)}), press sets " +
                          $"{spec.FormatValue(spec.ResetValue)}; on a touch button shows the live value",
            ParameterTemplate = $"({{{channelParam}}},{{Step}})",
            Parameters = parameters
        };
        vm.RegisterCommand(spec.CommandName);
    }

    public override CommandDescriptor Descriptor { get; }

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    /// <summary>Touch button, macro, CLI: no direction, so it acts like the dial press.</summary>
    protected override Task Run(CommandContext ctx) => Apply(ctx, (_, _, _) => Spec.ResetValue);

    public async Task ApplyAdjustment(CommandContext ctx, int ticks)
    {
        try
        {
            var step = StepOf(ctx);
            await Apply(ctx, (current, min, max) => ValueMath.Step(current, ticks, step, min, max)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error($"{Name}: failed", ex);
            ShowOverlay(ctx, "Failed");
        }
    }

    public Task ApplyReset(CommandContext ctx) => Execute(ctx);

    /// <summary>Dial indicator: position in the effective range plus the formatted value; null when offline or not applicable.</summary>
    public AdjustmentValue? GetValue(CommandContext ctx)
    {
        try
        {
            if (!Vm.IsConnected || !TryResolve(ctx, out var param, out var channel, out _)) return null;
            if (!Vm.TryGetFloat(param, Name, out var value)) return null;
            var (min, max) = Spec.RangeFor(channel, Vm.Edition);
            return new AdjustmentValue(ValueMath.Fraction(value, min, max), Spec.FormatValue(value));
        }
        catch (Exception ex)
        {
            Log.Error($"{Name}: value failed", ex);
            return null;
        }
    }

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas) => RenderBar(ctx, canvas);
}

/// <summary>
/// Pre-1.0 dial turn: Voicemeeter.StripGainUp(1) / Voicemeeter.StripGainDown(1,0.5). Hidden from
/// the menu; stays registered because the host only migrates a dial whose old commands still
/// exist, and for bindings a migration does not touch (macros, buttons, mixed dials).
/// </summary>
internal sealed class AdjustmentStepCommand : AdjustmentCommandBase
{
    private readonly int _direction;

    public AdjustmentStepCommand(AdjustmentSpec spec, bool up, VoicemeeterService vm, IPluginLogger log) : base(spec, vm, log)
    {
        _direction = up ? 1 : -1;
        var channelParam = VmParam.ParameterName(spec.Kind);
        var parameters = ChannelParameter(spec);
        parameters.Add(StepParameter(spec));
        Descriptor = new CommandDescriptor
        {
            CommandName = up ? spec.UpName : spec.DownName,
            DisplayName = $"{spec.DisplayName} {(up ? "Up" : "Down")}",
            Group = Group,
            Icon = CommandLooks.Icon(spec.Name),
            Description = $"{(up ? "Raises" : "Lowers")} {spec.Field} by Step ({RangeText(spec)})",
            ParameterTemplate = $"({{{channelParam}}},{{Step}})",
            Parameters = parameters,
            HiddenFromMenu = true
        };
    }

    public override CommandDescriptor Descriptor { get; }

    protected override string WatchName => Spec.ResetName;

    protected override Task Run(CommandContext ctx)
    {
        var step = StepOf(ctx);
        return Apply(ctx, (current, min, max) => ValueMath.Step(current, _direction, step, min, max));
    }
}

/// <summary>Pre-1.0 dial press / value display: Voicemeeter.StripGainReset(1). Hidden, see <see cref="AdjustmentStepCommand"/>.</summary>
internal sealed class AdjustmentResetCommand : AdjustmentCommandBase, IDisplayImageCommand
{
    public AdjustmentResetCommand(AdjustmentSpec spec, VoicemeeterService vm, IPluginLogger log) : base(spec, vm, log)
    {
        var channelParam = VmParam.ParameterName(spec.Kind);
        Descriptor = new CommandDescriptor
        {
            CommandName = spec.ResetName,
            DisplayName = $"{spec.DisplayName} Reset",
            Group = Group,
            Icon = CommandLooks.Icon(spec.Name),
            ButtonLayout = CommandLooks.SelfDrawn,
            Description = $"Sets {spec.Field} to {spec.FormatValue(spec.ResetValue)}; on a touch button shows the live value",
            ParameterTemplate = $"({{{channelParam}}})",
            Parameters = ChannelParameter(spec),
            HiddenFromMenu = true
        };
        vm.RegisterCommand(spec.ResetName);
    }

    public override CommandDescriptor Descriptor { get; }

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    protected override Task Run(CommandContext ctx) => Apply(ctx, (_, _, _) => Spec.ResetValue);

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas) => RenderBar(ctx, canvas);
}

internal static class AdjustmentCommands
{
    /// <summary>The knob command for one spec, then its hidden pre-1.0 Down / Up / Reset.</summary>
    public static IEnumerable<IPluginCommand> Create(AdjustmentSpec spec, VoicemeeterService vm, IPluginLogger log)
    {
        yield return new AdjustmentCommand(spec, vm, log);
        yield return new AdjustmentStepCommand(spec, up: false, vm, log);
        yield return new AdjustmentStepCommand(spec, up: true, vm, log);
        yield return new AdjustmentResetCommand(spec, vm, log);
    }

    /// <summary>
    /// Moves pre-1.0 dials onto the knob command: Down/Up/Reset on turn/turn/press, and Down/Up
    /// with nothing on press. The host leaves any other dial alone (e.g. a different command on
    /// press). Ids are recorded in the user's config; never change them.
    /// </summary>
    public static IEnumerable<CommandMigration> Migrations(AdjustmentSpec spec)
    {
        var channelParam = VmParam.ParameterName(spec.Kind);
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [channelParam] = $"{{{channelParam}}}",
            ["Step"] = "{Step}"
        };
        yield return new CommandMigration
        {
            Id = $"{spec.Name}-dial",
            From = new Dictionary<RotaryAction, string>
            {
                [RotaryAction.CounterClockwise] = spec.DownName,
                [RotaryAction.Clockwise] = spec.UpName,
                [RotaryAction.Press] = spec.ResetName
            },
            To = spec.CommandName,
            Parameters = parameters
        };
        yield return new CommandMigration
        {
            Id = $"{spec.Name}-turn",
            From = new Dictionary<RotaryAction, string>
            {
                [RotaryAction.CounterClockwise] = spec.DownName,
                [RotaryAction.Clockwise] = spec.UpName
            },
            To = spec.CommandName,
            Parameters = parameters
        };
    }
}