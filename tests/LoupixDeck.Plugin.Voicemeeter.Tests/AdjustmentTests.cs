using LoupixDeck.Plugin.Voicemeeter.Actions;
using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

/// <summary>The knob command as the host drives it: IAdjustmentCommand on a dial, Execute elsewhere.</summary>
public class AdjustmentTests
{
    private static IAdjustmentCommand Knob(Rig rig, string name) => (IAdjustmentCommand)rig.Command(name);

    [Fact]
    public void EverySpec_IsOneAdjustmentAndDisplayCommand()
    {
        using var rig = new Rig();
        foreach (var spec in Adjustments.All)
        {
            var cmd = rig.Command(spec.CommandName);
            Assert.IsAssignableFrom<IAdjustmentCommand>(cmd);
            Assert.IsAssignableFrom<IDisplayImageCommand>(cmd);
            Assert.False(cmd.Descriptor.HiddenFromMenu);
            Assert.Equal("Step", cmd.Descriptor.Parameters[1].Name);
        }
    }

    [Fact]
    public async Task TurnUp_AddsStepPerTick_AndClampsAtMax()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Gain"] = 8f;
        await rig.Turn("Voicemeeter.StripGain", 3, "1");
        Assert.Equal(("Strip[0].Gain", 11f), rig.Api.Writes[^1]);
        await rig.Turn("Voicemeeter.StripGain", 3, "1");
        Assert.Equal(("Strip[0].Gain", 12f), rig.Api.Writes[^1]);
        var writes = rig.Api.Writes.Count;
        await rig.Turn("Voicemeeter.StripGain", 1, "1");
        Assert.Equal(writes, rig.Api.Writes.Count); // already at max: no write
    }

    [Fact]
    public async Task TurnDown_ClampsAtMin()
    {
        using var rig = new Rig();
        rig.Api.Floats["Bus[0].Gain"] = -59f;
        await rig.Turn("Voicemeeter.BusGain", -2, "A1");
        Assert.Equal(("Bus[0].Gain", -60f), rig.Api.Writes[^1]);
        var writes = rig.Api.Writes.Count;
        await rig.Turn("Voicemeeter.BusGain", -1, "A1");
        Assert.Equal(writes, rig.Api.Writes.Count);
    }

    [Fact]
    public async Task StepParameter_OverridesSpecStep()
    {
        using var rig = new Rig();
        await rig.Turn("Voicemeeter.StripGain", 1, "2");
        Assert.Equal(("Strip[1].Gain", 1f), rig.Api.Writes[^1]);
        await rig.Turn("Voicemeeter.StripGain", -3, "2", "0.5");
        Assert.Equal(("Strip[1].Gain", -0.5f), rig.Api.Writes[^1]);
        await rig.Turn("Voicemeeter.StripGain", 1, "2", "x"); // unparsable: spec step
        Assert.Equal(("Strip[1].Gain", 0.5f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task Press_Resets_OnDialAndOnTouchButton()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Gain"] = -20f;
        await rig.Press("Voicemeeter.StripGain", "1");
        Assert.Equal(("Strip[0].Gain", 0f), rig.Api.Writes[^1]);

        rig.Api.Floats["Strip[0].Gain"] = 6f;
        rig.Api.Dirty = true;
        rig.Vm.PollOnce();
        await rig.Run("Voicemeeter.StripGain", "1", "0.5");
        Assert.Equal(("Strip[0].Gain", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public void GetValue_FractionOfEffectiveRange_AndText()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Gain"] = -24f;
        var gain = Knob(rig, "Voicemeeter.StripGain");
        var value = gain.GetValue(rig.DialCtx(0, "1"));
        Assert.Equal(new AdjustmentValue(0.5, "-24.0 dB"), value);
        Assert.Equal("-24.0 dB", gain.GetValueText(rig.DialCtx(0, "1")));

        // StripPanY: hardware 0..1, virtual -0.5..0.5 (strip 6 is the first virtual one on Potato).
        rig.Api.Floats["Strip[0].Pan_y"] = 0.25f;
        rig.Api.Floats["Strip[5].Pan_y"] = 0.25f;
        var pan = Knob(rig, "Voicemeeter.StripPanY");
        Assert.Equal(0.25, pan.GetValue(rig.DialCtx(0, "1"))!.Value.Normalized, 3);
        Assert.Equal(0.75, pan.GetValue(rig.DialCtx(0, "6"))!.Value.Normalized, 3);
    }

    [Fact]
    public async Task GetValue_FollowsTurn()
    {
        using var rig = new Rig();
        var gain = Knob(rig, "Voicemeeter.BusGain");
        Assert.Equal("0.0 dB", gain.GetValue(rig.DialCtx(0, "A1"))!.Value.Text);
        await rig.Turn("Voicemeeter.BusGain", -6, "A1");
        Assert.Equal("-6.0 dB", gain.GetValue(rig.DialCtx(0, "A1"))!.Value.Text);
        Assert.Contains("Voicemeeter.BusGain", rig.Host.Refreshes);
    }

    [Fact]
    public async Task Offline_GetValueNull_NoWrite()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 0;
        using var rig = new Rig(api);
        var gain = Knob(rig, "Voicemeeter.StripGain");
        Assert.Null(gain.GetValue(rig.DialCtx(0, "1")));
        Assert.Null(gain.GetValueText(rig.DialCtx(0, "1")));
        await rig.Turn("Voicemeeter.StripGain", 1, "1");
        await rig.Press("Voicemeeter.StripGain", "1");
        Assert.Empty(api.Writes);
    }

    [Fact]
    public void GetValue_Null_WhenChannelDoesNotResolve()
    {
        using var rig = new Rig();
        Assert.Null(Knob(rig, "Voicemeeter.StripGain").GetValue(rig.DialCtx(0, "abc")));
        Assert.Null(Knob(rig, "Voicemeeter.StripGain").GetValue(rig.DialCtx(0)));
        Assert.Null(Knob(rig, "Voicemeeter.StripComp").GetValue(rig.DialCtx(0, "6"))); // virtual strip
        Assert.NotNull(Knob(rig, "Voicemeeter.StripGain").GetValue(rig.DialCtx(0, "1")));
    }

    [Fact]
    public async Task OldCommands_StayRegistered_Hidden_AndWork()
    {
        using var rig = new Rig();
        foreach (var spec in Adjustments.All)
        {
            foreach (var name in new[] { spec.DownName, spec.UpName, spec.ResetName })
            {
                var cmd = rig.Command(name);
                Assert.True(cmd.Descriptor.HiddenFromMenu);
                Assert.False(cmd is IAdjustmentCommand);
            }
        }

        await rig.Run("Voicemeeter.StripGainDown", "1", "2");
        Assert.Equal(("Strip[0].Gain", -2f), rig.Api.Writes[^1]);
        await rig.Run("Voicemeeter.StripGainUp", "1");
        Assert.Equal(("Strip[0].Gain", -1f), rig.Api.Writes[^1]);
        await rig.Run("Voicemeeter.StripGainReset", "1");
        Assert.Equal(("Strip[0].Gain", 0f), rig.Api.Writes[^1]);
        Assert.Equal(["Strip 1", "Gain", "0.0 dB"], rig.Render("Voicemeeter.StripGainReset", "1").Texts);
    }

    [Fact]
    public void Migrations_OneStableUniqueIdPerRule()
    {
        using var rig = new Rig();
        var rules = new VoicemeeterPlugin().GetCommandMigrations().ToList();
        Assert.Equal(Adjustments.All.Count * 2, rules.Count);
        Assert.All(rules, r => Assert.False(string.IsNullOrWhiteSpace(r.Id)));
        Assert.Equal(rules.Count, rules.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Adjustments.All.SelectMany(s => new[] { $"{s.Name}-dial", $"{s.Name}-turn" }),
            rules.Select(r => r.Id));
        // Recorded in user configs once applied: these exact strings must never change.
        Assert.Equal(["StripGain-dial", "StripGain-turn", "BusGain-dial", "BusGain-turn"], rules.Take(4).Select(r => r.Id));
    }

    [Fact]
    public void Migrations_PointAtRegisteredCommandsAndTheirParameters()
    {
        using var rig = new Rig();
        foreach (var rule in new VoicemeeterPlugin().GetCommandMigrations())
        {
            var target = rig.Command(rule.To);
            Assert.IsAssignableFrom<IAdjustmentCommand>(target);

            // The host reads the old values by the old commands' parameter names, so those
            // commands must be registered and declare every "{name}" the rule carries over.
            var oldNames = rule.From.Values.SelectMany(n => rig.Command(n).Descriptor.Parameters.Select(p => p.Name)).ToHashSet();
            foreach (var (name, source) in rule.Parameters)
            {
                Assert.Contains(target.Descriptor.Parameters, p => p.Name == name);
                Assert.Equal($"{{{name}}}", source);
                Assert.Contains(name, oldNames);
            }
        }
    }

    [Fact]
    public void Migrations_StripGainShapes()
    {
        var rules = new VoicemeeterPlugin().GetCommandMigrations().ToDictionary(r => r.Id);
        var dial = rules["StripGain-dial"];
        Assert.Equal("Voicemeeter.StripGain", dial.To);
        Assert.Equal("Voicemeeter.StripGainDown", dial.From[RotaryAction.CounterClockwise]);
        Assert.Equal("Voicemeeter.StripGainUp", dial.From[RotaryAction.Clockwise]);
        Assert.Equal("Voicemeeter.StripGainReset", dial.From[RotaryAction.Press]);
        Assert.Equal("{Strip}", dial.Parameters["Strip"]);
        Assert.Equal("{Step}", dial.Parameters["Step"]);

        var turn = rules["StripGain-turn"];
        Assert.Equal(2, turn.From.Count);
        Assert.False(turn.From.ContainsKey(RotaryAction.Press));
        Assert.Equal("{Bus}", rules["BusGain-dial"].Parameters["Bus"]);
    }
}