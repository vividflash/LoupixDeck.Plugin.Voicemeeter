namespace LoupixDeck.Plugin.Voicemeeter.Remote;

/// <summary>
/// Read side of the Voicemeeter Remote API (VoicemeeterRemote64.dll). Every method returns the
/// raw VBVMR return code: 0 = OK, -1 = error, -2 = no server (Voicemeeter not running),
/// -3 = unknown parameter, -5 = structure mismatch.
/// </summary>
internal interface IVoicemeeterReader : IDisposable
{
    /// <summary>VBVMR_Login: 0 = OK, 1 = OK but Voicemeeter not running, -1 = no client, -2 = already logged in.</summary>
    int Login();

    /// <summary>VBVMR_Logout.</summary>
    int Logout();

    /// <summary>VBVMR_GetVoicemeeterType: 1 = Voicemeeter, 2 = Banana, 3 = Potato, 6 = Potato x64.</summary>
    int GetVoicemeeterType(out int type);

    /// <summary>VBVMR_IsParametersDirty: 0 = no change, 1 = changed, &lt;0 = error / no server.
    /// Must be called from one thread only.</summary>
    int IsParametersDirty();

    /// <summary>VBVMR_GetParameterFloat, e.g. "Strip[0].Mute".</summary>
    int GetFloat(string name, out float value);

    /// <summary>VBVMR_GetParameterStringW, e.g. "Strip[0].Label".</summary>
    int GetString(string name, out string value);

    /// <summary>VBVMR_GetLevel (type: 0 pre-fader in, 1 post-fader in, 2 post-mute in, 3 output).
    /// Extra code -3 = no level available, -4 = channel out of range.</summary>
    int GetLevel(int type, int channel, out float value);
}

/// <summary>Full Remote API including writes. Only <see cref="VoicemeeterService"/> writes.</summary>
internal interface IVoicemeeterApi : IVoicemeeterReader
{
    /// <summary>VBVMR_SetParameterFloat.</summary>
    int SetFloat(string name, float value);

    /// <summary>VBVMR_SetParameterStringW.</summary>
    int SetString(string name, string value);

    /// <summary>VBVMR_SetParametersW: one or more "name=value" instructions separated by ; , or newline.</summary>
    int SetParameters(string script);
}
