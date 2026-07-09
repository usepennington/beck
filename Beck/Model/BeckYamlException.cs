namespace Beck.Rendering;

/// <summary>
/// An error with an author-friendly message about their diagram source. Mirrors
/// the TypeScript engine's <c>BeckError</c>: the message carries an optional
/// 1-based line suffix so the same diagnostics surface in both engines.
/// </summary>
public sealed class BeckYamlException : Exception
{
    /// <summary>The 1-based line in the YAML source where the problem is, when known.</summary>
    public int? Line { get; }

    public BeckYamlException(string message, int? line = null)
        : base(line is int n ? $"{message} (line {n})" : message)
    {
        Line = line;
    }
}
