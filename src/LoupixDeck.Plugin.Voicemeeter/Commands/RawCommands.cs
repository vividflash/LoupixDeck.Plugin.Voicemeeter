using System.Globalization;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// Toggles a single API parameter, or runs a ";"-separated Voicemeeter script (original
/// RawCommand). A single parameter (no "=" and no ";") just inverts its current 0/1 value, like the
/// original's plain toggle. A script may contain "%toggle%" anywhere on the right of an "="; it is
/// replaced with the inverted current value of the instruction's target parameter (original
/// RawCommand.cs lines 205-261).
/// </summary>
/// <remarks>
/// LoupixDeck splits a command's positional parameters on every "," in the persisted string
/// (<c>CommandStringParser.GetParameters</c>), and the generic parameter editor does not escape
/// commas typed into a plugin's text fields (only two built-in commands, LaunchApp and OpenUrl,
/// special-case that via <c>CommandParameterEncoding</c>). Voicemeeter scripts legitimately contain
/// commas, so Api is declared LAST and <see cref="Fields"/> rejoins everything from that position
/// onward with "," — the same trick <c>VmLoadCommand</c> uses for file paths. This is lossless for
/// ordinary values; it only loses information for a segment that was empty or pure whitespace
/// between two commas, which <c>GetParameters</c> already trims/drops before the plugin ever sees it.
/// </remarks>
internal sealed class RawCommand : VmCommandBase, IDisplayImageCommand
{
    public RawCommand(VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
        Descriptor = new CommandDescriptor
        {
            CommandName = Prefix + "Raw",
            DisplayName = "Raw Command",
            Group = Group,
            Icon = CommandLooks.Icon("Raw"),
            ButtonLayout = CommandLooks.SelfDrawn,
            Description = "Toggles a Voicemeeter parameter, or runs a raw script; %toggle% inverts the current value",
            ParameterTemplate = "({Name},{OnColor},{OffColor},{Api})",
            Parameters =
            [
                new CommandParameter("Name", typeof(string)),
                new CommandParameter("OnColor", typeof(string)) { DefaultValue = "" },
                new CommandParameter("OffColor", typeof(string)) { DefaultValue = "" },
                new CommandParameter("Api", typeof(string))
            ]
        };
        vm.RegisterCommand(Descriptor.CommandName);
    }

    public override CommandDescriptor Descriptor { get; }

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    /// <summary>Splits the button's parameters into Name/OnColor/OffColor plus Api, which is
    /// everything from parameter index 3 onward rejoined with ",".</summary>
    private static (string Name, string OnColor, string OffColor, string Api) Fields(string[] p) =>
    (
        p.Length > 0 ? p[0] : string.Empty,
        p.Length > 1 ? p[1] : string.Empty,
        p.Length > 2 ? p[2] : string.Empty,
        p.Length > 3 ? string.Join(",", p[3..]).Trim() : string.Empty
    );

    protected override Task Run(CommandContext ctx)
    {
        if (!Vm.IsConnected)
        {
            Fail(ctx, Vm.Status, "offline");
            return Task.CompletedTask;
        }

        var (nameRaw, _, _, api) = Fields(ctx.Parameters);
        if (api.Length == 0)
        {
            Fail(ctx, "missing Api parameter");
            return Task.CompletedTask;
        }

        var label = !string.IsNullOrWhiteSpace(nameRaw) ? nameRaw : api;

        if (IsScript(api))
        {
            if (!TryBuildScript(api, out var script, out var buildError))
            {
                Fail(ctx, buildError);
                return Task.CompletedTask;
            }

            if (!Vm.TrySetParameters(script, out var setError)) Fail(ctx, setError, "Failed");
            else ShowOverlay(ctx, label);
            return Task.CompletedTask;
        }

        var on = Vm.TryGetFloat(api, Name, out var current) && current >= 0.5f;
        if (!Vm.TrySetFloat(api, on ? 0f : 1f, out var error)) Fail(ctx, error, "Failed");
        else ShowOverlay(ctx, $"{label} {(on ? Localization.Tr("off") : Localization.Tr("on"))}");
        return Task.CompletedTask;
    }

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        try
        {
            var (nameRaw, onColorRaw, offColorRaw, api) = Fields(ctx.Parameters);
            var name = !string.IsNullOrWhiteSpace(nameRaw) ? nameRaw : "Raw";

            if (!Vm.IsConnected || api.Length == 0) return RenderUnavailable(canvas, name);

            var onColor = ParseColor(onColorRaw, Palette.Active);
            var offColor = ParseColor(offColorRaw, Palette.Inactive);

            if (IsScript(api))
            {
                // A script has no single value to track; draw a static button (original: no state).
                Render.Toggle(canvas, name, Localization.Tr("script"), false, offColor, offColor);
                return true;
            }

            if (!Vm.TryGetFloat(api, Name, out var value)) return RenderUnavailable(canvas, name);
            Render.Toggle(canvas, name, api, value >= 0.5f, onColor, offColor);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"{Name}: render failed", ex);
            return false;
        }
    }

    private static bool IsScript(string api) => api.Contains('=') || api.Contains(';');

    /// <summary>Replaces "%toggle%" in every ";"-separated instruction with the inverted current
    /// value of that instruction's target parameter (text left of "="). False (with an error, no
    /// throw) when a %toggle% target cannot be read.</summary>
    private bool TryBuildScript(string api, out string script, out string error)
    {
        var parts = api.Split(';');
        for (var i = 0; i < parts.Length; i++)
        {
            var trimmed = parts[i].Trim();
            if (trimmed.Length == 0 || !trimmed.Contains("%toggle%"))
            {
                parts[i] = trimmed;
                continue;
            }

            var eq = trimmed.IndexOf('=');
            var target = (eq >= 0 ? trimmed[..eq] : trimmed).Trim();
            if (target.Length == 0 || !Vm.TryGetFloat(target, Name, out var current))
            {
                script = string.Empty;
                error = $"cannot toggle '{target}': not readable";
                return false;
            }

            parts[i] = trimmed.Replace("%toggle%", current >= 0.5f ? "0" : "1");
        }

        script = string.Join(";", parts);
        error = string.Empty;
        return true;
    }

    /// <summary>Parses "#rrggbb" / "#rgb"; falls back to <paramref name="fallback"/> on anything else.</summary>
    private static PluginColor ParseColor(string? hex, PluginColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var s = hex.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length == 3) s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        if (s.Length != 6) return fallback;
        if (!byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)) return fallback;
        if (!byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)) return fallback;
        if (!byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)) return fallback;
        return new PluginColor(r, g, b);
    }
}

/// <summary>
/// Shared resolution for the raw-adjustment commands (original RawAdjustment). Unlike family
/// B there is no fixed channel/spec: Api, Step, Min and Max all come from the command's own
/// parameters at run time.
/// </summary>
/// <remarks>
/// Same comma problem as <see cref="RawCommand"/> (see its remarks): Api is declared LAST, after
/// the three fixed numeric parameters, and everything from that position onward is rejoined with
/// "," so an Api value containing a comma survives the round trip through the host's positional
/// parameter splitter.
/// </remarks>
internal abstract class RawAdjustmentCommandBase : VmCommandBase
{
    protected RawAdjustmentCommandBase(VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
    }

    protected static List<CommandParameter> Parameters() =>
    [
        new CommandParameter("Step", typeof(double)) { DefaultValue = "1" },
        new CommandParameter("Min", typeof(double)) { DefaultValue = "0" },
        new CommandParameter("Max", typeof(double)) { DefaultValue = "10" },
        new CommandParameter("Api", typeof(string))
    ];

    protected bool TryResolve(CommandContext ctx, out string api, out float step, out float min, out float max, out string error)
    {
        step = ValueMath.ParseStep(ctx.Parameters.Length > 0 ? ctx.Parameters[0] : null, 1f);
        min = ParseFloat(ctx.Parameters, 1, 0f);
        max = ParseFloat(ctx.Parameters, 2, 10f);
        api = ctx.Parameters.Length > 3 ? string.Join(",", ctx.Parameters[3..]).Trim() : string.Empty;

        if (api.Length == 0)
        {
            error = "missing Api parameter";
            return false;
        }

        if (max <= min)
        {
            error = $"Max ({max}) must be greater than Min ({min})";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static float ParseFloat(string[] parameters, int index, float fallback) =>
        parameters.Length > index && float.TryParse(parameters[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && float.IsFinite(v)
            ? v
            : fallback;

    /// <summary>Resolves, computes and writes the next value; flashes it on the dial's slot.</summary>
    protected Task Apply(CommandContext ctx, Func<float, float, float, float, float> compute)
    {
        if (!Vm.IsConnected)
        {
            Fail(ctx, Vm.Status, "offline");
            return Task.CompletedTask;
        }

        if (!TryResolve(ctx, out var api, out var step, out var min, out var max, out var error))
        {
            Fail(ctx, error);
            return Task.CompletedTask;
        }

        if (!Vm.TryGetFloat(api, Name, out var current))
        {
            Fail(ctx, $"could not read {api}", "Failed");
            return Task.CompletedTask;
        }

        var next = ValueMath.Clamp(compute(current, step, min, max), min, max);
        if (next != current && !Vm.TrySetFloat(api, next, out error))
        {
            Fail(ctx, error, "Failed");
            return Task.CompletedTask;
        }

        ShowOverlay(ctx, $"{api} {ValueMath.Format(next, 2, string.Empty, signed: false)}");
        return Task.CompletedTask;
    }

    /// <summary>Live value bar for a touch button.</summary>
    protected bool RenderBar(CommandContext ctx, IRenderCanvas canvas)
    {
        try
        {
            if (!Vm.IsConnected || !TryResolve(ctx, out var api, out _, out var min, out var max, out _))
                return RenderUnavailable(canvas, "Raw");

            if (!Vm.TryGetFloat(api, Name, out var value)) return RenderUnavailable(canvas, api);

            Render.Bar(canvas, "Raw", api, ValueMath.Format(value, 2, string.Empty, signed: false),
                ValueMath.Fraction(value, min, max), Palette.Active);
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
/// The raw knob: Voicemeeter.RawAdjustment(1,0,10,Strip[0].Comp). On a dial a turn changes the
/// parameter by Step per tick and a press sets 0 (clamped to [Min, Max]) — the original
/// RawAdjustment has no reset value of its own, so this mirrors family B's common default. On a
/// touch button it draws the live value bar and a press resets, like <see cref="AdjustmentCommand"/>.
/// </summary>
internal sealed class RawAdjustmentCommand : RawAdjustmentCommandBase, IAdjustmentCommand, IDisplayImageCommand
{
    public const string CommandName = Prefix + "RawAdjustment";

    public RawAdjustmentCommand(VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
        Descriptor = new CommandDescriptor
        {
            CommandName = CommandName,
            DisplayName = "Raw Adjustment",
            Group = Group,
            Icon = CommandLooks.Icon("RawAdjustment"),
            ButtonLayout = CommandLooks.SelfDrawn,
            Description = "Dial: turn changes any Voicemeeter parameter by Step, clamped to [Min, Max], press sets 0; " +
                          "on a touch button shows the live value",
            ParameterTemplate = "({Step},{Min},{Max},{Api})",
            Parameters = Parameters()
        };
        vm.RegisterCommand(CommandName);
    }

    public override CommandDescriptor Descriptor { get; }

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    /// <summary>Touch button, macro, CLI: no direction, so it acts like the dial press.</summary>
    protected override Task Run(CommandContext ctx) => Apply(ctx, (_, _, min, max) => ValueMath.Clamp(0f, min, max));

    public async Task ApplyAdjustment(CommandContext ctx, int ticks)
    {
        try
        {
            await Apply(ctx, (current, step, min, max) => ValueMath.Step(current, ticks, step, min, max)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error($"{Name}: failed", ex);
            ShowOverlay(ctx, Localization.Tr("Failed"));
        }
    }

    public Task ApplyReset(CommandContext ctx) => Execute(ctx);

    /// <summary>Dial indicator: position in [Min, Max] plus the formatted value; null when offline or unreadable.</summary>
    public AdjustmentValue? GetValue(CommandContext ctx)
    {
        try
        {
            if (!Vm.IsConnected || !TryResolve(ctx, out var api, out _, out var min, out var max, out _)) return null;
            if (!Vm.TryGetFloat(api, Name, out var value)) return null;
            return new AdjustmentValue(ValueMath.Fraction(value, min, max), ValueMath.Format(value, 2, string.Empty, signed: false));
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
/// Pre-1.2 dial turn: Voicemeeter.RawUp(1,0,10,Strip[0].Comp) / Voicemeeter.RawDown(...). Hidden
/// from the menu; stays registered for the same reasons as <see cref="AdjustmentStepCommand"/>.
/// </summary>
internal sealed class RawAdjustmentStepCommand : RawAdjustmentCommandBase
{
    private readonly int _direction;

    public RawAdjustmentStepCommand(bool up, VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
        _direction = up ? 1 : -1;
        Descriptor = new CommandDescriptor
        {
            CommandName = Prefix + "Raw" + (up ? "Up" : "Down"),
            DisplayName = $"Raw Adjustment {(up ? "Up" : "Down")}",
            Group = Group,
            Icon = CommandLooks.Icon("RawAdjustment"),
            ButtonLayout = CommandLooks.IconAndCaption,
            Description = $"{(up ? "Raises" : "Lowers")} any Voicemeeter parameter by Step, clamped to [Min, Max]",
            ParameterTemplate = "({Step},{Min},{Max},{Api})",
            Parameters = Parameters(),
            HiddenFromMenu = true
        };
    }

    public override CommandDescriptor Descriptor { get; }

    protected override Task Run(CommandContext ctx) =>
        Apply(ctx, (current, step, min, max) => ValueMath.Step(current, _direction, step, min, max));
}

/// <summary>Pre-1.2 dial press / value display: Voicemeeter.RawReset(1,0,10,Strip[0].Comp). Hidden, see <see cref="RawAdjustmentStepCommand"/>.</summary>
internal sealed class RawAdjustmentResetCommand : RawAdjustmentCommandBase, IDisplayImageCommand
{
    public RawAdjustmentResetCommand(VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
        Descriptor = new CommandDescriptor
        {
            CommandName = Prefix + "RawReset",
            DisplayName = "Raw Adjustment Reset",
            Group = Group,
            Icon = CommandLooks.Icon("RawAdjustment"),
            ButtonLayout = CommandLooks.SelfDrawn,
            Description = "Sets any Voicemeeter parameter to 0 (clamped to [Min, Max]); on a touch button shows the live value",
            ParameterTemplate = "({Step},{Min},{Max},{Api})",
            Parameters = Parameters(),
            HiddenFromMenu = true
        };
        vm.RegisterCommand(Descriptor.CommandName);
    }

    public override CommandDescriptor Descriptor { get; }

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    protected override Task Run(CommandContext ctx) => Apply(ctx, (_, _, min, max) => ValueMath.Clamp(0f, min, max));

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas) => RenderBar(ctx, canvas);
}

internal static class RawAdjustmentCommands
{
    /// <summary>The raw knob, then its hidden pre-1.2 Down / Up / Reset.</summary>
    public static IEnumerable<IPluginCommand> Create(VoicemeeterService vm, IPluginLogger log)
    {
        yield return new RawAdjustmentCommand(vm, log);
        yield return new RawAdjustmentStepCommand(up: false, vm, log);
        yield return new RawAdjustmentStepCommand(up: true, vm, log);
        yield return new RawAdjustmentResetCommand(vm, log);
    }

    /// <summary>
    /// Moves pre-1.2 raw dials onto the raw knob, same two shapes as
    /// <see cref="AdjustmentCommands.Migrations"/>. Ids are recorded in the user's config; never
    /// change them.
    /// </summary>
    public static IEnumerable<CommandMigration> Migrations()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Step"] = "{Step}",
            ["Min"] = "{Min}",
            ["Max"] = "{Max}",
            ["Api"] = "{Api}"
        };
        const string down = VmCommandBase.Prefix + "RawDown";
        const string up = VmCommandBase.Prefix + "RawUp";
        yield return new CommandMigration
        {
            Id = "RawAdjustment-dial",
            From = new Dictionary<RotaryAction, string>
            {
                [RotaryAction.CounterClockwise] = down,
                [RotaryAction.Clockwise] = up,
                [RotaryAction.Press] = VmCommandBase.Prefix + "RawReset"
            },
            To = RawAdjustmentCommand.CommandName,
            Parameters = parameters
        };
        yield return new CommandMigration
        {
            Id = "RawAdjustment-turn",
            From = new Dictionary<RotaryAction, string>
            {
                [RotaryAction.CounterClockwise] = down,
                [RotaryAction.Clockwise] = up
            },
            To = RawAdjustmentCommand.CommandName,
            Parameters = parameters
        };
    }
}
