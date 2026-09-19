using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// Channel-less "press = one Remote call" commands (original SingleBaseCommand subclasses: Show,
/// Eject, Reset, Restart, Shutdown). Each just writes 1 to a "Command.&lt;X&gt;" parameter.
/// </summary>
internal sealed class VmGlobalCommand : VmCommandBase
{
    private readonly string _param;

    public VmGlobalCommand(string name, string display, string? description, string param, VoicemeeterService vm, IPluginLogger log)
        : base(vm, log)
    {
        _param = param;
        Descriptor = new CommandDescriptor
        {
            CommandName = Prefix + name,
            DisplayName = display,
            Group = Group,
            Description = description
        };
    }

    public override CommandDescriptor Descriptor { get; }

    protected override Task Run(CommandContext ctx)
    {
        if (!Vm.IsConnected)
        {
            Fail(ctx, Vm.Status, "offline");
            return Task.CompletedTask;
        }

        if (!Vm.TrySetFloat(_param, 1f, out var error)) Fail(ctx, error, "Failed");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Loads a Voicemeeter settings file (original LoadCommand: Command.Load = path, a string write).
/// The path is the command's only parameter; LoupixDeck splits parameters at commas, so a path
/// that happens to contain one arrives split across <see cref="CommandContext.Parameters"/> and is
/// rejoined here.
/// </summary>
internal sealed class VmLoadCommand : VmCommandBase
{
    public VmLoadCommand(VoicemeeterService vm, IPluginLogger log) : base(vm, log)
    {
        Descriptor = new CommandDescriptor
        {
            CommandName = Prefix + "Load",
            DisplayName = "Load Settings",
            Group = Group,
            Description = "Loads a Voicemeeter settings file from Path",
            ParameterTemplate = "({Path})",
            Parameters = [new CommandParameter("Path", typeof(string))]
        };
    }

    public override CommandDescriptor Descriptor { get; }

    protected override Task Run(CommandContext ctx)
    {
        if (!Vm.IsConnected)
        {
            Fail(ctx, Vm.Status, "offline");
            return Task.CompletedTask;
        }

        var path = string.Join(",", ctx.Parameters).Trim();
        if (path.Length == 0)
        {
            Fail(ctx, "missing Path parameter");
            return Task.CompletedTask;
        }

        if (!Vm.TrySetString("Command.Load", path, out var error)) Fail(ctx, error, "Failed");
        return Task.CompletedTask;
    }
}

internal static class GlobalCommands
{
    public static IEnumerable<IPluginCommand> Create(VoicemeeterService vm, IPluginLogger log)
    {
        yield return new VmGlobalCommand("Show", "Show Voicemeeter", "Shows the Voicemeeter window if minimized", "Command.Show", vm, log);
        yield return new VmGlobalCommand("Eject", "Eject Cassette", "Ejects the Voicemeeter recorder cassette", "Command.Eject", vm, log);
        yield return new VmLoadCommand(vm, log);
        yield return new VmGlobalCommand("Reset", "Reset Configuration", "(!) Resets ALL Voicemeeter configuration to default", "Command.Reset", vm, log);
        yield return new VmGlobalCommand("Restart", "Restart Audio Engine", "(!) Restarts the Voicemeeter audio engine (brief audio dropout)", "Command.Restart", vm, log);
        yield return new VmGlobalCommand("Shutdown", "Shutdown Voicemeeter", "(!) Shuts down Voicemeeter", "Command.Shutdown", vm, log);
    }
}
