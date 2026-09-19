using LoupixDeck.Plugin.Voicemeeter.Actions;
using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

public class MenuTests
{
    private static MenuNode Root(ButtonTargets target, Edition e, Func<Channel, string?>? label = null) =>
        Assert.Single(MenuBuilder.Build(target, e, Toggles.All, Adjustments.All, label ?? (_ => null)));

    [Fact]
    public void Standard_HasThreeStripsAndThreeBuses()
    {
        var root = Root(ButtonTargets.TouchButton, Edition.Standard);
        Assert.Equal("Voicemeeter", root.Name);
        Assert.Equal(["Strip 1", "Strip 2", "Strip 3"], root.Children[0].Children.Select(n => n.Name));
        Assert.Equal(["A1", "A2", "B1"], root.Children[1].Children.Select(n => n.Name));
    }

    [Fact]
    public void Dial_GetsGroupFillingAllThreeSlots()
    {
        var root = Root(ButtonTargets.RotaryEncoder, Edition.Potato);
        var b1 = root.Children[1].Children.Single(n => n.Name == "B1");
        var group = b1.Children.Single(n => n.RotaryGroup != null).RotaryGroup!;
        Assert.Equal("Voicemeeter.BusGainDown", group[RotaryAction.CounterClockwise].CommandName);
        Assert.Equal("Voicemeeter.BusGainUp", group[RotaryAction.Clockwise].CommandName);
        Assert.Equal("Voicemeeter.BusGainReset", group[RotaryAction.Press].CommandName);
        Assert.Equal("B1", group[RotaryAction.Press].Parameters["Bus"]);
    }

    [Fact]
    public void TouchTarget_HasNoDialGroup_ButHasMute()
    {
        var root = Root(ButtonTargets.TouchButton, Edition.Banana);
        var strip = root.Children[0].Children[0];
        Assert.DoesNotContain(strip.Children, n => n.RotaryGroup != null);
        var mute = strip.Children.Single(n => n.CommandName == "Voicemeeter.StripMute");
        Assert.Equal("1", mute.Parameters["Strip"]);
    }

    [Fact]
    public void Labels_AppearInChannelNames()
    {
        var root = Root(ButtonTargets.TouchButton, Edition.Potato, ch => ch.ApiIndex == 0 && ch.Kind == ChannelKind.Strip ? "Mic" : null);
        Assert.Equal("Strip 1: Mic", root.Children[0].Children[0].Name);
    }

    [Theory]
    [InlineData(ChannelKind.Strip, false, 5, "6")]
    [InlineData(ChannelKind.HardwareInput, false, 4, "5")]
    [InlineData(ChannelKind.HardwareInput, false, 5, null)]
    // Potato: 5 hardware inputs, so apiIndex 5 (first virtual strip) is global strip 6, not 1.
    [InlineData(ChannelKind.VirtualInput, false, 5, "6")]
    [InlineData(ChannelKind.VirtualInput, false, 4, null)]
    [InlineData(ChannelKind.Bus, true, 5, "B1")]
    [InlineData(ChannelKind.Strip, true, 0, null)]
    public void ParameterValue_PerKind(ChannelKind kind, bool isBus, int apiIndex, string? expected) =>
        Assert.Equal(expected, MenuBuilder.ParameterValue(kind, isBus, apiIndex, Edition.Potato));
}
