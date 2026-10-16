namespace RaTeX;

/// <summary>
/// Preserves a native error independently of subsequent calls and the native thread-local buffer.
/// </summary>
/// <param name="NativeCode">The nonzero error code returned by the native engine.</param>
/// <param name="NativeMessage">A managed copy of the native message, if one was provided.</param>
public sealed record RatexError(int NativeCode, string? NativeMessage);

/// <summary>
/// Reports a native parse or layout failure.
/// </summary>
/// <param name="error">The copied native error.</param>
public sealed class RatexException(RatexError error)
    : Exception(error.NativeMessage ?? $"RaTeX failed with native code {error.NativeCode}.")
{
    /// <summary>
    /// Gets the native code and message captured for this operation.
    /// </summary>
    public RatexError Error { get; } = error;
}

/// <summary>
/// Reports a native response that cannot be safely interpreted by this binding.
/// </summary>
/// <param name="message">A description of the protocol violation.</param>
/// <param name="innerException">The decoding failure that caused this exception, if any.</param>
public sealed class RatexProtocolException(string message, Exception? innerException = null)
    : Exception(message, innerException);
