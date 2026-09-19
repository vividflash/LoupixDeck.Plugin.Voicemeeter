using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>
/// Common plumbing for every Voicemeeter command: descriptor, error wrapping (a thrown exception
/// never reaches the host), channel resolution and dial feedback.
/// </summary>
internal abstract class VmCommandBase : IPluginCommand
{
    public const string Group = "Voicemeeter";
    public const string Prefix = "Voicemeeter.";
    internal static readonly TimeSpan OverlayDuration = TimeSpan.FromMilliseconds(1500);

    protected VmCommandBase(VoicemeeterService vm, IPluginLogger log)
    {
        Vm = vm;
        Log = log;
    }

    protected VoicemeeterService Vm { get; }
    protected IPluginLogger Log { get; }

    public abstract CommandDescriptor Descriptor { get; }

    public virtual ButtonTargets SupportedTargets => ButtonTargets.All;

    public string Name => Descriptor.CommandName;

    public async Task Execute(CommandContext ctx)
    {
        try
        {
            await Run(ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error($"{Name}: failed", ex);
            ShowOverlay(ctx, "Failed");
        }
    }

    /// <summary>The command's action. Report expected failures with <see cref="Fail"/>, do not throw.</summary>
    protected abstract Task Run(CommandContext ctx);

    /// <summary>Logs a warning and flashes a short text next to the dial (if pressed on a dial).</summary>
    protected void Fail(CommandContext ctx, string message, string overlay = "n/a")
    {
        Log.Warn($"{Name}: {message}");
        ShowOverlay(ctx, overlay);
    }

    /// <summary>Resolves parameter <paramref name="index"/> as a channel of <paramref name="kind"/> in the running edition.</summary>
    protected bool TryChannel(CommandContext ctx, ChannelKind kind, int index, out Channel channel, out string error)
    {
        var text = ctx.Parameters.Length > index ? ctx.Parameters[index] : null;
        return VmParam.TryResolve(kind, text, Vm.Edition, out channel, out error);
    }

    /// <summary>OverlayTouchText on the touch slot next to the dial that fired the command.</summary>
    protected static void ShowOverlay(CommandContext ctx, string text)
    {
        try
        {
            if (ctx.Target != ButtonTargets.RotaryEncoder || ctx.SourceIndex is not { } rotary) return;
            var slot = ctx.Host.GetTouchSlotForRotary(rotary);
            if (slot >= 0) ctx.Host.OverlayTouchText(slot, text, OverlayDuration);
        }
        catch
        {
            // feedback is best effort
        }
    }

    /// <summary>Draws the not-connected / wrong-edition / bad-parameter state. Always returns true.</summary>
    /// Not logged: it runs on every render tick.
    protected bool RenderUnavailable(IRenderCanvas canvas, string title)
    {
        var reason = Vm.State switch
        {
            ConnectionState.Connected => "n/a",
            ConnectionState.NotInstalled => "not installed",
            _ => "offline"
        };
        Render.Unavailable(canvas, title, reason);
        return true;
    }
}
