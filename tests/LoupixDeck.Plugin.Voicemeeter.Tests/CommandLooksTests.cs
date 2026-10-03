using System.Globalization;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

public class CommandLooksTests
{
    private static bool DrawsItself(IPluginCommand c) => c is IDisplayImageCommand or IAnimatedDisplayCommand;

    [Fact]
    public void Every_command_has_a_single_codepoint_icon()
    {
        using var rig = new Rig();
        foreach (var c in rig.Commands)
        {
            var icon = c.Descriptor.Icon;
            Assert.False(string.IsNullOrEmpty(icon), $"{c.Descriptor.CommandName} has no icon");
            Assert.Equal(1, new StringInfo(icon!).LengthInTextElements);
            Assert.InRange(char.ConvertToUtf32(icon!, 0), 0xF0001, 0xFFFFF);
        }
    }

    [Fact]
    public void Commands_that_draw_the_button_declare_no_layers_the_others_icon_and_caption()
    {
        using var rig = new Rig();
        foreach (var c in rig.Commands.Where(c => !c.Descriptor.HiddenFromMenu || DrawsItself(c)))
        {
            var mode = c.Descriptor.ButtonLayout?.Mode;
            Assert.Equal(DrawsItself(c) ? ButtonLayoutMode.None : ButtonLayoutMode.IconAndCaption, mode);
        }
    }
}
