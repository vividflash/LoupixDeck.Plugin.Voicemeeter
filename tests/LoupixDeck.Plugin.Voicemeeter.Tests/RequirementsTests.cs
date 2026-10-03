using LoupixDeck.Plugin.Voicemeeter.Remote;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

public class RequirementsTests
{
    [Fact]
    public void DllMissing_ReportsUnmet_WithMessageAndHint()
    {
        var host = new FakeHost();
        using var vm = new VoicemeeterService(null, "not installed", host.Logger, host.RequestButtonRefresh);
        vm.Start(runLoop: false);

        var req = Assert.Single(VoicemeeterPlugin.BuildRequirements(vm.State));
        Assert.Equal("voicemeeter-installed", req.Id);
        Assert.False(req.IsMet);
        Assert.False(string.IsNullOrWhiteSpace(req.Message));
        Assert.Contains("vb-audio.com/Voicemeeter", req.InstallHint);
    }

    [Theory]
    [InlineData(0)]   // Voicemeeter running
    [InlineData(1)]   // logged in, Voicemeeter not running
    public void DllFound_ReportsMet(int loginResult)
    {
        using var rig = new Rig(new FakeVoicemeeterApi { LoginResult = loginResult, RunningType = loginResult == 0 ? 3 : 0 });

        var req = Assert.Single(VoicemeeterPlugin.BuildRequirements(rig.Vm.State));
        Assert.True(req.IsMet);
        Assert.Null(req.Message);
        Assert.Empty(rig.Api.Writes);
        Assert.Empty(rig.Api.StringWrites);
        Assert.Empty(rig.Api.Scripts);
    }

    [Fact]
    public void LoginFailed_StillMet_DllWasFound()
    {
        using var rig = new Rig(new FakeVoicemeeterApi { LoginResult = -1 });
        Assert.True(Assert.Single(VoicemeeterPlugin.BuildRequirements(rig.Vm.State)).IsMet);
    }

    [Fact]
    public void BeforeInitialize_ReportsNothing() => Assert.Empty(new VoicemeeterPlugin().GetRequirements());
}
