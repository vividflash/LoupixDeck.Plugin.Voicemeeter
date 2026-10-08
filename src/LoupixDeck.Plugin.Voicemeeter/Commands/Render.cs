using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Commands;

/// <summary>Colours of the original plugin (Helpers/ColorHelper.cs).</summary>
internal static class Palette
{
    public static readonly PluginColor Inactive = new(59, 75, 85);
    public static readonly PluginColor Active = new(82, 144, 112);
    public static readonly PluginColor Danger = new(196, 78, 61);
    public static readonly PluginColor SelActive = new(203, 174, 130);
    public static readonly PluginColor Offline = new(32, 32, 32);
    public static readonly PluginColor Text = PluginColor.White;
    public static readonly PluginColor DimText = new(160, 160, 160);
}

/// <summary>Button drawings shared by all commands (canvas is 90x90 on current devices).</summary>
internal static class Render
{
    /// <summary>Voicemeeter not running / not installed, or the channel does not exist in this edition.</summary>
    public static void Unavailable(IRenderCanvas c, string title, string reason)
    {
        c.Clear(Palette.Offline);
        c.DrawText(title, 2, 4, c.Width - 4, c.Height / 2 - 4, Palette.DimText, 13f, TextHAlign.Center, TextVAlign.Middle, bold: true);
        c.DrawText(reason, 2, c.Height / 2, c.Width - 4, c.Height / 2 - 4, Palette.DimText, 12f, TextHAlign.Center, TextVAlign.Middle);
    }

    /// <summary>On/off button: coloured background, channel label on top, function name below.</summary>
    public static void Toggle(IRenderCanvas c, string label, string name, bool on, PluginColor activeColor, PluginColor inactiveColor)
    {
        c.Clear(on ? activeColor : inactiveColor);
        if (label.Length == 0)
        {
            // Channel folder: the channel is named once, the other keys only say what they switch.
            c.DrawText(name, 2, 4, c.Width - 4, c.Height - 8, Palette.Text, 17f, TextHAlign.Center, TextVAlign.Middle, bold: true);
            return;
        }

        c.DrawText(label, 2, 4, c.Width - 4, c.Height / 2 - 4, Palette.Text, 14f, TextHAlign.Center, TextVAlign.Middle, bold: true);
        c.DrawText(name, 2, c.Height / 2, c.Width - 4, c.Height / 2 - 4, Palette.Text, 15f, TextHAlign.Center, TextVAlign.Middle);
    }

    /// <summary>Value button: label, name, value text and a horizontal bar filled to <paramref name="fraction"/>.</summary>
    public static void Bar(IRenderCanvas c, string label, string name, string valueText, float fraction, PluginColor barColor)
    {
        c.Clear(PluginColor.Black);
        var w = c.Width;
        var h = c.Height;
        if (label.Length == 0)
        {
            c.DrawText(name, 2, 8, w - 4, 26, Palette.Text, 15f, TextHAlign.Center, TextVAlign.Middle, bold: true);
        }
        else
        {
            c.DrawText(label, 2, 2, w - 4, 22, Palette.Text, 13f, TextHAlign.Center, TextVAlign.Middle, bold: true);
            c.DrawText(name, 2, 22, w - 4, 18, Palette.DimText, 11f, TextHAlign.Center, TextVAlign.Middle);
        }

        c.DrawText(valueText, 2, 40, w - 4, 24, Palette.Text, 15f, TextHAlign.Center, TextVAlign.Middle, bold: true);

        var barX = 8;
        var barY = h - 20;
        var barW = w - 16;
        const int barH = 12;
        c.DrawRectangle(barX, barY, barW, barH, 1, barColor);
        var fill = (int)MathF.Round(barW * Math.Clamp(fraction, 0f, 1f));
        if (fill > 0) c.FillRectangle(barX, barY, fill, barH, barColor);
    }
}
