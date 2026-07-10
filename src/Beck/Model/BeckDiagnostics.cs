namespace Beck.Model;

/// <summary>
/// Non-fatal render-time warnings (e.g. an untyped document). The TS engine writes
/// these to <c>console.warn</c>; here they route to an optional sink so a host can
/// surface them without the library forcing a console dependency.
/// </summary>
internal static class BeckDiagnostics
{
    /// <summary>Optional warning sink; null discards (parity: warnings never affect the model).</summary>
    public static Action<string>? OnWarning { get; set; }

    internal static void Warn(string message) => OnWarning?.Invoke(message);
}
