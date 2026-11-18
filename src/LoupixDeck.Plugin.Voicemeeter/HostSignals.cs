using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter;

/// <summary>
/// Tells the host about the connection: <see cref="IPluginHost.SetAvailable"/> (buttons dimmed and
/// marked while Voicemeeter is not connected) and <see cref="IPluginHost.RequestMenuRefresh"/> when
/// the menu inputs change. Both calls are made only on a change.
/// </summary>
internal sealed class HostSignals : IDisposable
{
    private readonly IPluginHost _host;
    private readonly VoicemeeterService _vm;
    private readonly object _gate = new();
    private bool? _available;

    /// <summary>Subscribes to the service; create it before <see cref="VoicemeeterService.Start"/>.</summary>
    public HostSignals(IPluginHost host, VoicemeeterService vm)
    {
        _host = host;
        _vm = vm;
        _vm.StateChanged += ReportAvailability;
        _vm.MenuChanged += _host.RequestMenuRefresh;
    }

    /// <summary>
    /// Reports the current connection state if it differs from the last report. Call once after
    /// <see cref="VoicemeeterService.Start"/>: the first connection attempt has run by then, so a
    /// running Voicemeeter never shows the buttons dimmed at plugin start.
    /// </summary>
    public void ReportAvailability()
    {
        lock (_gate)
        {
            var available = _vm.IsConnected;
            if (_available == available) return;
            _available = available;
            _host.SetAvailable(available);
        }
    }

    public void Dispose()
    {
        _vm.StateChanged -= ReportAvailability;
        _vm.MenuChanged -= _host.RequestMenuRefresh;
    }
}
