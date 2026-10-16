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
/// Binds the pinned C ABI using the runtime's native asset resolution.
/// </summary>
internal static partial class NativeMethods
{
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
