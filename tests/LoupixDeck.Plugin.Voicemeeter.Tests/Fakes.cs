using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

/// <summary>In-memory Voicemeeter. Writes land in <see cref="Floats"/> and are recorded in <see cref="Writes"/>.</summary>
internal sealed class FakeVoicemeeterApi : IVoicemeeterApi
{
    public int LoginResult { get; set; }

    /// <summary>VBVMR_GetVoicemeeterType value; 0 = Voicemeeter not running.</summary>
    public int RunningType { get; set; } = 3;

    public bool Dirty { get; set; }
    public Dictionary<string, float> Floats { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Strings { get; } = new(StringComparer.Ordinal);
    public List<(string Name, float Value)> Writes { get; } = [];
    public List<(string Name, string Value)> StringWrites { get; } = [];
    public List<string> Scripts { get; } = [];
    public Dictionary<(int Type, int Channel), float> Levels { get; } = [];
    public int Logins { get; private set; }
    public int Logouts { get; private set; }
    public bool Disposed { get; private set; }

    private bool Running => RunningType != 0;

    public int Login()
    {
        Logins++;
        return LoginResult;
    }

    public int Logout()
    {
        Logouts++;
        return 0;
    }

    public int GetVoicemeeterType(out int type)
    {
        type = RunningType;
        return Running ? 0 : -2;
    }

    public int IsParametersDirty()
    {
        if (!Running) return -2;
        var d = Dirty ? 1 : 0;
        Dirty = false;
        return d;
    }

    public int GetFloat(string name, out float value)
    {
        value = 0;
        if (!Running) return -2;
        return Floats.TryGetValue(name, out value) ? 0 : -3;
    }

    public int GetString(string name, out string value)
    {
        value = string.Empty;
        if (!Running) return -2;
        if (Strings.TryGetValue(name, out var s)) value = s;
        return 0;
    }

    public int GetLevel(int type, int channel, out float value)
    {
        value = 0;
        if (!Running) return -2;
        return Levels.TryGetValue((type, channel), out value) ? 0 : -4;
    }

    public int SetFloat(string name, float value)
    {
        if (!Running) return -2;
        if (!Floats.ContainsKey(name)) return -3;
        Floats[name] = value;
        Writes.Add((name, value));
        return 0;
    }

    public int SetString(string name, string value)
    {
        if (!Running) return -2;
        StringWrites.Add((name, value));
        return 0;
    }

    public int SetParameters(string script)
    {
        if (!Running) return -2;
        Scripts.Add(script);
        return 0;
    }

    public void Dispose() => Disposed = true;

    /// <summary>Potato-like default state: all strips and buses at 0 dB, unmuted, no labels.</summary>
    public static FakeVoicemeeterApi Potato()
    {
        var api = new FakeVoicemeeterApi { RunningType = 3 };
        for (var i = 0; i < 8; i++)
        {
            foreach (var p in new[] { "Strip", "Bus" })
            {
                api.Floats[$"{p}[{i}].Mute"] = 0;
                api.Floats[$"{p}[{i}].Gain"] = 0;
                api.Strings[$"{p}[{i}].Label"] = string.Empty;
            }
        }

        return api;
    }
}

internal sealed class FakeLogger : IPluginLogger
{
    public List<string> Lines { get; } = [];
    public void Info(string message) => Lines.Add("I " + message);
    public void Warn(string message) => Lines.Add("W " + message);
    public void Error(string message, Exception? exception = null) => Lines.Add("E " + message + " " + exception?.Message);
}

internal sealed class FakeSettings : IPluginSettings
{
    private readonly Dictionary<string, object?> _values = [];
    public T? Get<T>(string key, T? defaultValue = default) => _values.TryGetValue(key, out var v) && v is T t ? t : defaultValue;
    public void Set<T>(string key, T value) => _values[key] = value;
    public bool Contains(string key) => _values.ContainsKey(key);
    public void Remove(string key) => _values.Remove(key);
    public IEnumerable<string> Keys => _values.Keys;
    public void Save() { }
}

internal sealed class FakeHost : IPluginHost
{
    public FakeLogger FakeLog { get; } = new();
    public List<string> Refreshes { get; } = [];
    public List<(int Slot, string Text)> Overlays { get; } = [];
    public IPluginLogger Logger => FakeLog;
    public IPluginSettings Settings { get; } = new FakeSettings();
    public string CurrentLanguage => "en";
    public Func<string, string>? Translate { get; set; }
    public string Tr(string key) => Translate?.Invoke(key) ?? key;
    public FolderGridInfo FolderGrid => new(5, 3, 0);
    public DeviceInfo? ActiveDevice => null;
    public bool IsInExclusiveMode => false;
    public void RequestButtonRefresh(string commandName) => Refreshes.Add(commandName);
    public void ExecuteCommand(string command) { }
    public void OpenFolder(IFolderProvider provider) { }
    public bool OpenBrowser(string url) => false;
    public void OverlayTouchText(int slot, string text, TimeSpan duration) => Overlays.Add((slot, text));
    public int GetTouchSlotForRotary(int rotaryIndex) => rotaryIndex + 10;
    public bool RequestExclusiveMode(IExclusiveModeProvider provider) => false;
    public void ReleaseExclusiveMode(IExclusiveModeProvider provider) { }
    public IFullDisplayRenderSession? RequestFullDisplayRenderer(IFullDisplayRenderer renderer) => null;
    public IReadOnlyList<string> GetButtonStates(string commandName) => [];
    public string? GetActiveButtonState(string commandName) => null;
    public bool SetActiveButtonState(string commandName, string stateNameOrId) => false;
}

/// <summary>Records draw calls; texts, background and fills are what tests assert on.</summary>
internal sealed class FakeCanvas : IRenderCanvas
{
    public int Width => 90;
    public int Height => 90;
    public PluginColor? Background { get; private set; }
    public List<string> Texts { get; } = [];
    public List<(int X, int Y, int W, int H, PluginColor Color)> Fills { get; } = [];

    public void Clear(PluginColor color)
    {
        Background = color;
        Texts.Clear();
        Fills.Clear();
    }

    public void FillRectangle(int x, int y, int width, int height, PluginColor color) => Fills.Add((x, y, width, height, color));
    public void DrawRectangle(int x, int y, int width, int height, int strokeWidth, PluginColor color) { }
    public void FillRoundedRectangle(int x, int y, int width, int height, int radius, PluginColor color) => Fills.Add((x, y, width, height, color));
    public void DrawRoundedRectangle(int x, int y, int width, int height, int radius, int strokeWidth, PluginColor color) { }
    public void FillCircle(int centerX, int centerY, int radius, PluginColor color) { }
    public void DrawCircle(int centerX, int centerY, int radius, int strokeWidth, PluginColor color) { }
    public void FillEllipse(int x, int y, int width, int height, PluginColor color) { }
    public void DrawEllipse(int x, int y, int width, int height, int strokeWidth, PluginColor color) { }
    public void DrawArc(int x, int y, int width, int height, float startAngle, float sweepAngle, int strokeWidth, PluginColor color) { }
    public void FillArc(int x, int y, int width, int height, float startAngle, float sweepAngle, PluginColor color) { }
    public void DrawLine(int x1, int y1, int x2, int y2, int strokeWidth, PluginColor color) { }

    public void DrawText(string text, int x, int y, int width, int height, PluginColor color, float fontSize,
        bool bold = false, bool italic = false, bool centered = true, bool outlined = false, PluginColor outlineColor = default) => Texts.Add(text);

    public void DrawText(string text, int x, int y, int width, int height, PluginColor color, float fontSize,
        TextHAlign hAlign, TextVAlign vAlign, bool bold = false, bool italic = false, bool outlined = false, PluginColor outlineColor = default) => Texts.Add(text);

    public float MeasureText(string text, float fontSize, bool bold = false, bool italic = false) => text.Length * fontSize / 2;
    public void DrawSymbol(string symbolId, int x, int y, int width, int height, PluginColor tint) { }
    public void DrawSymbol(string symbolId, int x, int y, int width, int height, SymbolStyle style) { }
    public void DrawImage(byte[] imageBytes, int x, int y, int width, int height) { }
    public void DrawImage(byte[] imageBytes, int x, int y, int width, int height, byte opacity, PluginColor tint = default) { }
    public void PushTransform() { }
    public void PopTransform() { }
    public void Translate(float dx, float dy) { }
    public void Rotate(float degrees) { }
    public void Scale(float sx, float sy) { }
}

/// <summary>Service + all registered commands wired to a fake API and host, without the poll thread.</summary>
internal sealed class Rig : IDisposable
{
    public FakeVoicemeeterApi Api { get; }
    public FakeHost Host { get; } = new();
    public VoicemeeterService Vm { get; }
    public List<IPluginCommand> Commands { get; }

    public Rig(FakeVoicemeeterApi? api = null, bool start = true)
    {
        Api = api ?? FakeVoicemeeterApi.Potato();
        Vm = new VoicemeeterService(Api, "fake", Host.Logger, Host.RequestButtonRefresh);
        Commands = VoicemeeterPlugin.BuildCommands(Vm, Host.Logger);
        if (start) Vm.Start(runLoop: false);
    }

    public IPluginCommand Command(string name) => Commands.Single(c => c.Descriptor.CommandName == name);

    public CommandContext Ctx(params string[] parameters) => new()
    {
        Parameters = parameters,
        Target = ButtonTargets.TouchButton,
        Host = Host
    };

    public CommandContext DialCtx(int rotary, params string[] parameters) => new()
    {
        Parameters = parameters,
        Target = ButtonTargets.RotaryEncoder,
        SourceIndex = rotary,
        Host = Host
    };

    public Task Run(string name, params string[] parameters) => Command(name).Execute(Ctx(parameters));

    /// <summary>Dial turn on rotary 0, as the host dispatches an IAdjustmentCommand.</summary>
    public Task Turn(string name, int ticks, params string[] parameters) =>
        ((IAdjustmentCommand)Command(name)).ApplyAdjustment(DialCtx(0, parameters), ticks);

    /// <summary>Dial press on rotary 0.</summary>
    public Task Press(string name, params string[] parameters) =>
        ((IAdjustmentCommand)Command(name)).ApplyReset(DialCtx(0, parameters));

    public FakeCanvas Render(string name, params string[] parameters)
    {
        var canvas = new FakeCanvas();
        Assert.True(((IDisplayImageCommand)Command(name)).RenderImage(Ctx(parameters), canvas));
        return canvas;
    }

    public void Dispose() => Vm.Dispose();
}
