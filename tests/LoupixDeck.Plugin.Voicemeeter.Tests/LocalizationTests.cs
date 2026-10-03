using System.Text.Json;
using System.Text.RegularExpressions;
using LoupixDeck.Plugin.Voicemeeter.Actions;
using LoupixDeck.Plugin.Voicemeeter.Commands;
using LoupixDeck.Plugin.Voicemeeter.Remote;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Voicemeeter.Tests;

public class LocalizationTests : IDisposable
{
    private static readonly Regex TrCall = new(
        @"Localization\.Tr\(\s*""((?:[^""\\]|\\.)*)""\s*\)", RegexOptions.Compiled);

    public LocalizationTests() => Localization.ResetForTests();
    public void Dispose() => Localization.ResetForTests();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "LoupixDeck.Plugin.Voicemeeter.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }

    private static string SrcDir => Path.Combine(RepoRoot(), "src", "LoupixDeck.Plugin.Voicemeeter");

    private static Dictionary<string, string> Load(string code) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(SrcDir, $"strings.{code}.json")))!;

    private static void AssertTranslated(IEnumerable<string?> texts, string what)
    {
        var de = Load("de");
        var es = Load("es");
        foreach (var text in texts.Where(t => !string.IsNullOrEmpty(t)))
        {
            Assert.True(de.ContainsKey(text!), $"strings.de.json is missing ({what}): {text}");
            Assert.True(es.ContainsKey(text!), $"strings.es.json is missing ({what}): {text}");
        }
    }

    [Fact]
    public void German_and_Spanish_have_identical_keys()
    {
        var de = Load("de");
        var es = Load("es");
        Assert.Empty(de.Keys.Except(es.Keys));
        Assert.Empty(es.Keys.Except(de.Keys));
        Assert.All(de.Values.Concat(es.Values), v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }

    [Fact]
    public void Every_command_display_name_and_description_has_a_key()
    {
        using var rig = new Rig();
        AssertTranslated(rig.Commands.Select(c => c.Descriptor.DisplayName), "display name");
        AssertTranslated(rig.Commands.Select(c => c.Descriptor.Description), "description");
        AssertTranslated(rig.Commands.Select(c => c.Descriptor.Group), "group");
    }

    [Theory]
    [InlineData(Edition.Standard)]
    [InlineData(Edition.Banana)]
    [InlineData(Edition.Potato)]
    public void Every_menu_title_has_a_key(Edition edition)
    {
        var asked = new List<string>();
        MenuBuilder.Build(edition, Toggles.All, Adjustments.All, _ => null, s => { asked.Add(s); return s; });
        Assert.NotEmpty(asked);
        AssertTranslated(asked, "menu title");
    }

    [Fact]
    public void Settings_group_metadata_and_manifest_texts_have_keys()
    {
        var plugin = new VoicemeeterPlugin();
        AssertTranslated(plugin.SettingsSchema.SelectMany(s => new[] { s.Label, s.Description }), "setting");
        AssertTranslated(plugin.GetCommandGroups().SelectMany(g => new[] { g.Group, g.Description }), "command group");
        AssertTranslated([plugin.Metadata.Description], "metadata");

        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(SrcDir, "plugin.json")));
        AssertTranslated([manifest.RootElement.GetProperty("description").GetString()], "plugin.json");
    }

    [Fact]
    public void Requirement_texts_have_keys()
    {
        var req = Assert.Single(VoicemeeterPlugin.BuildRequirements(ConnectionState.NotInstalled));
        AssertTranslated([req.Name, req.Message, req.InstallHint], "requirement");
        // Both DLL names the message can carry.
        AssertTranslated(
        [
            "Voicemeeter is not installed (VoicemeeterRemote64.dll not found).",
            "Voicemeeter is not installed (VoicemeeterRemote.dll not found).",
            "Voicemeeter is Windows only."
        ], "requirement");
    }

    [Fact]
    public void Connection_status_texts_have_keys()
    {
        var statuses = new List<string> { new VoicemeeterPlugin().SettingsSchema[0].Label };
        foreach (var type in new[] { 1, 2, 3 })
        {
            using var rig = new Rig(new FakeVoicemeeterApi { RunningType = type });
            statuses.Add(rig.Vm.Status);
        }

        using (var rig = new Rig(new FakeVoicemeeterApi { LoginResult = -1 })) statuses.Add(rig.Vm.Status);
        using (var rig = new Rig(new FakeVoicemeeterApi { LoginResult = 1, RunningType = 0 })) statuses.Add(rig.Vm.Status);
        AssertTranslated(statuses, "status");
    }

    [Fact]
    public void Every_literal_Tr_call_in_source_has_a_key()
    {
        var keys = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(SrcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            foreach (Match m in TrCall.Matches(File.ReadAllText(file))) keys.Add(m.Groups[1].Value);
        }

        Assert.NotEmpty(keys);
        AssertTranslated(keys, "Localization.Tr call");
    }

    [Fact]
    public void No_host_returns_english_unchanged() => Assert.Equal("offline", Localization.Tr("offline"));

    [Fact]
    public async Task Toggle_overlay_and_unavailable_texts_go_through_the_host()
    {
        using var rig = new Rig();
        rig.Host.Translate = s => "[DE] " + s;
        Localization.SetHost(rig.Host);

        await rig.Command("Voicemeeter.StripMute").Execute(rig.DialCtx(0, "1"));
        Assert.Matches(@"\[DE\] o(n|ff)$", rig.Host.Overlays.Last().Text);

        using var offline = new VoicemeeterService(null, "x", rig.Host.Logger, rig.Host.RequestButtonRefresh);
        var cmd = (IDisplayImageCommand)VoicemeeterPlugin.BuildCommands(offline, rig.Host.Logger).First();
        var canvas = new FakeCanvas();
        Assert.True(cmd.RenderImage(rig.Ctx("1"), canvas));
        Assert.Contains("[DE] not installed", canvas.Texts);
    }
}
