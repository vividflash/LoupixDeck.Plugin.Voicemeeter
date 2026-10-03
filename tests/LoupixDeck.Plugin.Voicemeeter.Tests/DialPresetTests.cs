using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

public class DialPresetTests
{
    [Theory]
    [InlineData(Edition.Standard, 3 + 3)]
    [InlineData(Edition.Banana, 5 + 5)]
    [InlineData(Edition.Potato, 8 + 8)]
    public void One_preset_per_strip_and_bus(Edition edition, int expected)
    {
        var presets = DialPresets.Build(edition, _ => null);
        Assert.Equal(expected, presets.Count);
        Assert.Equal(presets.Count, presets.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Strip_preset_turns_gain_and_presses_mute()
    {
        var p = DialPresets.Build(Edition.Potato, c => c.ApiIndex == 1 ? "Mic" : null)[1];
        Assert.Equal("strip-2-gain-mute", p.Id);
        Assert.Equal("Strip 2: Mic – Gain + Mute", p.Name);

        var turn = p.Actions[RotaryAction.Clockwise];
        Assert.Equal("Voicemeeter.StripGain", turn.CommandName);
        Assert.Equal("2", turn.Parameters["Strip"]);
        Assert.Equal("1", turn.Parameters["Step"]);
        Assert.Same(turn, p.Actions[RotaryAction.CounterClockwise]);

        var press = p.Actions[RotaryAction.Press];
        Assert.Equal("Voicemeeter.StripMute", press.CommandName);
        Assert.Equal("2", press.Parameters["Strip"]);
    }

    [Fact]
    public void Bus_preset_uses_the_bus_name()
    {
        var p = DialPresets.Build(Edition.Potato, _ => null).Single(x => x.Id == "bus-B1-gain-mute");
        Assert.Equal("B1 – Gain + Mute", p.Name);
        Assert.Equal("Voicemeeter.BusGain", p.Actions[RotaryAction.Clockwise].CommandName);
        Assert.Equal("B1", p.Actions[RotaryAction.Clockwise].Parameters["Bus"]);
        Assert.Equal("Voicemeeter.BusMute", p.Actions[RotaryAction.Press].CommandName);
    }

    [Fact]
    public void Every_preset_command_is_registered_with_matching_parameters_and_works_on_a_dial()
    {
        using var rig = new Rig();
        foreach (var preset in DialPresets.Build(Edition.Potato, _ => null))
        foreach (var reference in preset.Actions.Values)
        {
            var command = rig.Command(reference.CommandName);
            Assert.True(command.SupportedTargets.HasFlag(ButtonTargets.RotaryEncoder));
            Assert.True(reference.Parameters.Keys.All(k => command.Descriptor.Parameters.Any(p => p.Name == k)));
        }
    }

    [Fact]
    public void Plugin_offers_presets_only_when_Voicemeeter_is_installed()
    {
        Assert.Empty(new VoicemeeterPlugin().GetDialPresets());
    }
}
