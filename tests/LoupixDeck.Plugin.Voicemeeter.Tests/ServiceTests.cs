using LoupixDeck.Plugin.Voicemeeter.Remote;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

public class ServiceTests
{
    [Fact]
    public void NoDll_LoadsHarmlessly_WithStatus()
    {
        var host = new FakeHost();
        using var vm = new VoicemeeterService(null, "Voicemeeter is not installed (x)", host.Logger, host.RequestButtonRefresh);
        vm.Start(runLoop: false);
        vm.PollOnce();
        Assert.Equal(ConnectionState.NotInstalled, vm.State);
        Assert.Equal("Voicemeeter is not installed (x)", vm.Status);
        Assert.False(vm.TrySetFloat("Strip[0].Mute", 1, out var error));
        Assert.Contains("not connected", error);
    }

    [Fact]
    public void LoginFailure_IsReported()
    {
        using var rig = new Rig(new FakeVoicemeeterApi { LoginResult = -1 });
        Assert.Equal(ConnectionState.LoginFailed, rig.Vm.State);
    }

    [Fact]
    public void Connects_AndReadsEdition()
    {
        using var rig = new Rig();
        Assert.Equal(1, rig.Api.Logins);
        Assert.Equal(ConnectionState.Connected, rig.Vm.State);
        Assert.Equal(Edition.Potato, rig.Vm.Edition);
        Assert.Equal("Connected to Voicemeeter Potato.", rig.Vm.Status);
    }

    [Fact]
    public void VoicemeeterStartedLater_Connects_AndRefreshesAllCommands()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 0;
        using var rig = new Rig(api);
        Assert.Equal(ConnectionState.Waiting, rig.Vm.State);
        Assert.Equal("Waiting for Voicemeeter to start.", rig.Vm.Status);

        api.RunningType = 2;
        rig.Vm.PollOnce();
        Assert.Equal(ConnectionState.Connected, rig.Vm.State);
        Assert.Equal(Edition.Banana, rig.Vm.Edition);
        Assert.Contains("Voicemeeter.StripMute", rig.Host.Refreshes);
        Assert.Contains("Voicemeeter.StripGain", rig.Host.Refreshes);
        Assert.Equal(1, api.Logins); // no re-login
    }

    [Fact]
    public void Dirty_RereadsWatched_AndRefreshesOnlyChangedCommands()
    {
        using var rig = new Rig();
        Assert.True(rig.Vm.TryGetFloat("Strip[0].Mute", "Cmd.A", out _));
        Assert.True(rig.Vm.TryGetFloat("Strip[1].Mute", "Cmd.B", out _));
        rig.Host.Refreshes.Clear();

        rig.Api.Floats["Strip[1].Mute"] = 1; // changed in the Voicemeeter UI
        rig.Api.Dirty = true;
        rig.Vm.PollOnce();

        Assert.Equal(["Cmd.B"], rig.Host.Refreshes);
        Assert.True(rig.Vm.TryGetFloat("Strip[1].Mute", "Cmd.B", out var v));
        Assert.Equal(1f, v);
    }

    [Fact]
    public void NotDirty_NoReads_NoRefresh()
    {
        using var rig = new Rig();
        rig.Vm.TryGetFloat("Strip[0].Mute", "Cmd.A", out _);
        rig.Host.Refreshes.Clear();
        rig.Api.Floats["Strip[0].Mute"] = 1;
        rig.Vm.PollOnce();
        Assert.Empty(rig.Host.Refreshes);
    }

    [Fact]
    public void LabelChange_RefreshesWatcher()
    {
        using var rig = new Rig();
        var ch = new Channel(ChannelKind.Strip, 0, "Strip 1");
        Assert.Equal("Strip 1", rig.Vm.GetLabel(ch, "Cmd.A"));
        rig.Host.Refreshes.Clear();
        rig.Api.Strings["Strip[0].Label"] = " Mic ";
        rig.Api.Dirty = true;
        rig.Vm.PollOnce();
        Assert.Equal(["Cmd.A"], rig.Host.Refreshes);
        Assert.Equal("Mic", rig.Vm.GetLabel(ch, "Cmd.A"));
    }

    [Fact]
    public void VoicemeeterClosed_DisconnectsAfterThreeFailedPolls()
    {
        using var rig = new Rig();
        rig.Api.RunningType = 0;
        rig.Vm.PollOnce();
        rig.Vm.PollOnce();
        Assert.Equal(ConnectionState.Connected, rig.Vm.State);
        rig.Vm.PollOnce();
        Assert.Equal(ConnectionState.Waiting, rig.Vm.State);
        Assert.Equal(Edition.Unknown, rig.Vm.Edition);
        Assert.Equal("Waiting for Voicemeeter to start.", rig.Vm.Status);

        rig.Api.RunningType = 3;
        rig.Vm.PollOnce();
        Assert.Equal(ConnectionState.Connected, rig.Vm.State);
    }

    [Fact]
    public void SetFloat_WritesUpdatesCacheAndRefreshesWatchers()
    {
        using var rig = new Rig();
        rig.Vm.TryGetFloat("Bus[0].Gain", "Cmd.A", out _);
        rig.Host.Refreshes.Clear();
        Assert.True(rig.Vm.TrySetFloat("Bus[0].Gain", -6, out _));
        Assert.Equal([("Bus[0].Gain", -6f)], rig.Api.Writes);
        Assert.Equal(["Cmd.A"], rig.Host.Refreshes);
        Assert.True(rig.Vm.TryGetFloat("Bus[0].Gain", "Cmd.A", out var v));
        Assert.Equal(-6f, v);
    }

    [Fact]
    public void SetFloat_UnknownParameter_ReportsError()
    {
        using var rig = new Rig();
        Assert.False(rig.Vm.TrySetFloat("Strip[0].Nope", 1, out var error));
        Assert.Contains("unknown parameter", error);
    }

    [Fact]
    public void StringScriptAndLevel_PassThrough()
    {
        using var rig = new Rig();
        rig.Api.Levels[(3, 0)] = 0.25f;
        Assert.True(rig.Vm.TrySetString("Command.Load", "x.xml", out _));
        Assert.True(rig.Vm.TrySetParameters("Strip[0].Mute=1", out _));
        Assert.True(rig.Vm.TryGetLevel(3, 0, out var level));
        Assert.Equal(0.25f, level);
        Assert.Equal([("Command.Load", "x.xml")], rig.Api.StringWrites);
        Assert.Equal(["Strip[0].Mute=1"], rig.Api.Scripts);
    }

    [Fact]
    public void Dispose_LogsOutOnce()
    {
        var rig = new Rig();
        rig.Dispose();
        rig.Dispose();
        Assert.Equal(1, rig.Api.Logouts);
        Assert.True(rig.Api.Disposed);
    }

    [Fact]
    public async Task PollLoop_RunsInBackground_AndStops()
    {
        var host = new FakeHost();
        var api = FakeVoicemeeterApi.Potato();
        var vm = new VoicemeeterService(api, "fake", host.Logger, host.RequestButtonRefresh, TimeSpan.FromMilliseconds(5));
        vm.Start();
        vm.TryGetFloat("Strip[0].Mute", "Cmd.A", out _);
        host.Refreshes.Clear();
        api.Floats["Strip[0].Mute"] = 1;
        api.Dirty = true;
        for (var i = 0; i < 200 && host.Refreshes.Count == 0; i++) await Task.Delay(10);
        vm.Dispose();
        Assert.Contains("Cmd.A", host.Refreshes);
        Assert.Equal(1, api.Logouts);
    }
}
