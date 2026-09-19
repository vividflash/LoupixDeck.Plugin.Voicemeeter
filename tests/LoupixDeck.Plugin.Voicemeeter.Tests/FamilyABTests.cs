using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

/// <summary>
/// Stage 2 family A (Actions/Toggles.cs) and family B (Actions/Adjustments.cs) actions.
/// One test file per PORTING.md, covering all rows moved from todo to ported in this pass.
/// </summary>
public class FamilyABTests
{
    // ---- Family A: toggles ----

    [Fact]
    public async Task StripSolo_Toggles()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Solo"] = 0;
        await rig.Run("Voicemeeter.StripSolo", "1");
        Assert.Equal([("Strip[0].Solo", 1f)], rig.Api.Writes);
        Assert.Equal(Palette.Active, rig.Render("Voicemeeter.StripSolo", "1").Background);
    }

    [Fact]
    public async Task StripA_WritesSubChannelField()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[1].A3"] = 0;
        await rig.Run("Voicemeeter.StripA", "2", "3");
        Assert.Equal([("Strip[1].A3", 1f)], rig.Api.Writes);
    }

    [Fact]
    public async Task StripA_SubChannelOutOfRangeForEdition_NoWrite()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2; // Banana: A1-A3 only
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripA", "1", "4");
        Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task StripB_WritesSubChannelField()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].B2"] = 0;
        await rig.Run("Voicemeeter.StripB", "1", "2");
        Assert.Equal([("Strip[0].B2", 1f)], rig.Api.Writes);
    }

    [Fact]
    public async Task StripMono_Toggles()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Mono"] = 0;
        await rig.Run("Voicemeeter.StripMono", "1");
        Assert.Equal([("Strip[0].Mono", 1f)], rig.Api.Writes);
    }

    [Fact]
    public async Task StripMono_NoWrite_DrawsNa_OnVirtualStrip()
    {
        // Potato: 5 hardware inputs, so global strip 6 is the first virtual input.
        using var rig = new Rig();
        rig.Api.Floats["Strip[5].Mono"] = 0;
        await rig.Run("Voicemeeter.StripMono", "6");
        Assert.Empty(rig.Api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripMono", "6").Texts);
    }

    [Fact]
    public async Task StripPostReverb_Toggles_OnPotato()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].PostReverb"] = 0;
        await rig.Run("Voicemeeter.StripPostReverb", "1");
        Assert.Equal([("Strip[0].PostReverb", 1f)], rig.Api.Writes);
    }

    [Fact]
    public async Task StripPostReverb_NoWrite_DrawsNa_OnBanana()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2; // Banana
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripPostReverb", "1");
        Assert.Empty(api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripPostReverb", "1").Texts);
    }

    [Fact]
    public async Task StripPostDelay_Toggles_OnPotato()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].PostDelay"] = 0;
        await rig.Run("Voicemeeter.StripPostDelay", "1");
        Assert.Equal([("Strip[0].PostDelay", 1f)], rig.Api.Writes);
    }

    [Fact]
    public async Task StripPostDelay_NoWrite_DrawsNa_OnBanana()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripPostDelay", "1");
        Assert.Empty(api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripPostDelay", "1").Texts);
    }

    [Fact]
    public async Task StripPostFx1_Toggles_OnPotato()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].PostFx1"] = 0;
        await rig.Run("Voicemeeter.StripPostFx1", "1");
        Assert.Equal([("Strip[0].PostFx1", 1f)], rig.Api.Writes);
    }

    [Fact]
    public async Task StripPostFx1_NoWrite_DrawsNa_OnBanana()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripPostFx1", "1");
        Assert.Empty(api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripPostFx1", "1").Texts);
    }

    [Fact]
    public async Task StripPostFx2_Toggles_OnPotato()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].PostFx2"] = 0;
        await rig.Run("Voicemeeter.StripPostFx2", "1");
        Assert.Equal([("Strip[0].PostFx2", 1f)], rig.Api.Writes);
    }

    [Fact]
    public async Task StripPostFx2_NoWrite_DrawsNa_OnBanana()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripPostFx2", "1");
        Assert.Empty(api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripPostFx2", "1").Texts);
    }

    [Fact]
    public async Task BusMono_Toggles()
    {
        using var rig = new Rig();
        rig.Api.Floats["Bus[0].Mono"] = 0;
        await rig.Run("Voicemeeter.BusMono", "1");
        Assert.Equal([("Bus[0].Mono", 1f)], rig.Api.Writes);
    }

    [Fact]
    public async Task BusEQ_TogglesEqDotOnField_ButtonTextIsEQ()
    {
        using var rig = new Rig();
        rig.Api.Floats["Bus[0].EQ.on"] = 0;
        await rig.Run("Voicemeeter.BusEQ", "1");
        Assert.Equal([("Bus[0].EQ.on", 1f)], rig.Api.Writes);
        Assert.Contains("EQ", rig.Render("Voicemeeter.BusEQ", "1").Texts);
    }

    [Fact]
    public async Task BusSel_Toggles_ActiveColorIsSelActive()
    {
        using var rig = new Rig();
        rig.Api.Floats["Bus[0].Sel"] = 0;
        await rig.Run("Voicemeeter.BusSel", "1");
        Assert.Equal([("Bus[0].Sel", 1f)], rig.Api.Writes);
        Assert.Equal(Palette.SelActive, rig.Render("Voicemeeter.BusSel", "1").Background);
    }

    // ---- Family B: adjustments ----

    [Fact]
    public async Task StripComp_ClampsAtMax()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Comp"] = 9.5f;
        await rig.Run("Voicemeeter.StripCompUp", "1");
        Assert.Equal(("Strip[0].Comp", 10f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripComp_ClampsAtMin()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Comp"] = 0.5f;
        await rig.Run("Voicemeeter.StripCompDown", "1");
        Assert.Equal(("Strip[0].Comp", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripComp_Reset()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Comp"] = 5f;
        await rig.Run("Voicemeeter.StripCompReset", "1");
        Assert.Equal(("Strip[0].Comp", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripComp_NoWrite_DrawsNa_OnVirtualStrip()
    {
        // Potato: 5 hardware inputs, so global strip 6 is the first virtual input.
        using var rig = new Rig();
        rig.Api.Floats["Strip[5].Comp"] = 5f;
        await rig.Run("Voicemeeter.StripCompUp", "6");
        Assert.Empty(rig.Api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripCompReset", "6").Texts);
    }

    [Fact]
    public async Task StripGate_ClampsAtMax()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Gate"] = 9.5f;
        await rig.Run("Voicemeeter.StripGateUp", "1");
        Assert.Equal(("Strip[0].Gate", 10f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripGate_ClampsAtMin()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Gate"] = 0.5f;
        await rig.Run("Voicemeeter.StripGateDown", "1");
        Assert.Equal(("Strip[0].Gate", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripReverb_ClampsAtMax_OnPotato()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Reverb"] = 9.5f;
        await rig.Run("Voicemeeter.StripReverbUp", "1");
        Assert.Equal(("Strip[0].Reverb", 10f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripReverb_NoWrite_DrawsNa_OnBanana()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripReverbUp", "1");
        Assert.Empty(api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripReverbReset", "1").Texts);
    }

    [Fact]
    public async Task StripDelay_ClampsAtMin_OnPotato()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Delay"] = 0.5f;
        await rig.Run("Voicemeeter.StripDelayDown", "1");
        Assert.Equal(("Strip[0].Delay", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripDelay_NoWrite_DrawsNa_OnBanana()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripDelayUp", "1");
        Assert.Empty(api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripDelayReset", "1").Texts);
    }

    [Fact]
    public async Task StripFx1_ClampsAtMax_OnPotato()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Fx1"] = 9.5f;
        await rig.Run("Voicemeeter.StripFx1Up", "1");
        Assert.Equal(("Strip[0].Fx1", 10f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripFx1_NoWrite_DrawsNa_OnBanana()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripFx1Up", "1");
        Assert.Empty(api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripFx1Reset", "1").Texts);
    }

    [Fact]
    public async Task StripFx2_ClampsAtMin_OnPotato()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Fx2"] = 0.5f;
        await rig.Run("Voicemeeter.StripFx2Down", "1");
        Assert.Equal(("Strip[0].Fx2", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripFx2_NoWrite_DrawsNa_OnBanana()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripFx2Down", "1");
        Assert.Empty(api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripFx2Reset", "1").Texts);
    }

    [Fact]
    public async Task StripPanX_ClampsAtMax()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Pan_x"] = 0.45f;
        await rig.Run("Voicemeeter.StripPanXUp", "1");
        Assert.Equal(("Strip[0].Pan_x", 0.5f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripPanX_ClampsAtMin()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Pan_x"] = -0.45f;
        await rig.Run("Voicemeeter.StripPanXDown", "1");
        Assert.Equal(("Strip[0].Pan_x", -0.5f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripPanX_Reset()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Pan_x"] = 0.3f;
        await rig.Run("Voicemeeter.StripPanXReset", "1");
        Assert.Equal(("Strip[0].Pan_x", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripPanY_ClampsAtMax_OnHardwareStrip()
    {
        // Global strip 1 is hardware on every edition: range is 0..1.
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Pan_y"] = 0.95f;
        await rig.Run("Voicemeeter.StripPanYUp", "1");
        Assert.Equal(("Strip[0].Pan_y", 1f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripPanY_ClampsAtMin_OnHardwareStrip()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Pan_y"] = 0.05f;
        await rig.Run("Voicemeeter.StripPanYDown", "1");
        Assert.Equal(("Strip[0].Pan_y", 0f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripPanY_ClampsAtMax_OnVirtualStrip()
    {
        // Potato: 5 hardware inputs, so global strip 6 is the first virtual input: range is -0.5..0.5.
        using var rig = new Rig();
        rig.Api.Floats["Strip[5].Pan_y"] = 0.45f;
        await rig.Run("Voicemeeter.StripPanYUp", "6");
        Assert.Equal(("Strip[5].Pan_y", 0.5f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripPanY_ClampsAtMin_OnVirtualStrip()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[5].Pan_y"] = -0.45f;
        await rig.Run("Voicemeeter.StripPanYDown", "6");
        Assert.Equal(("Strip[5].Pan_y", -0.5f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripPanY_AppliesToEveryStripKind_NoRestriction()
    {
        // Unlike StripComp/StripEQGain1, StripPanY has no applicability restriction: both a
        // hardware strip (1) and a virtual one (6) accept the command; only the range differs.
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].Pan_y"] = 0f;
        rig.Api.Floats["Strip[5].Pan_y"] = 0f;
        await rig.Run("Voicemeeter.StripPanYReset", "1");
        await rig.Run("Voicemeeter.StripPanYReset", "6");
        Assert.DoesNotContain("n/a", rig.Render("Voicemeeter.StripPanYReset", "1").Texts);
        Assert.DoesNotContain("n/a", rig.Render("Voicemeeter.StripPanYReset", "6").Texts);
    }

    [Fact]
    public async Task StripEQGain1_ClampsAtMax_UsesGlobalStripNumber()
    {
        // Potato: 5 hardware inputs, so global strip 6 is the first virtual input -> Strip[5].
        using var rig = new Rig();
        rig.Api.Floats["Strip[5].EQGain1"] = 11.5f;
        await rig.Run("Voicemeeter.StripEQGain1Up", "6");
        Assert.Equal(("Strip[5].EQGain1", 12f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripEQGain1_NoWrite_DrawsNa_OnHardwareStrip()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[0].EQGain1"] = 0f;
        await rig.Run("Voicemeeter.StripEQGain1Up", "1");
        Assert.Empty(rig.Api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripEQGain1Reset", "1").Texts);
    }

    [Fact]
    public async Task StripEQGain2_ClampsAtMin()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[5].EQGain2"] = -11.5f;
        await rig.Run("Voicemeeter.StripEQGain2Down", "6");
        Assert.Equal(("Strip[5].EQGain2", -12f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripEQGain3_ClampsAtMax_OnPotato()
    {
        using var rig = new Rig();
        rig.Api.Floats["Strip[5].EQGain3"] = 11.5f;
        await rig.Run("Voicemeeter.StripEQGain3Up", "6");
        Assert.Equal(("Strip[5].EQGain3", 12f), rig.Api.Writes[^1]);
    }

    [Fact]
    public async Task StripEQGain3_NoWrite_DrawsNa_OnBanana()
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2; // Banana
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripEQGain3Up", "6");
        Assert.Empty(api.Writes);
        Assert.Contains("n/a", rig.Render("Voicemeeter.StripEQGain3Reset", "6").Texts);
    }

    // ---- Global strip numbering: hardware and virtual strips on Potato and Banana ----

    [Theory]
    [InlineData("1", "Strip[0].Comp")] // hardware strip 1
    [InlineData("5", "Strip[4].Comp")] // last hardware strip on Potato
    public async Task StripComp_GlobalNumber_MapsToApiIndex_OnPotato(string strip, string param)
    {
        using var rig = new Rig();
        rig.Api.Floats[param] = 5f;
        await rig.Run("Voicemeeter.StripCompUp", strip);
        Assert.Equal((param, 6f), rig.Api.Writes[^1]);
    }

    [Theory]
    [InlineData("6", "Strip[5].EQGain1")] // first virtual strip on Potato (5 hardware inputs)
    [InlineData("8", "Strip[7].EQGain1")] // last virtual strip on Potato
    public async Task StripEQGain1_GlobalNumber_MapsToApiIndex_OnPotato(string strip, string param)
    {
        using var rig = new Rig();
        rig.Api.Floats[param] = 0f;
        await rig.Run("Voicemeeter.StripEQGain1Up", strip);
        Assert.Equal((param, 1f), rig.Api.Writes[^1]);
    }

    [Theory]
    [InlineData("1", "Strip[0].Comp")] // hardware strip 1
    [InlineData("3", "Strip[2].Comp")] // last hardware strip on Banana
    public async Task StripComp_GlobalNumber_MapsToApiIndex_OnBanana(string strip, string param)
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2; // Banana: 3 hardware inputs, 2 virtual
        api.Floats[param] = 5f;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripCompUp", strip);
        Assert.Equal((param, 6f), rig.Api.Writes[^1]);
    }

    [Theory]
    [InlineData("4", "Strip[3].EQGain1")] // first virtual strip on Banana (3 hardware inputs)
    [InlineData("5", "Strip[4].EQGain1")] // last virtual strip on Banana
    public async Task StripEQGain1_GlobalNumber_MapsToApiIndex_OnBanana(string strip, string param)
    {
        var api = FakeVoicemeeterApi.Potato();
        api.RunningType = 2; // Banana
        api.Floats[param] = 0f;
        using var rig = new Rig(api);
        await rig.Run("Voicemeeter.StripEQGain1Up", strip);
        Assert.Equal((param, 1f), rig.Api.Writes[^1]);
    }
}
