using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LoupixDeck.Plugin.Voicemeeter.Remote;

/// <summary>
/// The real Remote API. The DLL is loaded by full path with <see cref="NativeLibrary"/> and the
/// exports are bound as delegates, so nothing depends on DllImport name resolution inside the
/// host's plugin AssemblyLoadContext.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class NativeVoicemeeterApi : IVoicemeeterApi
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int NoArgFn();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetTypeFn(out int type);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetFloatFn([MarshalAs(UnmanagedType.LPStr)] string name, out float value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetFloatFn([MarshalAs(UnmanagedType.LPStr)] string name, float value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetStringWFn([MarshalAs(UnmanagedType.LPStr)] string name, IntPtr buffer);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetStringWFn([MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetParametersWFn([MarshalAs(UnmanagedType.LPWStr)] string script);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetLevelFn(int type, int channel, out float value);

    // The API writes at most 512 wide chars; allocate double to be safe.
    private const int StringBufferChars = 1024;

    private readonly IntPtr _lib;
    private readonly NoArgFn _login, _logout, _isDirty;
    private readonly GetTypeFn _getType;
    private readonly GetFloatFn _getFloat;
    private readonly SetFloatFn _setFloat;
    private readonly GetStringWFn _getString;
    private readonly SetStringWFn _setString;
    private readonly SetParametersWFn _setParameters;
    private readonly GetLevelFn _getLevel;
    private bool _disposed;

    public string DllPath { get; }

    private NativeVoicemeeterApi(IntPtr lib, string path)
    {
        _lib = lib;
        DllPath = path;
        _login = Bind<NoArgFn>("VBVMR_Login");
        _logout = Bind<NoArgFn>("VBVMR_Logout");
        _isDirty = Bind<NoArgFn>("VBVMR_IsParametersDirty");
        _getType = Bind<GetTypeFn>("VBVMR_GetVoicemeeterType");
        _getFloat = Bind<GetFloatFn>("VBVMR_GetParameterFloat");
        _setFloat = Bind<SetFloatFn>("VBVMR_SetParameterFloat");
        _getString = Bind<GetStringWFn>("VBVMR_GetParameterStringW");
        _setString = Bind<SetStringWFn>("VBVMR_SetParameterStringW");
        _setParameters = Bind<SetParametersWFn>("VBVMR_SetParametersW");
        _getLevel = Bind<GetLevelFn>("VBVMR_GetLevel");
    }

    private T Bind<T>(string export) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_lib, export));

    /// <summary>Loads the DLL from the first candidate path that exists. Never throws.</summary>
    public static NativeVoicemeeterApi? TryLoad(out string message)
    {
        var tried = new List<string>();
        foreach (var path in DllLocator.Candidates(DllLocator.ReadRegistry, DllLocator.DllName))
        {
            tried.Add(path);
            if (!File.Exists(path)) continue;
            if (!NativeLibrary.TryLoad(path, out var lib)) continue;
            try
            {
                var api = new NativeVoicemeeterApi(lib, path);
                message = $"Loaded {path}";
                return api;
            }
            catch (Exception ex)
            {
                NativeLibrary.Free(lib);
                message = $"{path} is missing exports: {ex.Message}";
                return null;
            }
        }

        message = $"Voicemeeter is not installed ({DllLocator.DllName} not found in: {string.Join("; ", tried)})";
        return null;
    }

    public int Login() => _login();
    public int Logout() => _logout();
    public int IsParametersDirty() => _isDirty();
    public int GetVoicemeeterType(out int type) => _getType(out type);
    public int GetFloat(string name, out float value) => _getFloat(name, out value);
    public int SetFloat(string name, float value) => _setFloat(name, value);
    public int SetString(string name, string value) => _setString(name, value);
    public int SetParameters(string script) => _setParameters(script);
    public int GetLevel(int type, int channel, out float value) => _getLevel(type, channel, out value);

    public int GetString(string name, out string value)
    {
        var buffer = Marshal.AllocHGlobal(StringBufferChars * 2);
        try
        {
            Marshal.WriteInt16(buffer, 0);
            var rc = _getString(name, buffer);
            value = rc == 0 ? Marshal.PtrToStringUni(buffer) ?? string.Empty : string.Empty;
            return rc;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeLibrary.Free(_lib);
    }
}
