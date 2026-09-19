using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

/// <summary>Family C: globals (Show/Eject/Load/Reset/Restart/Shutdown) and the raw command/adjustment.</summary>
public class FamilyCTests
{
    [Theory]
    [InlineData("Voicemeeter.Show", "Command.Show")]
    [InlineData("Voicemeeter.Eject", "Command.Eject")]
    [InlineData("Voicemeeter.Reset", "Command.Reset")]
    [InlineData("Voicemeeter.Restart", "Command.Restart")]
    [InlineData("Voicemeeter.Shutdown", "Command.Shutdown")]
    public async Task Global_SendsCommandWriteOfOne(string command, string param)
    {
        using var rig = new Rig();
        rig.Api.Floats[param] = 0;
        await rig.Run(command);
        Assert.Equal([(param, 1f)], rig.Api.Writes);
    }

    [Fact]
    public async Task Global_Offline_NoWrite()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 0;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.Show");
        Assert.Empty(api.Writes);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("W Voicemeeter.Show"));
    }

    [Fact]
    public async Task Load_WritesStringParameter()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.Load", "C:\\configs\\studio.xml");
        Assert.Equal([("Command.Load", "C:\\configs\\studio.xml")], rig.Api.StringWrites);
    }

    [Fact]
    public async Task Load_PathWithComma_IsRejoined()
    {
        using var rig = new Rig();
        // LoupixDeck splits positional parameters at commas; Load rejoins them with ",".
        await rig.Run("Voicemeeter.Load", "C:\\configs\\my", "studio.xml");
        Assert.Equal([("Command.Load", "C:\\configs\\my,studio.xml")], rig.Api.StringWrites);
    }

    [Fact]
    public async Task Load_MissingPath_LogsWarning_NoThrow()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.Load", "");
        Assert.Empty(rig.Api.StringWrites);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("W Voicemeeter.Load: missing Path"));
    }

    // ---- RawCommand ----
    // Parameter order is Name, OnColor, OffColor, Api: Api is LAST and deliberately absorbs
    // every parameter from that position onward (rejoined with ","), because Voicemeeter scripts
    // legitimately contain commas and the host splits positional parameters on every "," in the
    // persisted command string.

    [Fact]
    public async Task Raw_SingleParameter_TogglesZeroToOne()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Mute"] = 0;
        await rig.Run("Voicemeeter.Raw", "Mic Mute", "", "", "Strip[0].Mute");
        Assert.Equal([("Strip[0].Mute", 1f)], rig.Api.Writes);
    }

    [Fact]
    public async Task Raw_SingleParameter_TogglesOneToZero()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Mute"] = 1;
        await rig.Run("Voicemeeter.Raw", "Mic Mute", "", "", "Strip[0].Mute");
        Assert.Equal([("Strip[0].Mute", 0f)], rig.Api.Writes);
    }

    [Fact]
    public async Task Raw_SingleParameter_RendersOnOffFromLiveValue()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Mute"] = 0;
        var off = rig.Render("Voicemeeter.Raw", "Mic Mute", "", "", "Strip[0].Mute");
        Assert.Equal(Palette.Inactive, off.Background);

        rig.Api.Floats["Strip[0].Mute"] = 1;
        rig.Api.Dirty = true;
        rig.Vm.PollOnce();
        var on = rig.Render("Voicemeeter.Raw", "Mic Mute", "", "", "Strip[0].Mute");
        Assert.Equal(Palette.Active, on.Background);
    }

    [Fact]
    public async Task Raw_Script_RunsAsIs()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.Raw", "Two Things", "", "", "Strip[0].Mute=1;Bus[0].Mono=1");
        Assert.Equal(["Strip[0].Mute=1;Bus[0].Mono=1"], rig.Api.Scripts);
    }

    [Fact]
    public async Task Raw_Script_WithToggle_InvertsTargetValue()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Mute"] = 1;
        await rig.Run("Voicemeeter.Raw", "Toggle Mute", "", "", "Strip[0].Mute=%toggle%");
        Assert.Equal(["Strip[0].Mute=0"], rig.Api.Scripts);
    }

    [Fact]
    public async Task Raw_Script_WithToggle_MultipleInstructions()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Mute"] = 0;
        rig.Api.Floats["Bus[0].Mono"] = 0;
        await rig.Run("Voicemeeter.Raw", "Combo", "", "", "Strip[0].Mute=%toggle%;Bus[0].Mono=1");
        Assert.Equal(["Strip[0].Mute=1;Bus[0].Mono=1"], rig.Api.Scripts);
    }

    [Fact]
    public async Task Raw_Script_ToggleOnUnreadableTarget_LogsWarning_NoThrow()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.Raw", "Bad", "", "", "Strip[99].Bogus=%toggle%");
        Assert.Empty(rig.Api.Scripts);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("W Voicemeeter.Raw: cannot toggle"));
    }

    [Fact]
    public async Task Raw_MissingApi_LogsWarning_NoThrow()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.Raw", "Name only");
        Assert.Empty(rig.Api.Writes);
        Assert.Empty(rig.Api.Scripts);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("W Voicemeeter.Raw: missing Api"));
    }

    [Fact]
    public async Task Raw_UnknownParameter_LogsWarning_NoThrow()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.Raw", "Ghost", "", "", "Strip[9].DoesNotExist");
        Assert.Empty(rig.Api.Writes);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("W Voicemeeter.Raw: Voicemeeter refused"));
    }

    [Fact]
    public async Task Raw_ApiWithEmbeddedComma_IsReconstructed()
    {
        using var rig = new Rig();
        // Simulates what the host hands the command after splitting a script such as
        // "Strip[0].Label=A,B" on every comma: the tail past OffColor is two array entries.
        await rig.Run("Voicemeeter.Raw", "SetLabel", "", "", "Strip[0].Label=A", "B");
        Assert.Equal(["Strip[0].Label=A,B"], rig.Api.Scripts);
    }

    // ---- RawAdjustment ----
    // Parameter order is Step, Min, Max, Api: the three numeric parameters stay fixed and Api is
    // LAST, absorbing every parameter from that position onward (rejoined with ","), for the same
    // reason as RawCommand above.

    [Fact]
    public async Task RawUp_StepsAndClamps()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Comp"] = 9.5f;
        await rig.Run("Voicemeeter.RawUp", "1", "0", "10", "Strip[0].Comp");
        Assert.Equal(("Strip[0].Comp", 10f), rig.Api.Writes[^1]);

        var writes = rig.Api.Writes.Count;
        await rig.Run("Voicemeeter.RawUp", "1", "0", "10", "Strip[0].Comp");
        Assert.Equal(writes, rig.Api.Writes.Count); // already at max: no write
    }

    [Fact]
    public async Task RawDown_CustomStep_ClampsAtMin()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Comp"] = 1f;
        await rig.Run("Voicemeeter.RawDown", "3", "0", "10", "Strip[0].Comp");
        Assert.Equal(("Strip[0].Comp", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task RawReset_SetsZero_ClampedToRange()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Comp"] = 7f;
        await rig.Run("Voicemeeter.RawReset", "1", "0", "10", "Strip[0].Comp");
        Assert.Equal(("Strip[0].Comp", 0f), rig.Api.Writes[^1]);

        rig.Api.Floats["Strip[0].Comp"] = 7f;
        await rig.Run("Voicemeeter.RawReset", "1", "2", "10", "Strip[0].Comp");
        Assert.Equal(("Strip[0].Comp", 2f), rig.Api.Writes[^1]); // 0 clamped up to Min
    }

    [Fact]
    public void RawReset_RendersValueBar()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Comp"] = 5f;
        var canvas = rig.Render("Voicemeeter.RawReset", "1", "0", "10", "Strip[0].Comp");
        Assert.Contains("Strip[0].Comp", canvas.Texts);
        var bar = Assert.Single(canvas.Fills);
        Assert.Equal(37, bar.W); // half of 74 px, same geometry as family B's bar
    }

    [Fact]
    public async Task RawAdjustment_BadMinMax_LogsWarning_NoThrow()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.RawUp", "1", "10", "0", "Strip[0].Comp"); // Max <= Min
        Assert.Empty(rig.Api.Writes);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("W Voicemeeter.RawUp"));
    }

    [Fact]
    public async Task RawAdjustment_MissingApi_LogsWarning_NoThrow()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.RawUp");
        Assert.Empty(rig.Api.Writes);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("W Voicemeeter.RawUp: missing Api"));
    }

    [Fact]
    public async Task RawAdjustment_Offline_NoWrite()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 0;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.RawUp", "1", "0", "10", "Strip[0].Comp");
        Assert.Empty(api.Writes);
        Assert.Contains("offline", rig.Render("Voicemeeter.RawReset", "1", "0", "10", "Strip[0].Comp").Texts);
    }
}
