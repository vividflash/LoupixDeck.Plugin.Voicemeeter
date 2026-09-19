using LoupixDeck.Plugin.Voicemeeter.Actions;
using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

public class CommandTests
{
    [Fact]
    public void Registry_HasPilotCommands()
    {
        using var rig = new Rig();
        var names = rig.Commands.Select(c => c.Descriptor.CommandName).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        foreach (var n in new[]
                 {
                     "Voicemeeter.StripMute", "Voicemeeter.BusMute",
                     "Voicemeeter.StripGainDown", "Voicemeeter.StripGainUp", "Voicemeeter.StripGainReset",
                     "Voicemeeter.BusGainDown", "Voicemeeter.BusGainUp", "Voicemeeter.BusGainReset"
                 })
        {
            Assert.Contains(n, names);
        }

        Assert.All(rig.Commands, c => Assert.Equal("Voicemeeter", c.Descriptor.Group));
        Assert.All(rig.Commands, c => Assert.StartsWith("Voicemeeter.", c.Descriptor.CommandName));
    }

    [Fact]
    public void Descriptors_HaveChannelParameter()
    {
        using var rig = new Rig();
        var mute = rig.Command("Voicemeeter.StripMute").Descriptor;
        Assert.Equal("({Strip})", mute.ParameterTemplate);
        Assert.Equal("Strip", mute.Parameters[0].Name);
        Assert.Equal("1", mute.Parameters[0].DefaultValue);

        var up = rig.Command("Voicemeeter.BusGainUp").Descriptor;
        Assert.Equal("({Bus},{Step})", up.ParameterTemplate);
        Assert.Equal("1", up.Parameters[1].DefaultValue);
    }

    [Fact]
    public async Task StripMute_Toggles_AndRendersState()
    {
        using var rig = new Rig();
        rig.Api.Strings["Strip[0].Label"] = "Mic";

        var before = rig.Render("Voicemeeter.StripMute", "1");
        Assert.Equal(Palette.Inactive, before.Background);
        Assert.Equal(["Mic", "Mute"], before.Texts);

        await rig.Run("Voicemeeter.StripMute", "1");
        Assert.Equal([("Strip[0].Mute", 1f)], rig.Api.Writes);
        Assert.Contains("Voicemeeter.StripMute", rig.Host.Refreshes);
        Assert.Equal(Palette.Danger, rig.Render("Voicemeeter.StripMute", "1").Background);

        await rig.Run("Voicemeeter.StripMute", "1");
        Assert.Equal(("Strip[0].Mute", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public void StripMute_FollowsChangeMadeInVoicemeeter()
    {
        using var rig = new Rig();
        rig.Render("Voicemeeter.StripMute", "3");
        rig.Host.Refreshes.Clear();

        rig.Api.Floats["Strip[2].Mute"] = 1;
        rig.Api.Dirty = true;
        rig.Vm.PollOnce();

        Assert.Equal(["Voicemeeter.StripMute"], rig.Host.Refreshes);
        Assert.Equal(Palette.Danger, rig.Render("Voicemeeter.StripMute", "3").Background);
        Assert.Empty(rig.Api.Writes);
    }

    [Fact]
    public async Task BusMute_ByName()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.BusMute", "B2");
        Assert.Equal([("Bus[6].Mute", 1f)], rig.Api.Writes);
        Assert.Equal(["B2", "Mute"], rig.Render("Voicemeeter.BusMute", "B2").Texts);
    }

    [Fact]
    public async Task BusMute_MissingInEdition_NoWrite_DrawsNa()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2; // Banana: B1-B2 only
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.BusMute", "B3");
        Assert.Empty(api.Writes);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("W Voicemeeter.BusMute: bus B3 does not exist"));
        Assert.Contains("n/a", rig.Render("Voicemeeter.BusMute", "B3").Texts);
    }

    [Fact]
    public async Task Offline_NoWrite_DrawsOffline()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 0;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripMute", "1");
        await rig.Run("Voicemeeter.StripGainUp", "1");
        Assert.Empty(api.Writes);
        Assert.Contains("offline", rig.Render("Voicemeeter.StripMute", "1").Texts);
        Assert.Contains("offline", rig.Render("Voicemeeter.StripGainReset", "1").Texts);
    }

    [Fact]
    public void NotInstalled_DrawsNotInstalled()
    {
        var host = new FakeHost();
        using var vm = new VoicemeeterService(null, "missing", host.Logger, host.RequestButtonRefresh);
        var cmd = (IDisplayImageCommand)VoicemeeterPlugin.BuildCommands(vm, host.Logger).First();
        var canvas = new FakeCanvas();
        Assert.True(cmd.RenderImage(new CommandContext { Parameters = ["1"], Target = ButtonTargets.TouchButton, Host = host }, canvas));
        Assert.Contains("not installed", canvas.Texts);
    }

    [Fact]
    public async Task GainUp_StepsAndClampsAtPlus12()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Gain"] = 11.5f;
        await rig.Run("Voicemeeter.StripGainUp", "1");
        Assert.Equal(("Strip[0].Gain", 12f), rig.Api.Writes[^1]);
        var writes = rig.Api.Writes.Count;
        await rig.Run("Voicemeeter.StripGainUp", "1");
        Assert.Equal(writes, rig.Api.Writes.Count); // already at max: no write
    }

    [Fact]
    public async Task GainDown_CustomStep_ClampsAtMinus60()
    {
        using var rig = new Rig();
        rig.Api.Floats["Bus[0].Gain"] = -58f;
        await rig.Run("Voicemeeter.BusGainDown", "A1", "3");
        Assert.Equal(("Bus[0].Gain", -60f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task GainDown_HalfStep()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.StripGainDown", "2", "0.5");
        Assert.Equal(("Strip[1].Gain", -0.5f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task GainReset_SetsZero()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[4].Gain"] = -20f;
        await rig.Run("Voicemeeter.StripGainReset", "5");
        Assert.Equal(("Strip[4].Gain", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task Gain_OnDial_FlashesValueNextToDial()
    {
        using var rig = new Rig();
        rig.Api.Strings["Strip[0].Label"] = "Mic";
        await rig.Command("Voicemeeter.StripGainUp").Execute(rig.DialCtx(2, "1"));
        Assert.Equal([(12, "Mic +1.0 dB")], rig.Host.Overlays);
    }

    [Fact]
    public void GainReset_RendersBar()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Gain"] = -24f;
        var canvas = rig.Render("Voicemeeter.StripGainReset", "1");
        Assert.Equal(["Strip 1", "Gain", "-24.0 dB"], canvas.Texts);
        var bar = Assert.Single(canvas.Fills);
        Assert.Equal(Palette.Active, bar.Color);
        Assert.Equal(37, bar.W); // half of 74 px

        rig.Api.Floats["Strip[0].Mute"] = 1;
        rig.Api.Dirty = true;
        rig.Vm.PollOnce();
        Assert.Equal(Palette.Inactive, Assert.Single(rig.Render("Voicemeeter.StripGainReset", "1").Fills).Color);
    }

    [Fact]
    public async Task GainReset_RedrawsWhenKnobTurns()
    {
        using var rig = new Rig();
        rig.Render("Voicemeeter.BusGainReset", "A1");
        rig.Host.Refreshes.Clear();
        await rig.Run("Voicemeeter.BusGainUp", "A1");
        Assert.Contains("Voicemeeter.BusGainReset", rig.Host.Refreshes);
    }

    [Fact]
    public async Task BadParameter_LogsWarning_NoThrow()
    {
        using var rig = new Rig();
        await rig.Run("Voicemeeter.StripMute", "abc");
        await rig.Run("Voicemeeter.StripMute");
        Assert.Empty(rig.Api.Writes);
        Assert.Equal(2, rig.Host.FakeLog.Lines.Count(l => l.StartsWith("W Voicemeeter.StripMute")));
    }

    [Fact]
    public void Spec_PotatoOnly_DrawsNaOnBanana()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2;
        using var rig = new Rig(api);
        var spec = new ToggleSpec("TestPost", "Test", ChannelKind.HardwareInput, "PostFx1") { PotatoOnly = true };
        var cmd = new ToggleCommand(spec, rig.Vm, rig.Host.Logger);
        var canvas = new FakeCanvas();
        Assert.True(cmd.RenderImage(rig.Ctx("1"), canvas));
        Assert.Contains("n/a", canvas.Texts);
    }

    [Fact]
    public async Task Spec_SubChannel_BuildsField()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[1].A3"] = 0;
        var spec = new ToggleSpec("TestA", "Test A", ChannelKind.Strip, "A")
        {
            Sub = new SubChannelSpec("Bus", "A", EditionInfo.ABuses)
        };
        var cmd = new ToggleCommand(spec, rig.Vm, rig.Host.Logger);
        Assert.Equal("({Strip},{Bus})", cmd.Descriptor.ParameterTemplate);
        await cmd.Execute(rig.Ctx("2", "3"));
        Assert.Equal([("Strip[1].A3", 1f)], rig.Api.Writes);
        await cmd.Execute(rig.Ctx("2", "6")); // Potato has A1-A5
        Assert.Single(rig.Api.Writes);
    }

    [Fact]
    public void AllSpecs_HaveUniqueNames()
    {
        var names = Toggles.All.Select(s => s.CommandName)
            .Concat(Adjustments.All.SelectMany(s => new[] { s.UpName, s.DownName, s.ResetName }))
            .ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
