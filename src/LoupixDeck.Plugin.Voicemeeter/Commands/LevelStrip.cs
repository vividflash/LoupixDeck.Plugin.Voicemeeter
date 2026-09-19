using System.Text.RegularExpressions;
using LoupixDeck.Plugin.Voicemeeter.Actions;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>What one strip band meters: a strip or bus parameter text, resolved per tick against the running edition.</summary>
internal readonly record struct LevelSource(ChannelKind Kind, string ChannelText, LevelType Type, string Label);

/// <summary>
/// Finds the Voicemeeter channel a dial controls from its bound commands (right turn, left turn,
/// press): any adjustment (Voicemeeter.StripGainUp(2,1)), toggle (Voicemeeter.BusMute(A1)) or
/// level meter command. Strips meter post-fader, buses output, a Level command its own type.
/// </summary>
internal static partial class DialChannelParser
{
    [GeneratedRegex(@"Voicemeeter\.(\w+)\(([^)]*)\)")]
    private static partial Regex CommandPattern();

    public static LevelSource? FromRotary(SideStripRotary rotary)
    {
        var label = rotary.Label?.Trim() ?? string.Empty;
        foreach (var command in new[] { rotary.RightCommand, rotary.LeftCommand, rotary.PressCommand })
        {
            if (string.IsNullOrEmpty(command)) continue;
            foreach (Match m in CommandPattern().Matches(command))
            {
                var name = VmCommandBase.Prefix + m.Groups[1].Value;
                var args = m.Groups[2].Value.Split(',');
                var channel = args[0].Trim();
                if (channel.Length == 0) continue;
                if (name == LevelCommand.CommandName)
                {
                    if (!LevelMap.TryParseType(args.Length > 1 ? args[1] : null, out var type)) continue;
                    return new LevelSource(LevelMap.KindFor(type), channel, type, label);
                }

                var kind = KindOf(name);
                if (kind != null) return new LevelSource(kind.Value, channel, DefaultType(kind.Value), label);
            }
        }

        return null;
    }

    private static ChannelKind? KindOf(string commandName)
    {
        foreach (var spec in Adjustments.All)
        {
            if (commandName == spec.UpName || commandName == spec.DownName || commandName == spec.ResetName) return spec.Kind;
        }

        foreach (var spec in Toggles.All)
        {
            if (commandName == spec.CommandName) return spec.Kind;
        }

        return null;
    }

    public static LevelType DefaultType(ChannelKind kind) => kind == ChannelKind.Bus ? LevelType.Output : LevelType.PostFaderInput;

    /// <summary>Fallback list "1,A1,B1": numbers are strips, A/B names are buses.</summary>
    public static List<LevelSource> ParseFallback(string? text)
    {
        var list = new List<LevelSource>();
        foreach (var raw in (text ?? string.Empty).Split(',', ';', ' '))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;
            var kind = char.IsLetter(token[0]) ? ChannelKind.Bus : ChannelKind.Strip;
            list.Add(new LevelSource(kind, token, DefaultType(kind), string.Empty));
        }

        return list;
    }
}

/// <summary>
/// Side strip with one band per adjacent dial: channel name, live level bar (green/yellow/red,
/// white marker = gain on the same -60..+12 dB scale) and the gain value or "muted". Meters the
/// channels the dials control; when no dial is bound to a Voicemeeter command, meters the fixed
/// list from the plugin setting <see cref="FallbackKey"/>.
/// </summary>
internal sealed class LevelStripProvider(VoicemeeterService vm, IPluginSettings settings, IPluginLogger log)
    : ISideStripProvider, ISegmentStripProvider
{
    public const string FallbackKey = "levels.stripChannels";
    public const string DefaultFallback = "1,A1,B1";

    public string Id => "voicemeeter.levels";
    public string Title => "Voicemeeter Levels";

    public ISideStripSession CreateSession(SideStripContext context) => new LevelStripSession(vm, settings, log, context);
}

internal sealed class LevelStripSession : ISideStripSession, ISegmentStripSession
{
    /// <summary>Watch owner for the gain/mute/label reads (no button carries this name).</summary>
    internal const string WatchName = VmCommandBase.Prefix + "LevelStrip";

    internal readonly record struct Band(bool Has, string Name, float Db, float? Gain, bool Muted)
    {
        public static readonly Band Empty = new(false, string.Empty, float.NegativeInfinity, null, false);
    }

    private const int Edge = 4;
    private const int BarH = 14;
    private const float FontSize = 12f;
    private static readonly PluginColor Background = new(18, 18, 18);

    private readonly VoicemeeterService _vm;
    private readonly IPluginSettings _settings;
    private readonly IPluginLogger _log;
    private readonly SideStripContext _context;
    private readonly List<LevelSource?> _bound;
    private readonly IDisposable _subscription;
    private Band[] _bands = [];
    private int _signature;
    private bool _disposed;

    public LevelStripSession(VoicemeeterService vm, IPluginSettings settings, IPluginLogger log, SideStripContext context)
    {
        _vm = vm;
        _settings = settings;
        _log = log;
        _context = context;
        _bound = context.Rotaries.Select(DialChannelParser.FromRotary).ToList();
        Update();
        _subscription = vm.SubscribeLevels(OnLevelTick);
    }

    public event EventHandler? StripChanged;

    /// <summary>Bands as last computed (one per dial, or per fallback entry when no dial is bound).</summary>
    internal IReadOnlyList<Band> Bands => _bands;

    /// <summary>True when the bands come from the dials, false when the fallback list is used.</summary>
    internal bool UsesDials => _bound.Any(b => b != null);

    /// <summary>The sources metered right now.</summary>
    internal IReadOnlyList<LevelSource?> Sources()
    {
        if (UsesDials) return _bound;
        var slots = Math.Max(1, _context.Rotaries.Count);
        return DialChannelParser.ParseFallback(_settings.Get(LevelStripProvider.FallbackKey, LevelStripProvider.DefaultFallback))
            .Take(slots).Select(s => (LevelSource?)s).ToList();
    }

    private void OnLevelTick()
    {
        if (_disposed) return;
        if (Update()) StripChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Re-reads every band; true when what the strip shows changed.</summary>
    internal bool Update()
    {
        var sources = Sources();
        var bands = new Band[sources.Count];
        var sig = new HashCode();
        for (var i = 0; i < sources.Count; i++)
        {
            bands[i] = Read(sources[i]);
            var b = bands[i];
            sig.Add(b.Has);
            sig.Add(b.Name);
            sig.Add(b.Muted);
            sig.Add(b.Gain is { } g ? (int)MathF.Round(g * 10) : int.MinValue);
            sig.Add(MeterRender.Pixels(LevelMap.Fraction(b.Db), 60));
        }

        _bands = bands;
        var hash = sig.ToHashCode();
        if (hash == _signature) return false;
        _signature = hash;
        return true;
    }

    private Band Read(LevelSource? source)
    {
        if (source is not { } s || !_vm.IsConnected) return Band.Empty;
        if (!VmParam.TryResolve(s.Kind, s.ChannelText, _vm.Edition, out var channel, out _)) return Band.Empty;
        var name = s.Label.Length > 0 ? s.Label : _vm.GetLabel(channel, WatchName);
        var db = LevelReader.TryPeak(_vm, s.Type, channel, out var peak) ? LevelMap.ToDb(peak) : float.NegativeInfinity;
        float? gain = _vm.TryGetFloat(channel.Param("Gain"), WatchName, out var g) ? g : null;
        var muted = _vm.TryGetFloat(channel.Param("Mute"), WatchName, out var m) && m >= 0.5f;
        return new Band(true, name, db, gain, muted);
    }

    public bool RenderStrip(IRenderCanvas canvas)
    {
        try
        {
            var bands = _bands;
            if (bands.Length == 0 || bands.All(b => !b.Has)) return false;
            canvas.Clear(Background);
            var bandH = (canvas.Height - 2f * Edge) / bands.Length;
            for (var i = 0; i < bands.Length; i++)
                DrawBand(canvas, bands[i], (int)MathF.Round(Edge + i * bandH), (int)MathF.Round(bandH));
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Voicemeeter: level strip render failed", ex);
            return false;
        }
    }

    public bool RenderSegment(int rotaryIndex, IRenderCanvas canvas)
    {
        try
        {
            var bands = _bands;
            if (rotaryIndex < 0 || rotaryIndex >= bands.Length || !bands[rotaryIndex].Has) return false;
            DrawBand(canvas, bands[rotaryIndex], 0, canvas.Height);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Voicemeeter: level strip render failed", ex);
            return false;
        }
    }

    /// <summary>Name on top, level bar with gain marker, gain value (or "muted") below; centred in the band.</summary>
    private static void DrawBand(IRenderCanvas c, Band band, int top, int height)
    {
        const int side = 8;
        const int rowH = 12;
        const int gap = 7;
        var w = c.Width - 2 * side;
        var groupTop = top + (height - (rowH + gap + BarH + gap + rowH)) / 2;
        var barTop = groupTop + rowH + gap;
        var valueTop = barTop + BarH + gap;

        if (!band.Has)
        {
            c.DrawText("–", 0, groupTop, c.Width, rowH, Palette.DimText, FontSize, TextHAlign.Center, TextVAlign.Middle);
            return;
        }

        c.DrawText(MeterRender.Fit(band.Name, c, FontSize, w), 0, groupTop, c.Width, rowH, Palette.Text, FontSize,
            TextHAlign.Center, TextVAlign.Middle, outlined: true, outlineColor: PluginColor.Black);
        MeterRender.Bar(c, side, barTop, w, BarH, band.Db, band.Muted, band.Gain);
        var (text, color) = band.Muted ? ("muted", MeterRender.Red)
            : band.Gain is { } g ? (ValueMath.Format(g, 1, "", true), Palette.Text)
            : ("", Palette.Text);
        if (text.Length > 0)
            c.DrawText(text, 0, valueTop, c.Width, rowH, color, FontSize, TextHAlign.Center, TextVAlign.Middle,
                outlined: true, outlineColor: PluginColor.Black);
    }

    public void OnStripTapped(int x, int y)
    {
        // Display only: the strip never changes Voicemeeter.
    }

    public void OnStripSwiped(StripSwipeDirection direction)
    {
        if (direction == StripSwipeDirection.Up) _context.RequestNextPage();
        else _context.RequestPreviousPage();
    }

    public void Dispose()
    {
        _disposed = true;
        _subscription.Dispose();
    }
}
