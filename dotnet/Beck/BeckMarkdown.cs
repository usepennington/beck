namespace Beck;

/// <summary>Helpers for embedding Beck diagrams in Markdown.</summary>
public static class BeckMarkdown
{
    /// <summary>
    /// Wrap Beck YAML in a fenced <c>```beck</c> code block. A markdown engine
    /// renders this as <c>&lt;code class="language-beck"&gt;</c>, which the Beck
    /// client then hydrates into a diagram.
    /// </summary>
    public static string Fence(string yaml) =>
        "```beck\n" + yaml.TrimEnd('\n') + "\n```\n";
}
