using LoupixDeck.Plugin.Voicemeeter.Actions;
using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter;

/// <summary>
/// Entry point. Loads VoicemeeterRemote64.dll, logs in once (Initialize) and out once (Shutdown).
/// Without Voicemeeter the plugin still loads; commands draw "not installed" / "offline" and the
/// settings page shows the reason.
/// </summary>
public sealed class VoicemeeterPlugin : LoupixPlugin, IPluginSettingsPage, IMenuContributor, IPluginRequirements
{
    private IPluginHost? _host;
    private VoicemeeterService? _vm;
    private List<IPluginCommand> _commands = [];
    private List<ISideStripProvider> _sideStrips = [];

    public override PluginMetadata Metadata { get; } = new()
    {
        Id = "voicemeeter",
        Name = "Voicemeeter",
        Version = new Version(1, 2, 1),
        SdkVersion = new Version(1, 28, 0),
        Author = "vividflash",
        Description = "Mute and gain controls for Voicemeeter, Banana and Potato strips and buses."
    };

    public override void Initialize(IPluginHost host)
    {
        _host = host;
        Localization.SetHost(host);
        IVoicemeeterApi? api = null;
        string message;
        var installed = Edition.Unknown;
        if (OperatingSystem.IsWindows())
        {
            api = NativeVoicemeeterApi.TryLoad(out message);
            installed = DllLocator.EditionFromInstaller(
                DllLocator.ReadRegistry(DllLocator.UninstallKey) ?? DllLocator.ReadRegistry(DllLocator.UninstallKey32));
        }
        else
        {
            message = "Voicemeeter is Windows only.";
        }

        host.Logger.Info($"Voicemeeter: {message}");
        _vm = new VoicemeeterService(api, message, host.Logger, RequestRefresh) { InstalledEdition = installed };
        _commands = BuildCommands(_vm, host.Logger);
        _sideStrips = Levels.CreateSideStrips(_vm, host.Logger, host.Settings).ToList();
        _vm.Start();
    }

    /// <summary>The single registration point: one list per action family (see docs/PORTING.md).</summary>
    internal static List<IPluginCommand> BuildCommands(VoicemeeterService vm, IPluginLogger log)
    {
        var commands = new List<IPluginCommand>();
        commands.AddRange(Toggles.All.Select(spec => new ToggleCommand(spec, vm, log)));
        commands.AddRange(Adjustments.All.SelectMany(spec => AdjustmentCommands.Create(spec, vm, log)));
        commands.AddRange(Globals.Create(vm, log));
        commands.AddRange(Levels.Create(vm, log));
        return commands;
    }

    private void RequestRefresh(string commandName) => _host?.RequestButtonRefresh(commandName);

    public override IEnumerable<IPluginCommand> GetCommands() => _commands;

    public override IEnumerable<ISideStripProvider> GetSideStripProviders() => _sideStrips;

    public override IEnumerable<DialPresetDescriptor> GetDialPresets() =>
        _vm is { State: not ConnectionState.NotInstalled } vm ? DialPresets.Build(vm.MenuEdition, vm.PeekLabel) : [];

    public override IEnumerable<CommandMigration> GetCommandMigrations() =>
        Adjustments.All.SelectMany(AdjustmentCommands.Migrations).Concat(RawAdjustmentCommands.Migrations());

    public override IReadOnlyList<CommandGroupDescriptor> GetCommandGroups() =>
    [
        new CommandGroupDescriptor
        {
            Group = VmCommandBase.Group,
            Description = "Voicemeeter strips and buses",
            Section = CommandGroupSection.Plugins
        }
    ];

    public Task<IReadOnlyList<MenuNode>> GetMenuNodes(ButtonTargets target)
    {
        var vm = _vm;
        if (vm == null || vm.State == ConnectionState.NotInstalled)
            return Task.FromResult<IReadOnlyList<MenuNode>>([]);

        var nodes = MenuBuilder.Build(vm.MenuEdition, Toggles.All, Adjustments.All,
            vm.PeekLabel, Localization.Tr, touch: target.HasFlag(ButtonTargets.TouchButton));
        return Task.FromResult(nodes);
    }

    public IReadOnlyList<PluginRequirement> GetRequirements() => _vm == null ? [] : BuildRequirements(_vm.State);

    /// <summary>
    /// Only the install is a requirement. Voicemeeter not running (yet) is normal: the service
    /// keeps probing and connects whenever it starts, so it is not reported.
    /// </summary>
    internal static IReadOnlyList<PluginRequirement> BuildRequirements(ConnectionState state)
    {
        var installed = state != ConnectionState.NotInstalled;
        return
        [
            new PluginRequirement
            {
                Id = "voicemeeter-installed",
                Name = "Voicemeeter",
                IsMet = installed,
                Message = installed ? null
                    : OperatingSystem.IsWindows() ? $"Voicemeeter is not installed ({DllLocator.DllName} not found)." : "Voicemeeter is Windows only.",
                InstallHint = installed || !OperatingSystem.IsWindows() ? null : "Download Voicemeeter from vb-audio.com/Voicemeeter and restart LoupixDeck."
            }
        ];
    }

    public IReadOnlyList<PluginSettingDescriptor> SettingsSchema =>
    [
        new PluginSettingDescriptor
        {
            Key = "status",
            Label = _vm?.Status ?? "Not started.",
            Kind = PluginSettingKind.Heading,
            Description = "Connection to the Voicemeeter Remote API."
        },
        .. Levels.Settings
    ];

    public IReadOnlyList<PluginSettingAction> SettingsActions => [];

    public void OnSettingsSaved()
    {
    }

    public override void Shutdown()
    {
        _vm?.Dispose();
        _vm = null;
    }
}
