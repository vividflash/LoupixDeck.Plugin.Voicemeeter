using LoupixDeck.Plugin.Voicemeeter.Remote;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

public class RemoteTests
{
    [Fact]
    public void ParameterNames_UseApiSyntax()
    {
        Assert.Equal("Strip[0].Mute", VmParam.Name("Strip", 0, "Mute"));
        Assert.Equal("Bus[7].Gain", VmParam.Name("Bus", 7, "Gain"));
        Assert.Equal("Strip[3].EQ.on", new Channel(ChannelKind.Strip, 3, "Strip 4").Param("EQ.on"));
        Assert.Equal("Bus[1].Label", new Channel(ChannelKind.Bus, 1, "A2").LabelParam);
    }

    [Theory]
    [InlineData(Edition.Standard, 2, 1, 3, 2, 1, 3)]
    [InlineData(Edition.Banana, 3, 2, 5, 3, 2, 5)]
    [InlineData(Edition.Potato, 5, 3, 8, 5, 3, 8)]
    public void EditionCounts(Edition e, int hw, int virt, int strips, int a, int b, int buses)
    {
        Assert.Equal(hw, EditionInfo.HardwareInputs(e));
        Assert.Equal(virt, EditionInfo.VirtualInputs(e));
        Assert.Equal(strips, EditionInfo.Strips(e));
        Assert.Equal(a, EditionInfo.ABuses(e));
        Assert.Equal(b, EditionInfo.BBuses(e));
        Assert.Equal(buses, EditionInfo.Buses(e));
        Assert.Equal(buses, EditionInfo.BusNames(e).Count);
    }

    [Fact]
    public void BusNames_PerEdition()
    {
        Assert.Equal(["A1", "A2", "B1"], EditionInfo.BusNames(Edition.Standard));
        Assert.Equal(["A1", "A2", "A3", "B1", "B2"], EditionInfo.BusNames(Edition.Banana));
        Assert.Equal(["A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3"], EditionInfo.BusNames(Edition.Potato));
    }

    [Theory]
    [InlineData(1, Edition.Standard)]
    [InlineData(2, Edition.Banana)]
    [InlineData(3, Edition.Potato)]
    [InlineData(6, Edition.Potato)]
    [InlineData(0, Edition.Unknown)]
    [InlineData(-2, Edition.Unknown)]
    public void EditionFromApiType(int type, Edition expected) => Assert.Equal(expected, EditionInfo.FromApiType(type));

    [Theory]
    [InlineData(ChannelKind.Strip, "1", Edition.Potato, 0, "Strip 1")]
    [InlineData(ChannelKind.Strip, " 8 ", Edition.Potato, 7, "Strip 8")]
    [InlineData(ChannelKind.Strip, "3", Edition.Standard, 2, "Strip 3")]
    // HardwareInput/VirtualInput resolve the same global strip number as Strip; the kind only
    // restricts applicability afterwards (see KindApplies_MatchesGlobalStripLocation).
    [InlineData(ChannelKind.HardwareInput, "5", Edition.Potato, 4, "Strip 5")]
    [InlineData(ChannelKind.VirtualInput, "6", Edition.Potato, 5, "Strip 6")]
    [InlineData(ChannelKind.VirtualInput, "5", Edition.Banana, 4, "Strip 5")]
    [InlineData(ChannelKind.Bus, "1", Edition.Potato, 0, "A1")]
    [InlineData(ChannelKind.Bus, "a1", Edition.Potato, 0, "A1")]
    [InlineData(ChannelKind.Bus, "B1", Edition.Potato, 5, "B1")]
    [InlineData(ChannelKind.Bus, "B1", Edition.Banana, 3, "B1")]
    [InlineData(ChannelKind.Bus, "B1", Edition.Standard, 2, "B1")]
    [InlineData(ChannelKind.Bus, "8", Edition.Potato, 7, "B3")]
    public void Resolve_Valid(ChannelKind kind, string text, Edition e, int apiIndex, string name)
    {
        Assert.True(VmParam.TryResolve(kind, text, e, out var ch, out var error), error);
        Assert.Equal(apiIndex, ch.ApiIndex);
        Assert.Equal(name, ch.Name);
    }

    [Theory]
    [InlineData(ChannelKind.Strip, "0", Edition.Potato)]
    [InlineData(ChannelKind.Strip, "9", Edition.Potato)]
    [InlineData(ChannelKind.Strip, "4", Edition.Standard)]
    [InlineData(ChannelKind.Strip, "x", Edition.Potato)]
    [InlineData(ChannelKind.Strip, "", Edition.Potato)]
    [InlineData(ChannelKind.Strip, null, Edition.Potato)]
    [InlineData(ChannelKind.Strip, "1", Edition.Unknown)]
    // HardwareInput/VirtualInput now share the global strip range (1..Strips), so the same bounds
    // as Strip apply; kind-mismatch (e.g. hardware number on a virtual strip) is not a resolve
    // failure, it is an applicability restriction (see KindApplies_MatchesGlobalStripLocation).
    [InlineData(ChannelKind.HardwareInput, "0", Edition.Potato)]
    [InlineData(ChannelKind.HardwareInput, "9", Edition.Potato)]
    [InlineData(ChannelKind.VirtualInput, "0", Edition.Banana)]
    [InlineData(ChannelKind.VirtualInput, "6", Edition.Banana)]
    [InlineData(ChannelKind.Bus, "B3", Edition.Banana)]
    [InlineData(ChannelKind.Bus, "A3", Edition.Standard)]
    [InlineData(ChannelKind.Bus, "C1", Edition.Potato)]
    [InlineData(ChannelKind.Bus, "4", Edition.Standard)]
    public void Resolve_Invalid(ChannelKind kind, string? text, Edition e)
    {
        Assert.False(VmParam.TryResolve(kind, text, e, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    // Potato: 5 hardware inputs, 3 virtual (global strips 1-5 hardware, 6-8 virtual).
    [InlineData(ChannelKind.HardwareInput, 0, Edition.Potato, true)]
    [InlineData(ChannelKind.HardwareInput, 4, Edition.Potato, true)]
    [InlineData(ChannelKind.HardwareInput, 5, Edition.Potato, false)]
    [InlineData(ChannelKind.VirtualInput, 4, Edition.Potato, false)]
    [InlineData(ChannelKind.VirtualInput, 5, Edition.Potato, true)]
    [InlineData(ChannelKind.VirtualInput, 7, Edition.Potato, true)]
    // Banana: 3 hardware inputs, 2 virtual (global strips 1-3 hardware, 4-5 virtual).
    [InlineData(ChannelKind.HardwareInput, 2, Edition.Banana, true)]
    [InlineData(ChannelKind.HardwareInput, 3, Edition.Banana, false)]
    [InlineData(ChannelKind.VirtualInput, 2, Edition.Banana, false)]
    [InlineData(ChannelKind.VirtualInput, 3, Edition.Banana, true)]
    [InlineData(ChannelKind.VirtualInput, 4, Edition.Banana, true)]
    // Strip and Bus are never restricted.
    [InlineData(ChannelKind.Strip, 7, Edition.Potato, true)]
    [InlineData(ChannelKind.Bus, 0, Edition.Potato, true)]
    public void KindApplies_MatchesGlobalStripLocation(ChannelKind kind, int apiIndex, Edition e, bool expected) =>
        Assert.Equal(expected, VmParam.KindApplies(kind, apiIndex, e));

    [Fact]
    public void DllCandidates_RegistryFirstThenFallback()
    {
        var reg = new Dictionary<string, string?>
        {
            [DllLocator.UninstallKey32] = "\"D:\\Apps\\VB\\Voicemeeter8Setup.exe\""
        };
        var list = DllLocator.Candidates(k => reg.GetValueOrDefault(k), "VoicemeeterRemote64.dll");
        Assert.Equal(["D:\\Apps\\VB\\VoicemeeterRemote64.dll", "C:\\Program Files (x86)\\VB\\Voicemeeter\\VoicemeeterRemote64.dll"], list);
    }

    [Fact]
    public void DllCandidates_NoRegistry_OnlyFallback_NoDuplicates()
    {
        Assert.Single(DllLocator.Candidates(_ => null, "VoicemeeterRemote64.dll"));
        var same = DllLocator.Candidates(_ => "C:\\Program Files (x86)\\VB\\Voicemeeter\\voicemeeterprosetup.exe", "VoicemeeterRemote64.dll");
        Assert.Single(same);
    }

    [Theory]
    [InlineData("C:\\VB\\Voicemeeter8Setup.exe", Edition.Potato)]
    [InlineData("C:\\VB\\voicemeeterprosetup.exe", Edition.Banana)]
    [InlineData("C:\\VB\\voicemeetersetup.exe", Edition.Standard)]
    [InlineData(null, Edition.Unknown)]
    public void EditionFromInstaller(string? path, Edition expected) => Assert.Equal(expected, DllLocator.EditionFromInstaller(path));
}
