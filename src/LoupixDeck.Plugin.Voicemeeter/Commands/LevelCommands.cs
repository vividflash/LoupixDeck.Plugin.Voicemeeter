using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>Reads the peak of a strip or bus: the loudest of its level channels (L/R, or 8 for virtual strips and buses).</summary>
internal static class LevelReader
{
    public static bool TryPeak(VoicemeeterService vm, LevelType type, Channel channel, out float peak)
    {
        peak = 0;
        var (first, count) = LevelMap.Range(type, channel, vm.Edition);
        var any = false;
        for (var c = first; c < first + count; c++)
        {
            if (!vm.TryGetLevel((int)type, c, out var v)) continue;
            any = true;
            if (v > peak) peak = v;
        }

        return any;
    }
}

/// <summary>Meter colours and drawings (the original drew one flat bar; zones added here).</summary>
internal static class MeterRender
{
    public static readonly PluginColor Green = Palette.Active;
    public static readonly PluginColor Yellow = new(222, 186, 62);
    public static readonly PluginColor Red = Palette.Danger;
    public static readonly PluginColor Track = new(44, 44, 44);
    public static readonly PluginColor Muted = new(110, 110, 110);

    /// <summary>Green below this level, yellow from here up to 0 dB, red at and above 0 dB.</summary>
    public const float YellowDb = -12f;

    public static PluginColor PeakColor(float db) => db >= 0 ? Red : db >= YellowDb ? Yellow : Green;

    /// <summary>Horizontal meter: track, then the fill split into green / yellow / red zones, a 0 dB tick.</summary>
    public static void Bar(IRenderCanvas c, int x, int y, int w, int h, float db, bool muted, float? markerDb = null)
    {
        c.FillRectangle(x, y, w, h, Track);
        var fill = Pixels(LevelMap.Fraction(db), w);
        if (fill > 0)
        {
            if (muted)
            {
                c.FillRectangle(x, y, fill, h, Muted);
            }
            else
            {
                var yellowAt = Pixels(LevelMap.Fraction(YellowDb), w);
                var redAt = Pixels(LevelMap.Fraction(0), w);
                c.FillRectangle(x, y, Math.Min(fill, yellowAt), h, Green);
                if (fill > yellowAt) c.FillRectangle(x + yellowAt, y, Math.Min(fill, redAt) - yellowAt, h, Yellow);
                if (fill > redAt) c.FillRectangle(x + redAt, y, fill - redAt, h, Red);
            }
        }

        var zero = x + Pixels(LevelMap.Fraction(0), w);
        c.DrawLine(zero, y, zero, y + h - 1, 1, Palette.DimText);
        if (markerDb is { } m)
        {
            var mx = x + Math.Clamp(Pixels(LevelMap.Fraction(m), w), 1, w - 2);
            c.FillRectangle(mx - 1, y - 2, 2, h + 4, Palette.Text);
        }
    }

    public static int Pixels(float fraction, int width) => (int)MathF.Round(width * Math.Clamp(fraction, 0f, 1f));

    /// <summary>Truncates with an ellipsis so the text fits <paramref name="maxWidth"/>.</summary>
    public static string Fit(string text, IRenderCanvas canvas, float fontSize, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || canvas.MeasureText(text, fontSize) <= maxWidth) return text;
        var trimmed = text;
        while (trimmed.Length > 1 && canvas.MeasureText(trimmed + "…", fontSize) > maxWidth) trimmed = trimmed[..^1];
        return trimmed + "…";
    }
}

/// <summary>
/// Original LevelsCommand: a live meter on a touch button. Voicemeeter.Level(channel,type):
/// channel is a strip number for the input types and a bus number or name (A1, B2) for Output;
/// type is PreFader, PostFader (default), PostMute or Output (or 0-3).
///
/// Animated at 12 fps: the host's image-poll path is clamped to 4 fps, too slow for a meter.
/// Each frame reads the shared level cache (no API call of its own) and returns a frame number
/// derived from what is drawn, so the host skips the device push while the picture is unchanged.
/// </summary>
internal sealed class LevelCommand : VmCommandBase, IAnimatedDisplayCommand
{
    public const string CommandName = Prefix + "Level";
    private const int BarX = 6;
    private const int BarH = 18;

    public LevelCommand(VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
        vm.RegisterCommand(CommandName);
    }

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = CommandName,
        DisplayName = "Level Meter",
        Group = Group,
        Icon = CommandLooks.Icon("Level"),
        ButtonLayout = CommandLooks.SelfDrawn,
        Description = "Live level of a strip (PreFader, PostFader, PostMute) or bus (Output), in dB",
        ParameterTemplate = "({Channel},{Type})",
        Parameters =
        [
            new CommandParameter("Channel", typeof(string)) { DefaultValue = "1" },
            new CommandParameter("Type", typeof(string)) { DefaultValue = "PostFader" }
        ]
    };

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    public int TargetFps => 12;

    /// <summary>A meter has no press action (the original was a display-only widget).</summary>
    protected override Task Run(CommandContext ctx) => Task.CompletedTask;

    internal bool TryResolve(CommandContext ctx, out LevelType type, out Channel channel, out string error)
    {
        channel = default;
        if (!LevelMap.TryParseType(ctx.Parameters.Length > 1 ? ctx.Parameters[1] : null, out type))
        {
            error = $"unknown level type '{ctx.Parameters[1]}' (PreFader, PostFader, PostMute, Output)";
            return false;
        }

        return TryChannel(ctx, LevelMap.KindFor(type), 0, out channel, out error);
    }

    public AnimationFrameInfo RenderAnimatedFrame(CommandContext ctx, IRenderCanvas canvas, AnimationFrameContext frame)
    {
        try
        {
            if (!Vm.IsConnected || !TryResolve(ctx, out var type, out var channel, out _))
            {
                RenderUnavailable(canvas, Localization.Tr("Level"));
                return AnimationFrameInfo.Frame(HashCode.Combine(Vm.State, "unavailable"));
            }

            var label = Vm.GetLabel(channel, Name);
            var db = LevelReader.TryPeak(Vm, type, channel, out var peak) ? LevelMap.ToDb(peak) : float.NegativeInfinity;
            var dbText = LevelMap.FormatDb(db);
            var barW = canvas.Width - 2 * BarX;
            Draw(canvas, label, LevelMap.ShortName(type), dbText, db);
            return AnimationFrameInfo.Frame(HashCode.Combine(label, type, dbText, MeterRender.Pixels(LevelMap.Fraction(db), barW)));
        }
        catch (Exception ex)
        {
            Log.Error($"{Name}: render failed", ex);
            return AnimationFrameInfo.Skip();
        }
    }

    private static void Draw(IRenderCanvas c, string label, string typeName, string dbText, float db)
    {
        c.Clear(PluginColor.Black);
        var w = c.Width;
        c.DrawText(label, 2, 2, w - 4, 20, Palette.Text, 13f, TextHAlign.Center, TextVAlign.Middle, bold: true);
        c.DrawText(typeName, 2, 22, w - 4, 14, Palette.DimText, 10f, TextHAlign.Center, TextVAlign.Middle);
        c.DrawText(dbText, 2, 37, w - 4, 22, db >= MeterRender.YellowDb ? MeterRender.PeakColor(db) : Palette.Text, 15f,
            TextHAlign.Center, TextVAlign.Middle, bold: true);
        MeterRender.Bar(c, BarX, c.Height - BarH - 8, w - 2 * BarX, BarH, db, muted: false);
    }
}
