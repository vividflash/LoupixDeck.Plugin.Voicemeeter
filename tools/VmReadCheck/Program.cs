// Read-only live check. Logs in to the Voicemeeter Remote API, reads edition, strip/bus
// mute + gain + labels and level samples, then logs out. The API object is held as
// IVoicemeeterReader, which has no write methods, so this tool cannot change Voicemeeter.
using System.Globalization;
using LoupixDeck.Plugin.Voicemeeter.Remote;

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("Windows only.");
    return 1;
}

IVoicemeeterReader? vm = NativeVoicemeeterApi.TryLoad(out var message);
Console.WriteLine(message);
if (vm == null) return 1;

using (vm)
{
    var login = vm.Login();
    Console.WriteLine($"Login: {login}");
    if (login is not (0 or 1))
    {
        Console.WriteLine("Login failed, nothing read.");
        if (login == -2) vm.Logout();
        return 1;
    }

    try
    {
        // Let the client pick up the current state (VB recommends polling IsParametersDirty after login).
        for (var i = 0; i < 10; i++)
        {
            vm.IsParametersDirty();
            Thread.Sleep(50);
        }

        var rc = vm.GetVoicemeeterType(out var type);
        var edition = EditionInfo.FromApiType(type);
        Console.WriteLine($"GetVoicemeeterType: rc={rc} type={type} -> {EditionInfo.DisplayName(edition)} ({edition})");
        if (edition == Edition.Unknown) return 1;

        string F(string name) => vm.GetFloat(name, out var v) == 0 ? v.ToString("0.0", CultureInfo.InvariantCulture) : "err";
        string S(string name) => vm.GetString(name, out var s) == 0 ? s : "err";

        Console.WriteLine($"Strips: {EditionInfo.Strips(edition)}, buses: {EditionInfo.Buses(edition)} ({string.Join(" ", EditionInfo.BusNames(edition))})");
        for (var i = 0; i < EditionInfo.Strips(edition); i++)
            Console.WriteLine($"  Strip[{i}] label='{S($"Strip[{i}].Label")}' device='{S($"Strip[{i}].device.name")}' Mute={F($"Strip[{i}].Mute")} Gain={F($"Strip[{i}].Gain")}");
        var busNames = EditionInfo.BusNames(edition);
        for (var i = 0; i < EditionInfo.Buses(edition); i++)
            Console.WriteLine($"  Bus[{i}] ({busNames[i]}) label='{S($"Bus[{i}].Label")}' Mute={F($"Bus[{i}].Mute")} Gain={F($"Bus[{i}].Gain")}");

        var lrc = vm.GetLevel(3, 0, out var level);
        Console.WriteLine($"Level output ch0: rc={lrc} value={level.ToString("0.0000", CultureInfo.InvariantCulture)}");

        // Level index map check (GetLevel only): last valid channel and first index past the end per type,
        // then the peak per strip / bus as LevelMap groups them.
        string Db(float v) => LevelMap.FormatDb(LevelMap.ToDb(v));
        foreach (var (levelType, total) in new[] { (1, LevelMap.InputChannels(edition)), (3, LevelMap.OutputChannels(edition)) })
        {
            var last = vm.GetLevel(levelType, total - 1, out _);
            var past = vm.GetLevel(levelType, total, out _);
            Console.WriteLine($"Level type {levelType}: {total} channels expected, rc[{total - 1}]={last} rc[{total}]={past}");
        }

        for (var i = 0; i < EditionInfo.Strips(edition); i++)
        {
            var (first, count) = LevelMap.Range(LevelType.PostFaderInput, new Channel(ChannelKind.Strip, i, $"Strip {i + 1}"), edition);
            var values = Enumerable.Range(first, count).Select(c => vm.GetLevel(1, c, out var v) == 0 ? v : -1f).ToArray();
            Console.WriteLine($"  Strip[{i}] post-fader ch {first}..{first + count - 1}: peak {Db(values.Max())}");
        }

        for (var i = 0; i < EditionInfo.Buses(edition); i++)
        {
            var (first, count) = LevelMap.Range(LevelType.Output, new Channel(ChannelKind.Bus, i, busNames[i]), edition);
            var values = Enumerable.Range(first, count).Select(c => vm.GetLevel(3, c, out var v) == 0 ? v : -1f).ToArray();
            Console.WriteLine($"  Bus[{i}] ({busNames[i]}) output ch {first}..{first + count - 1}: peak {Db(values.Max())}");
        }
        return 0;
    }
    finally
    {
        Console.WriteLine($"Logout: {vm.Logout()}");
    }
}
