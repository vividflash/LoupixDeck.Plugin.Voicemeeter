using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Remote;

internal enum ConnectionState
{
    /// <summary>VoicemeeterRemote64.dll not found or not loadable.</summary>
    NotInstalled,

    /// <summary>VBVMR_Login returned -1 (no client).</summary>
    LoginFailed,

    /// <summary>Logged in, Voicemeeter itself is not running (yet).</summary>
    Waiting,

    Connected
}

/// <summary>
/// Owns the Remote API session: login once at start, logout at dispose. A single background
/// loop polls VBVMR_IsParametersDirty (50 ms) while connected and VBVMR_GetVoicemeeterType
/// (1 s) while not, so Voicemeeter can start, stop and restart at any time.
///
/// Commands read through <see cref="TryGetFloat"/> / <see cref="GetLabel"/>: the first read of a
/// parameter registers it as watched for that command name; on every dirty tick all watched
/// parameters are re-read and the commands whose values changed get a
/// <c>RequestButtonRefresh</c>. All API calls are serialized by one lock.
/// </summary>
internal sealed class VoicemeeterService : IDisposable
{
    private const int DirtyFailuresBeforeDisconnect = 3;

    private readonly IVoicemeeterApi? _api;
    private readonly IPluginLogger _log;
    private readonly Action<string> _refreshCommand;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _offlineProbeInterval;
    private readonly object _sync = new();
    private readonly Dictionary<string, float> _floats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _watchers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _commands = new(StringComparer.Ordinal);
    private readonly HashSet<string> _stringParams = new(StringComparer.Ordinal);
    private readonly string _loadMessage;
    private readonly Dictionary<(int Type, int Channel), float> _levels = [];
    private readonly Dictionary<(int Type, int Channel), long> _levelLeases = [];
    private readonly List<Action> _levelSubscribers = [];
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _loggedIn;
    private int _dirtyFailures;
    private bool _disposed;

    public VoicemeeterService(IVoicemeeterApi? api, string loadMessage, IPluginLogger log, Action<string> refreshCommand,
        TimeSpan? pollInterval = null, TimeSpan? offlineProbeInterval = null)
    {
        _api = api;
        _loadMessage = loadMessage;
        _log = log;
        _refreshCommand = refreshCommand;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(50);
        _offlineProbeInterval = offlineProbeInterval ?? TimeSpan.FromSeconds(1);
        State = api == null ? ConnectionState.NotInstalled : ConnectionState.Waiting;
    }

    public ConnectionState State { get; private set; }

    /// <summary>Edition of the running Voicemeeter; Unknown while not connected.</summary>
    public Edition Edition { get; private set; }

    /// <summary>Edition guessed from the installer name; used for menus before Voicemeeter runs.</summary>
    public Edition InstalledEdition { get; set; }

    /// <summary>Edition to build menus for: running edition, else installed, else Potato (largest).</summary>
    public Edition MenuEdition => Edition != Edition.Unknown ? Edition
        : InstalledEdition != Edition.Unknown ? InstalledEdition : Edition.Potato;

    public bool IsConnected => State == ConnectionState.Connected;

    /// <summary>Raised (outside the lock) whenever <see cref="State"/> or <see cref="Edition"/> changes.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// Raised (outside the lock) with a command name whenever that command's buttons are asked to
    /// redraw: one of its watched values changed, or the connection did. For views the host does
    /// not refresh by command name (an open folder).
    /// </summary>
    public event Action<string>? CommandRefreshed;

    public string Status => State switch
    {
        ConnectionState.NotInstalled => _loadMessage,
        ConnectionState.LoginFailed => "Could not log in to the Voicemeeter Remote API.",
        ConnectionState.Waiting => "Waiting for Voicemeeter to start.",
        ConnectionState.Connected => $"Connected to {EditionInfo.DisplayName(Edition)}.",
        _ => string.Empty
    };

    /// <summary>Logs in and (optionally) starts the poll loop. Tests pass runLoop=false and call <see cref="PollOnce"/>.</summary>
    public void Start(bool runLoop = true)
    {
        if (_api == null)
        {
            _log.Warn($"Voicemeeter: {_loadMessage}");
            return;
        }

        int rc;
        lock (_sync)
        {
            rc = Safe(() => _api.Login(), -1);
            _loggedIn = rc is 0 or 1 or -2;
            if (!_loggedIn) State = ConnectionState.LoginFailed;
        }

        _log.Info($"Voicemeeter: login returned {rc} ({LoginText(rc)})");
        if (!_loggedIn) return;

        PollOnce();
        if (!runLoop) return;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _loop = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch (Exception ex)
                {
                    _log.Error("Voicemeeter: poll failed", ex);
                }

                try
                {
                    await Task.Delay(IsConnected ? _pollInterval : _offlineProbeInterval, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, ct);
    }

    private static string LoginText(int rc) => rc switch
    {
        0 => "ok",
        1 => "ok, Voicemeeter not running",
        -1 => "no client",
        -2 => "already logged in",
        _ => "unexpected"
    };

    /// <summary>One poll tick: connect detection while offline, dirty check + re-read while online.</summary>
    internal void PollOnce()
    {
        if (_api == null || !_loggedIn || _disposed) return;

        HashSet<string>? toRefresh = null;
        var stateChanged = false;
        string? logLine = null;

        lock (_sync)
        {
            if (State != ConnectionState.Connected)
            {
                var type = Safe(() => _api.GetVoicemeeterType(out var t) == 0 ? t : 0, 0);
                var edition = EditionInfo.FromApiType(type);
                if (edition != Edition.Unknown)
                {
                    Edition = edition;
                    State = ConnectionState.Connected;
                    _dirtyFailures = 0;
                    Safe(() => _api.IsParametersDirty(), 0); // first call syncs the client's view
                    ReadAllWatched();
                    toRefresh = new HashSet<string>(_commands, StringComparer.Ordinal);
                    stateChanged = true;
                    logLine = $"Voicemeeter: connected to {EditionInfo.DisplayName(edition)}";
                }
            }
            else
            {
                var dirty = Safe(() => _api.IsParametersDirty(), -1);
                if (dirty < 0)
                {
                    _dirtyFailures++;
                    if (_dirtyFailures >= DirtyFailuresBeforeDisconnect)
                    {
                        State = ConnectionState.Waiting;
                        Edition = Edition.Unknown;
                        _floats.Clear();
                        _strings.Clear();
                        toRefresh = new HashSet<string>(_commands, StringComparer.Ordinal);
                        stateChanged = true;
                        logLine = $"Voicemeeter: connection lost (dirty poll returned {dirty}), waiting for Voicemeeter";
                    }
                }
                else
                {
                    _dirtyFailures = 0;
                    if (dirty > 0) toRefresh = ReadAllWatched();
                }
            }
        }

        if (logLine != null) _log.Info(logLine);
        if (stateChanged) RaiseStateChanged();
        Refresh(toRefresh);
        PollLevels();
    }

    /// <summary>Re-reads every watched parameter; returns the command names whose values changed. Caller holds the lock.</summary>
    private HashSet<string> ReadAllWatched()
    {
        var changed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (param, commands) in _watchers)
        {
            if (_stringParams.Contains(param))
            {
                var ok = Safe(() => _api!.GetString(param, out var s) == 0 ? s : null, null);
                if (ok == null) continue;
                if (!_strings.TryGetValue(param, out var old) || old != ok)
                {
                    _strings[param] = ok;
                    changed.UnionWith(commands);
                }
            }
            else
            {
                float v = 0;
                var rc = Safe(() => _api!.GetFloat(param, out v), -1);
                if (rc != 0) continue;
                if (!_floats.TryGetValue(param, out var old) || old != v)
                {
                    _floats[param] = v;
                    changed.UnionWith(commands);
                }
            }
        }

        return changed;
    }

    /// <summary>Makes sure connection changes redraw this command even before it has read anything.</summary>
    public void RegisterCommand(string commandName)
    {
        lock (_sync) _commands.Add(commandName);
    }

    private void Watch(string param, string commandName, bool isString = false)
    {
        _commands.Add(commandName);
        if (isString) _stringParams.Add(param);
        if (!_watchers.TryGetValue(param, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            _watchers[param] = set;
        }

        set.Add(commandName);
    }

    /// <summary>
    /// Current value of a float parameter (cached, else read live) and registers it as watched for
    /// <paramref name="commandName"/>. False when not connected or the read failed.
    /// </summary>
    public bool TryGetFloat(string param, string commandName, out float value)
    {
        lock (_sync)
        {
            Watch(param, commandName);
            if (_floats.TryGetValue(param, out value)) return true;
            if (!IsConnected || _api == null) return false;
            float v = 0;
            var rc = Safe(() => _api.GetFloat(param, out v), -1);
            if (rc != 0) return false;
            _floats[param] = v;
            value = v;
            return true;
        }
    }

    /// <summary>Strip/bus label as set in Voicemeeter, or <see cref="EditionInfo.DefaultLabel"/> when empty.</summary>
    public string GetLabel(Channel channel, string commandName)
    {
        var param = channel.LabelParam;
        lock (_sync)
        {
            Watch(param, commandName, isString: true);
            if (!_strings.TryGetValue(param, out var label) && IsConnected && _api != null)
            {
                label = Safe(() => _api.GetString(param, out var s) == 0 ? s : null, null);
                if (label != null) _strings[param] = label;
            }

            return string.IsNullOrWhiteSpace(label) ? EditionInfo.DefaultLabel(channel, Edition) : label.Trim();
        }
    }

    /// <summary>Label without registering a watch (menus); null when not connected or empty.</summary>
    public string? PeekLabel(Channel channel)
    {
        lock (_sync)
        {
            if (_strings.TryGetValue(channel.LabelParam, out var cached)) return string.IsNullOrWhiteSpace(cached) ? null : cached.Trim();
            if (!IsConnected || _api == null) return null;
            var label = Safe(() => _api.GetString(channel.LabelParam, out var s) == 0 ? s : null, null);
            return string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        }
    }

    /// <summary>Writes a float parameter. The only float write path of the plugin.</summary>
    public bool TrySetFloat(string param, float value, out string error)
    {
        HashSet<string>? toRefresh = null;
        lock (_sync)
        {
            if (!IsConnected || _api == null)
            {
                error = "Voicemeeter is not connected";
                return false;
            }

            var rc = Safe(() => _api.SetFloat(param, value), -1);
            if (rc != 0)
            {
                error = $"Voicemeeter refused {param} = {value} ({ResultText(rc)})";
                return false;
            }

            _floats[param] = value;
            if (_watchers.TryGetValue(param, out var set)) toRefresh = new HashSet<string>(set, StringComparer.Ordinal);
        }

        Refresh(toRefresh);
        error = string.Empty;
        return true;
    }

    /// <summary>Writes a string parameter (e.g. "Command.Load"). Cached string values are refreshed on the next dirty tick.</summary>
    public bool TrySetString(string param, string value, out string error)
    {
        lock (_sync)
        {
            if (!IsConnected || _api == null)
            {
                error = "Voicemeeter is not connected";
                return false;
            }

            var rc = Safe(() => _api.SetString(param, value), -1);
            error = rc == 0 ? string.Empty : $"Voicemeeter refused {param} ({ResultText(rc)})";
            return rc == 0;
        }
    }

    /// <summary>Runs a parameter script ("Strip[0].Mute=1; Bus[0].Gain=-6"). Values arrive via the next dirty tick.</summary>
    public bool TrySetParameters(string script, out string error)
    {
        lock (_sync)
        {
            if (!IsConnected || _api == null)
            {
                error = "Voicemeeter is not connected";
                return false;
            }

            var rc = Safe(() => _api.SetParameters(script), -1);
            error = rc == 0 ? string.Empty : $"Voicemeeter refused the script ({ResultText(rc)})";
            return rc == 0;
        }
    }

    /// <summary>How long a level channel keeps being polled after its last <see cref="TryGetLevel"/>.</summary>
    internal const long LevelLeaseMs = 1000;

    /// <summary>Millisecond clock for level leases; tests replace it.</summary>
    internal Func<long> Clock { get; set; } = () => Environment.TickCount64;

    /// <summary>
    /// True while any meter needs levels: a subscriber (side strip session) is registered or a
    /// channel was read within <see cref="LevelLeaseMs"/> (meter buttons, which the host only
    /// renders while visible). While false the poll tick reads no levels at all.
    /// </summary>
    public bool LevelPollingActive
    {
        get
        {
            lock (_sync) return _levelSubscribers.Count > 0 || _levelLeases.Count > 0;
        }
    }

    /// <summary>
    /// Level meter channel (type: 0 pre-fader in, 1 post-fader in, 2 post-mute in, 3 output) as
    /// linear amplitude. Served from the cache the shared level poller fills on every poll tick;
    /// the first read of a channel goes to the API and starts polling it (lease, see
    /// <see cref="LevelPollingActive"/>).
    /// </summary>
    public bool TryGetLevel(int type, int channel, out float value)
    {
        lock (_sync)
        {
            value = 0;
            if (!IsConnected || _api == null) return false;
            var key = (type, channel);
            _levelLeases[key] = Clock();
            if (_levels.TryGetValue(key, out value)) return true;
            float v = 0;
            var rc = Safe(() => _api.GetLevel(type, channel, out v), -1);
            if (rc != 0) return false;
            _levels[key] = v;
            value = v;
            return true;
        }
    }

    /// <summary>Calls <paramref name="onTick"/> after every level poll tick until disposed; keeps the poller running.</summary>
    public IDisposable SubscribeLevels(Action onTick)
    {
        lock (_sync) _levelSubscribers.Add(onTick);
        return new Unsubscriber(() =>
        {
            lock (_sync) _levelSubscribers.Remove(onTick);
        });
    }

    /// <summary>The shared level poller: re-reads every leased channel, drops expired leases, notifies subscribers.</summary>
    private void PollLevels()
    {
        Action[] subscribers;
        lock (_sync)
        {
            if (_levelLeases.Count == 0 && _levelSubscribers.Count == 0) return;
            if (!IsConnected || _api == null)
            {
                // Subscribers are still ticked while offline, so a side strip blanks its bands.
                _levels.Clear();
                _levelLeases.Clear();
            }
            else
            {
                var now = Clock();
                List<(int, int)>? expired = null;
                foreach (var (key, stamp) in _levelLeases)
                {
                    if (now - stamp > LevelLeaseMs)
                    {
                        (expired ??= []).Add(key);
                        continue;
                    }

                    float v = 0;
                    if (Safe(() => _api.GetLevel(key.Type, key.Channel, out v), -1) == 0) _levels[key] = v;
                    else _levels.Remove(key);
                }

                if (expired != null)
                {
                    foreach (var key in expired)
                    {
                        _levelLeases.Remove(key);
                        _levels.Remove(key);
                    }
                }
            }

            subscribers = _levelSubscribers.ToArray();
        }

        foreach (var onTick in subscribers)
        {
            try
            {
                onTick();
            }
            catch (Exception ex)
            {
                _log.Error("Voicemeeter: level subscriber failed", ex);
            }
        }
    }

    private sealed class Unsubscriber(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }

    internal static string ResultText(int rc) => rc switch
    {
        -1 => "error",
        -2 => "Voicemeeter not running",
        -3 => "unknown parameter",
        -5 => "structure mismatch",
        _ => $"code {rc}"
    };

    private void Refresh(HashSet<string>? commands)
    {
        if (commands == null) return;
        foreach (var name in commands)
        {
            try
            {
                _refreshCommand(name);
                CommandRefreshed?.Invoke(name);
            }
            catch (Exception ex)
            {
                _log.Error($"Voicemeeter: refresh of {name} failed", ex);
            }
        }
    }

    private void RaiseStateChanged()
    {
        try
        {
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _log.Error("Voicemeeter: StateChanged handler failed", ex);
        }
    }

    private T Safe<T>(Func<T> call, T fallback)
    {
        try
        {
            return call();
        }
        catch (Exception ex)
        {
            _log.Error("Voicemeeter: Remote API call threw", ex);
            return fallback;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // loop exits via cancellation
        }

        _cts?.Dispose();
        lock (_sync)
        {
            if (_api != null)
            {
                if (_loggedIn) Safe(() => _api.Logout(), 0);
                _loggedIn = false;
                Safe(() => { _api.Dispose(); return 0; }, 0);
            }

            State = _api == null ? ConnectionState.NotInstalled : ConnectionState.Waiting;
        }
    }
}
