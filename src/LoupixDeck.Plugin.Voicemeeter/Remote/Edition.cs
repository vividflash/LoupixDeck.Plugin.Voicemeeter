namespace LoupixDeck.Plugin.Voicemeeter.Remote;

public enum Edition
{
    Unknown = 0,
    Standard = 1,
    Banana = 2,
    Potato = 3
}

/// <summary>
/// Strip and bus layout per edition (VoicemeeterRemote.h, channel tables of VBVMR_GetLevel):
/// Standard 2 HW + 1 virtual input, buses A1 B1 (its two hardware outputs A1 / A2 share the one
/// bus A); Banana 3 + 2, A1-A3 B1-B2; Potato 5 + 3, A1-A5 B1-B3. Strips are numbered hardware inputs first, then virtual
/// inputs; buses A (physical) first, then B (virtual).
/// </summary>
internal static class EditionInfo
{
    /// <summary>Maps VBVMR_GetVoicemeeterType (1, 2, 3, and 6 = Potato x64) to an edition.</summary>
    public static Edition FromApiType(int type) => type switch
    {
        1 => Edition.Standard,
        2 => Edition.Banana,
        3 or 6 => Edition.Potato,
        _ => Edition.Unknown
    };

    public static string DisplayName(Edition e) => e switch
    {
        Edition.Standard => "Voicemeeter",
        Edition.Banana => "Voicemeeter Banana",
        Edition.Potato => "Voicemeeter Potato",
        _ => "Voicemeeter"
    };

    public static int HardwareInputs(Edition e) => e switch
    {
        Edition.Standard => 2,
        Edition.Banana => 3,
        Edition.Potato => 5,
        _ => 0
    };

    public static int VirtualInputs(Edition e) => e switch
    {
        Edition.Standard => 1,
        Edition.Banana => 2,
        Edition.Potato => 3,
        _ => 0
    };

    /// <summary>Physical output buses A1..An.</summary>
    public static int ABuses(Edition e) => e switch
    {
        Edition.Standard => 1,
        Edition.Banana => 3,
        Edition.Potato => 5,
        _ => 0
    };

    /// <summary>Virtual output buses B1..Bn.</summary>
    public static int BBuses(Edition e) => e switch
    {
        Edition.Standard => 1,
        Edition.Banana => 2,
        Edition.Potato => 3,
        _ => 0
    };

    public static int Strips(Edition e) => HardwareInputs(e) + VirtualInputs(e);

    public static int Buses(Edition e) => ABuses(e) + BBuses(e);

    /// <summary>Bus names in API index order, e.g. Potato: A1 A2 A3 A4 A5 B1 B2 B3.</summary>
    public static IReadOnlyList<string> BusNames(Edition e)
    {
        var names = new List<string>();
        for (var i = 1; i <= ABuses(e); i++) names.Add($"A{i}");
        for (var i = 1; i <= BBuses(e); i++) names.Add($"B{i}");
        return names;
    }
}
