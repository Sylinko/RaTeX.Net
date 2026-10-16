using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RaTeX.Interop;

/// <summary>
/// Mirrors the native RGBA structure. Field order and widths are part of the C ABI.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeColor
{
    public float R;
    public float G;
    public float B;
    public float A;
}

/// <summary>
/// Mirrors native options. Color points to caller-owned storage for the duration of the call.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeOptions
{
    public nuint StructSize;
    public int DisplayMode;
    public NativeColor* Color;
}

/// <summary>
/// Carries a native result. A successful Data pointer must be released by FreeDisplayList.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeResult
{
    public nint Data;
    public int ErrorCode;
}

/// <summary>
/// Binds the pinned C ABI and resolves its library only from application or package locations.
/// </summary>
internal static partial class NativeMethods
{
    static NativeMethods() => NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != "ratex_ffi")
        {
            return 0;
        }

        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException("This package currently ships and validates only win-x64.");
        }

        // Only application/package locations; never download binaries or search PATH.
        var directory = Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
        foreach (var path in new[]
                 {
                     Path.Combine(directory, "runtimes", "win-x64", "native", "ratex_ffi.dll"),
                     Path.Combine(directory, "ratex_ffi.dll")
                 })
        {
            if (File.Exists(path))
            {
                return NativeLibrary.Load(path);
            }
        }
        throw new DllNotFoundException(
            "Pinned ratex_ffi.dll was not found in the application's native assets. Restore RaTeX.Net or run build/build-native.ps1.");
    }

    /// <summary>
    /// Parses NUL-terminated UTF-8 synchronously. Input and options are borrowed until the call returns.
    /// </summary>
    [LibraryImport("ratex_ffi", EntryPoint = "ratex_parse_and_layout")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial NativeResult ParseAndLayout(byte* latex, NativeOptions* options);

    /// <summary>
    /// Releases a successful result using its originating native allocator.
    /// </summary>
    [LibraryImport("ratex_ffi", EntryPoint = "ratex_free_display_list")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void FreeDisplayList(nint data);

    /// <summary>
    /// Borrows the calling thread's last UTF-8 error; copy it immediately before any further native call.
    /// </summary>
    [LibraryImport("ratex_ffi", EntryPoint = "ratex_get_last_error")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint GetLastError();
}
