using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

public class FamilyDTests
{
    private const string Level = "Voicemeeter.Level";

    private static FakeCanvas RenderMeter(Rig rig, params string[] parameters)
    {
        var canvas = new FakeCanvas();
        var info = ((IAnimatedDisplayCommand)rig.Command(Level)).RenderAnimatedFrame(rig.Ctx(parameters), canvas, default);
        Assert.True(info.Drawn);
        return canvas;
    }

    private static Channel Resolve(ChannelKind kind, string text, Edition edition)
    {
        Assert.True(VmParam.TryResolve(kind, text, edition, out var channel, out var error), error);
        return channel;
    }

    [Fact]
    public void DbConversion()
    {
        Assert.Equal(0f, LevelMap.ToDb(1f), 3);
        Assert.Equal(-6.02f, LevelMap.ToDb(0.5f), 2);
        Assert.Equal(-20f, LevelMap.ToDb(0.1f), 3);
        Assert.True(float.IsNegativeInfinity(LevelMap.ToDb(0f)));
        Assert.Equal(0f, LevelMap.Fraction(float.NegativeInfinity));
        Assert.Equal(0f, LevelMap.Fraction(-80f));
        Assert.Equal(60f / 72f, LevelMap.Fraction(0f), 4);
        Assert.Equal(1f, LevelMap.Fraction(20f));
        Assert.Equal("-6 dB", LevelMap.FormatDb(LevelMap.ToDb(0.5f)));
        Assert.Equal("0 dB", LevelMap.FormatDb(-0.2f));
        Assert.Equal("-inf", LevelMap.FormatDb(float.NegativeInfinity));
        Assert.Equal(MeterRender.Green, MeterRender.PeakColor(-20f));
        Assert.Equal(MeterRender.Yellow, MeterRender.PeakColor(-6f));
        Assert.Equal(MeterRender.Red, MeterRender.PeakColor(0f));
    }

    [Theory]
    [InlineData("PreFader", LevelType.PreFaderInput)]
    [InlineData("postfader", LevelType.PostFaderInput)]
    [InlineData("Post-Mute", LevelType.PostMuteInput)]
    [InlineData("PostMuteInput", LevelType.PostMuteInput)]
    [InlineData("Output", LevelType.Output)]
    [InlineData("3", LevelType.Output)]
    [InlineData("", LevelType.PostFaderInput)]
    public void LevelType_Parses(string text, LevelType expected)
    {
        Assert.True(LevelMap.TryParseType(text, out var type));
        Assert.Equal(expected, type);
    }

    [Fact]
    public void LevelType_RejectsUnknown()
    {
        Assert.False(LevelMap.TryParseType("loud", out _));
        Assert.False(LevelMap.TryParseType("4", out _));
    }

    [Theory]
    // Potato: 5 hardware strips x 2 + 3 virtual x 8 = 34 input channels; 8 buses x 8 = 64 outputs.
    [InlineData(Edition.Potato, ChannelKind.Strip, "1", LevelType.PostFaderInput, 0, 2)]
    [InlineData(Edition.Potato, ChannelKind.Strip, "5", LevelType.PreFaderInput, 8, 2)]
    [InlineData(Edition.Potato, ChannelKind.Strip, "6", LevelType.PostMuteInput, 10, 8)]
    [InlineData(Edition.Potato, ChannelKind.Strip, "8", LevelType.PostFaderInput, 26, 8)]
    [InlineData(Edition.Potato, ChannelKind.Bus, "A1", LevelType.Output, 0, 8)]
    [InlineData(Edition.Potato, ChannelKind.Bus, "B3", LevelType.Output, 56, 8)]
    // Banana: 3 x 2 + 2 x 8 = 22 inputs; 5 buses = 40 outputs.
    [InlineData(Edition.Banana, ChannelKind.Strip, "3", LevelType.PostFaderInput, 4, 2)]
    [InlineData(Edition.Banana, ChannelKind.Strip, "4", LevelType.PostFaderInput, 6, 8)]
    [InlineData(Edition.Banana, ChannelKind.Strip, "5", LevelType.PostFaderInput, 14, 8)]
    [InlineData(Edition.Banana, ChannelKind.Bus, "B2", LevelType.Output, 32, 8)]
    // Standard: 2 x 2 + 1 x 8 = 12 inputs; 3 buses = 24 outputs.
    [InlineData(Edition.Standard, ChannelKind.Strip, "2", LevelType.PostFaderInput, 2, 2)]
    [InlineData(Edition.Standard, ChannelKind.Strip, "3", LevelType.PostFaderInput, 4, 8)]
    [InlineData(Edition.Standard, ChannelKind.Bus, "B1", LevelType.Output, 16, 8)]
    public void LevelIndexMapping(Edition edition, ChannelKind kind, string text, LevelType type, int first, int count)
    {
        var (f, c) = LevelMap.Range(type, Resolve(kind, text, edition), edition);
        Assert.Equal((first, count), (f, c));
        var total = type == LevelType.Output ? LevelMap.OutputChannels(edition) : LevelMap.InputChannels(edition);
        Assert.True(f + c <= total);
    }

    [Fact]
    public void ChannelTotals_PerEdition()
    {
        Assert.Equal((34, 64), (LevelMap.InputChannels(Edition.Potato), LevelMap.OutputChannels(Edition.Potato)));
        Assert.Equal((22, 40), (LevelMap.InputChannels(Edition.Banana), LevelMap.OutputChannels(Edition.Banana)));
        Assert.Equal((12, 24), (LevelMap.InputChannels(Edition.Standard), LevelMap.OutputChannels(Edition.Standard)));
    }

    [Fact]
    public void Meter_DrawsLouderChannelOfPair_InZones()
    {
        using var rig = new Rig();
        rig.Api.Levels[(1, 0)] = 0.1f;
        rig.Api.Levels[(1, 1)] = 0.5f; // right channel louder: -6 dB
        var canvas = RenderMeter(rig, "1", "PostFader");

        Assert.Contains("Strip 1", canvas.Texts);
        Assert.Contains("Post-fader", canvas.Texts);
        Assert.Contains("-6 dB", canvas.Texts);
        // Bar is 78 px wide: -6 dB fills 58 px, green up to -12 dB (52 px), then 6 px yellow, no red.
        Assert.Contains(canvas.Fills, f => f.X == 6 && f.W == 52 && f.Color == MeterRender.Green);
        Assert.Contains(canvas.Fills, f => f.X == 58 && f.W == 6 && f.Color == MeterRender.Yellow);
        Assert.DoesNotContain(canvas.Fills, f => f.Color == MeterRender.Red);
    }

    [Fact]
    public void Meter_BusByName_OutputAboveZeroIsRed()
    {
        using var rig = new Rig();
        rig.Api.Levels[(3, 13)] = 2f; // A2 = Bus[1] = output channels 8..15, +6 dB
        var canvas = RenderMeter(rig, "A2", "Output");
        Assert.Contains("A2", canvas.Texts);
        Assert.Contains("6 dB", canvas.Texts);
        Assert.Contains(canvas.Fills, f => f.Color == MeterRender.Red);
    }

    [Fact]
    public void Meter_OfflineAndBadParameters_ShowUnavailable()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 0;
        using (var offline = new Rig(api))
            Assert.Contains("offline", RenderMeter(offline, "1", "PostFader").Texts);

        using var rig = new Rig();
        Assert.Contains("n/a", RenderMeter(rig, "9", "PostFader").Texts);   // Potato has 8 strips
        Assert.Contains("n/a", RenderMeter(rig, "A1", "PostFader").Texts);  // bus name on an input type
        Assert.Contains("n/a", RenderMeter(rig, "1", "loud").Texts);
    }

    [Fact]
    public void Meter_FrameNumber_OnlyChangesWithPicture()
    {
        using var rig = new Rig();
        rig.Api.Levels[(3, 0)] = 0.5f;
        var cmd = (IAnimatedDisplayCommand)rig.Command(Level);
        var a = cmd.RenderAnimatedFrame(rig.Ctx("A1", "Output"), new FakeCanvas(), default).FrameNumber;
        var b = cmd.RenderAnimatedFrame(rig.Ctx("A1", "Output"), new FakeCanvas(), default).FrameNumber;
        rig.Api.Levels[(3, 0)] = 0.05f;
        rig.Vm.PollOnce();
        var c = cmd.RenderAnimatedFrame(rig.Ctx("A1", "Output"), new FakeCanvas(), default).FrameNumber;
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(cmd.TargetFps is > 0 and <= 15);
    }

    [Fact]
    public void Poller_RunsOnlyWhileMetersNeedLevels()
    {
        var fake = FakeVoicemeeterApi.Potato();
        fake.Levels[(3, 0)] = 0.5f;
        var api = new CountingApi(fake);
        var log = new FakeLogger();
        using var vm = new VoicemeeterService(api, "fake", log, _ => { });
        long now = 0;
        vm.Clock = () => now;
        vm.Start(runLoop: false);

        vm.PollOnce();
        Assert.False(vm.LevelPollingActive);
        Assert.Equal(0, api.LevelReads);

        // A meter read leases the channel; poll ticks keep it fresh.
        Assert.True(vm.TryGetLevel(3, 0, out var v));
        Assert.Equal(0.5f, v);
        Assert.True(vm.LevelPollingActive);
        fake.Levels[(3, 0)] = 0.25f;
        vm.PollOnce();
        Assert.Equal(2, api.LevelReads);
        Assert.True(vm.TryGetLevel(3, 0, out v));
        Assert.Equal(0.25f, v);
        Assert.Equal(2, api.LevelReads); // served from the cache

        // Nobody reads for longer than the lease: the poller stops reading.
        now += VoicemeeterService.LevelLeaseMs + 1;
        vm.PollOnce();
        Assert.False(vm.LevelPollingActive);
        var reads = api.LevelReads;
        vm.PollOnce();
        vm.PollOnce();
        Assert.Equal(reads, api.LevelReads);

        // A subscriber (side strip) keeps it running until disposed.
        var ticks = 0;
        var sub = vm.SubscribeLevels(() => ticks++);
        Assert.True(vm.LevelPollingActive);
        vm.PollOnce();
        vm.PollOnce();
        Assert.Equal(2, ticks);
        sub.Dispose();
        Assert.False(vm.LevelPollingActive);
        vm.PollOnce();
        Assert.Equal(2, ticks);
    }

    private static SideStripContext Context(params SideStripRotary[] rotaries) =>
        new() { Side = StripSide.Left, Width = 60, Height = 270, Rotaries = rotaries };

    [Fact]
    public void Strip_MetersChannelsOfBoundDials()
    {
        using var rig = new Rig();
        rig.Api.Levels[(1, 2)] = 0.5f;          // Strip 2 left, post-fader
        rig.Api.Levels[(3, 0)] = 0.1f;          // A1
        rig.Api.Floats["Strip[1].Gain"] = -6f;
        rig.Api.Floats["Bus[0].Mute"] = 1f;
        var provider = new LevelStripProvider(rig.Vm, rig.Host.Settings, rig.Host.Logger);
        Assert.IsAssignableFrom<ISegmentStripProvider>(provider);
        using var session = (LevelStripSession)provider.CreateSession(Context(
            new SideStripRotary { Index = 0, LeftCommand = "Voicemeeter.StripGain(2,1)", RightCommand = "Voicemeeter.StripGain(2,1)" },
            new SideStripRotary { Index = 1, Label = "Speakers", PressCommand = "Voicemeeter.BusMute(A1)" },
            new SideStripRotary { Index = 2, Label = "Spotify", RightCommand = "Audio.VolumeUp(x)" }));

        Assert.True(session.UsesDials);
        Assert.Equal(
            [new LevelSource(ChannelKind.Strip, "2", LevelType.PostFaderInput, ""), new LevelSource(ChannelKind.Bus, "A1", LevelType.Output, "Speakers"), null],
            session.Sources());
        var bands = session.Bands;
        Assert.Equal(("Strip 2", -6f, false), (bands[0].Name, bands[0].Gain!.Value, bands[0].Muted));
        Assert.Equal(-6.02f, bands[0].Db, 2);
        Assert.Equal(("Speakers", true, -20f), (bands[1].Name, bands[1].Muted, MathF.Round(bands[1].Db)));
        Assert.False(bands[2].Has);

        var segment = new FakeCanvas();
        Assert.True(session.RenderSegment(0, segment));
        Assert.Contains("-6.0", segment.Texts);
        Assert.True(session.RenderSegment(1, new FakeCanvas()));
        Assert.False(session.RenderSegment(2, new FakeCanvas()));   // host draws that dial's label
        var strip = new FakeCanvas();
        Assert.True(session.RenderStrip(strip));
        Assert.Contains("muted", strip.Texts);
    }

    [Fact]
    public void Strip_LevelCommandOnDial_UsesItsType()
    {
        var source = DialChannelParser.FromRotary(new SideStripRotary { PressCommand = "Voicemeeter.Level(3,PreFader)" });
        Assert.Equal(new LevelSource(ChannelKind.Strip, "3", LevelType.PreFaderInput, ""), source);
    }

    [Fact]
    public void Strip_RenamedHardwareInputAdjustmentOnDial_MetersItsGlobalStripNumber()
    {
        // StripComp (renamed from InputComp) is a HardwareInput-restricted adjustment; the dial
        // carries the global strip number ("1"), and the bound-dial detection (here by the pre-1.0
        // Up/Down/Reset command names, which stay registered) must still recognise it and meter Strip[0], not treat it as
        // a HardwareInput-local index.
        var source = DialChannelParser.FromRotary(new SideStripRotary
        {
            LeftCommand = "Voicemeeter.StripCompDown(1,1)", RightCommand = "Voicemeeter.StripCompUp(1,1)"
        });
        Assert.Equal(new LevelSource(ChannelKind.HardwareInput, "1", LevelType.PostFaderInput, ""), source);

        using var rig = new Rig();
        rig.Api.Levels[(1, 0)] = 0.5f; // Strip 1, post-fader, left channel
        var provider = new LevelStripProvider(rig.Vm, rig.Host.Settings, rig.Host.Logger);
        using var session = (LevelStripSession)provider.CreateSession(Context(
            new SideStripRotary { Index = 0, LeftCommand = "Voicemeeter.StripCompDown(1,1)", RightCommand = "Voicemeeter.StripCompUp(1,1)" }));
        Assert.True(session.UsesDials);
        Assert.Equal("Strip 1", session.Bands[0].Name);
    }

    [Fact]
    public void Strip_RenamedVirtualInputAdjustmentOnDial_MetersItsGlobalStripNumber()
    {
        // StripEQGain1 (renamed from VirtualEQGain1) takes the global strip number too; "6" is the
        // first virtual strip on Potato (5 hardware inputs), i.e. Strip[5].
        var source = DialChannelParser.FromRotary(new SideStripRotary { PressCommand = "Voicemeeter.StripEQGain1Reset(6)" });
        Assert.Equal(new LevelSource(ChannelKind.VirtualInput, "6", LevelType.PostFaderInput, ""), source);

        using var rig = new Rig();
        var provider = new LevelStripProvider(rig.Vm, rig.Host.Settings, rig.Host.Logger);
        using var session = (LevelStripSession)provider.CreateSession(Context(
            new SideStripRotary { Index = 0, PressCommand = "Voicemeeter.StripEQGain1Reset(6)" }));
        Assert.True(session.UsesDials);
        Assert.Equal("Strip 6", session.Bands[0].Name);
    }

    [Fact]
    public void Strip_NothingBound_UsesFallbackSetting()
    {
        using var rig = new Rig();
        var provider = new LevelStripProvider(rig.Vm, rig.Host.Settings, rig.Host.Logger);
        var dials = new[] { new SideStripRotary { Index = 0 }, new SideStripRotary { Index = 1 }, new SideStripRotary { Index = 2 } };
        using (var session = (LevelStripSession)provider.CreateSession(Context(dials)))
        {
            Assert.False(session.UsesDials);
            Assert.Equal(["Strip 1", "A1", "B1"], session.Bands.Select(b => b.Name));
        }

        rig.Host.Settings.Set(LevelStripProvider.FallbackKey, "B2, 4");
        using var custom = (LevelStripSession)provider.CreateSession(Context(dials));
        Assert.Equal(["B2", "Strip 4"], custom.Bands.Select(b => b.Name));
    }

    [Fact]
    public void Strip_RaisesChangedOnlyWhenPictureChanges_AndStopsPollingOnDispose()
    {
        using var rig = new Rig();
        long now = 0;
        rig.Vm.Clock = () => now;
        rig.Api.Levels[(3, 0)] = 0.1f;
        var provider = new LevelStripProvider(rig.Vm, rig.Host.Settings, rig.Host.Logger);
        var session = provider.CreateSession(Context(new SideStripRotary { RightCommand = "Voicemeeter.BusGain(A1,1)" }));
        var changes = 0;
        session.StripChanged += (_, _) => changes++;

        rig.Vm.PollOnce();
        Assert.Equal(0, changes);
        rig.Api.Levels[(3, 0)] = 0.8f;
        rig.Vm.PollOnce();
        Assert.Equal(1, changes);
        rig.Vm.PollOnce();
        Assert.Equal(1, changes);

        session.Dispose();
        now += VoicemeeterService.LevelLeaseMs + 1;
        rig.Vm.PollOnce();
        Assert.False(rig.Vm.LevelPollingActive);
        rig.Api.Levels[(3, 0)] = 0.2f;
        rig.Vm.PollOnce();
        Assert.Equal(1, changes);
    }

    /// <summary>Counts GetLevel calls; everything else goes to the fake.</summary>
    private sealed class CountingApi(FakeVoicemeeterApi inner) : IVoicemeeterApi
    {
        public int LevelReads { get; private set; }
        public int Login() => inner.Login();
        public int Logout() => inner.Logout();
        public int GetVoicemeeterType(out int type) => inner.GetVoicemeeterType(out type);
        public int IsParametersDirty() => inner.IsParametersDirty();
        public int GetFloat(string name, out float value) => inner.GetFloat(name, out value);
        public int GetString(string name, out string value) => inner.GetString(name, out value);

        public int GetLevel(int type, int channel, out float value)
        {
            LevelReads++;
            return inner.GetLevel(type, channel, out value);
        }

        public int SetFloat(string name, float value) => inner.SetFloat(name, value);
        public int SetString(string name, string value) => inner.SetString(name, value);
        public int SetParameters(string script) => inner.SetParameters(script);
        public void Dispose() => inner.Dispose();
    }
}
