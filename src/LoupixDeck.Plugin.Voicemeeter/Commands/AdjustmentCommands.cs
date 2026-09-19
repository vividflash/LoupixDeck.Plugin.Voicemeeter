using System.Globalization;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// One continuous parameter per strip or bus (the original SingleBaseAdjustment). Values are in
/// API units (dB for gain). Registering a new adjustment is one entry in Actions/Adjustments.cs;
/// it yields three commands: {Name}Down / {Name}Up (dial turn) and {Name}Reset (dial press, and
/// on a touch button a live value bar).
/// </summary>
/// <param name="Name">Command name stem without prefix, e.g. "StripGain" -> Voicemeeter.StripGainUp/Down/Reset. Never rename once shipped.</param>
/// <param name="DisplayName">Picker name stem, e.g. "Strip Gain".</param>
/// <param name="Kind">Which channels the command addresses.</param>
/// <param name="Field">API field after "Strip[i].", e.g. "Gain", "Comp", "Pan_x", "EQGain1".</param>
/// <param name="Min">Lowest value (clamped).</param>
/// <param name="Max">Highest value (clamped).</param>
/// <param name="Step">Default change per dial tick (overridable by the Step parameter).</param>
/// <param name="ResetValue">Value set by the Reset command.</param>
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

    public string UpName => VmCommandBase.Prefix + Name + "Up";
    public string DownName => VmCommandBase.Prefix + Name + "Down";
    public string ResetName => VmCommandBase.Prefix + Name + "Reset";

    public string FormatValue(float v) => ValueMath.Format(v, Decimals, Unit, Signed);

    public string Text => ButtonText ?? Field;
}

/// <summary>Shared resolution for the three adjustment commands.</summary>
internal abstract class AdjustmentCommandBase : VmCommandBase
{
    protected AdjustmentCommandBase(AdjustmentSpec spec, VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
        Spec = spec;
    }

    public AdjustmentSpec Spec { get; }

    protected static List<CommandParameter> ChannelParameter(AdjustmentSpec spec) =>
        [new CommandParameter(VmParam.ParameterName(spec.Kind), typeof(int)) { DefaultValue = "1" }];

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

        if (!Vm.TryGetFloat(param, Spec.ResetName, out var current))
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

        ShowOverlay(ctx, $"{Vm.GetLabel(channel, Spec.ResetName)} {Spec.FormatValue(next)}");
        return Task.CompletedTask;
    }
}

/// <summary>Dial turn: Voicemeeter.StripGainUp(1) / Voicemeeter.StripGainDown(1,0.5). Optional Step parameter.</summary>
internal sealed class AdjustmentStepCommand : AdjustmentCommandBase
{
    private readonly int _direction;

    public AdjustmentStepCommand(AdjustmentSpec spec, bool up, VoicemeeterService vm, IPluginLogger log) : base(spec, vm, log)
    {
        _direction = up ? 1 : -1;
        var channelParam = VmParam.ParameterName(spec.Kind);
        var parameters = ChannelParameter(spec);
        parameters.Add(new CommandParameter("Step", typeof(double)) { DefaultValue = spec.Step.ToString(CultureInfo.InvariantCulture) });
        var rangeText = spec.RangeByVirtual is { } byVirtual
            ? $"{spec.FormatValue(byVirtual(false).Min)} to {spec.FormatValue(byVirtual(false).Max)} on hardware strips, " +
              $"{spec.FormatValue(byVirtual(true).Min)} to {spec.FormatValue(byVirtual(true).Max)} on virtual strips"
            : $"{spec.FormatValue(spec.Min)} to {spec.FormatValue(spec.Max)}";
        Descriptor = new CommandDescriptor
        {
            CommandName = up ? spec.UpName : spec.DownName,
            DisplayName = $"{spec.DisplayName} {(up ? "Up" : "Down")}",
            Group = Group,
            Description = $"{(up ? "Raises" : "Lowers")} {spec.Field} by Step ({rangeText})",
            ParameterTemplate = $"({{{channelParam}}},{{Step}})",
            Parameters = parameters
        };
    }

    public override CommandDescriptor Descriptor { get; }

    protected override Task Run(CommandContext ctx)
    {
        var step = ValueMath.ParseStep(ctx.Parameters.Length > 1 ? ctx.Parameters[1] : null, Spec.Step);
        return Apply(ctx, (current, min, max) => ValueMath.Step(current, _direction, step, min, max));
    }
}

/// <summary>
/// Dial press: sets <see cref="AdjustmentSpec.ResetValue"/> (original: press = reset to 0 dB).
/// On a touch button it draws the live value bar, so it doubles as the value display next to the dial.
/// </summary>
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
            Description = $"Sets {spec.Field} to {spec.FormatValue(spec.ResetValue)}; on a touch button shows the live value",
            ParameterTemplate = $"({{{channelParam}}})",
            Parameters = ChannelParameter(spec)
        };
        vm.RegisterCommand(spec.ResetName);
    }

    public override CommandDescriptor Descriptor { get; }

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    protected override Task Run(CommandContext ctx) => Apply(ctx, (_, _, _) => Spec.ResetValue);

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
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

internal static class AdjustmentCommands
{
    /// <summary>The Down / Up / Reset triplet for one spec.</summary>
    public static IEnumerable<IPluginCommand> Create(AdjustmentSpec spec, VoicemeeterService vm, IPluginLogger log)
    {
        yield return new AdjustmentStepCommand(spec, up: false, vm, log);
        yield return new AdjustmentStepCommand(spec, up: true, vm, log);
        yield return new AdjustmentResetCommand(spec, vm, log);
    }
}
