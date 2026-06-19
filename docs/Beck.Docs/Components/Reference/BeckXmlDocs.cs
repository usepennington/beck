using System.Xml.Linq;
using Beck;

namespace Beck.Docs.Components.Reference;

/// <summary>
/// Reads the <c>&lt;summary&gt;</c> doc comments compiled into <c>Beck.xml</c> — shipped next to
/// <c>Beck.dll</c> because the engine project enables <c>GenerateDocumentationFile</c>. The gallery
/// uses it so a token's description on the docs site is literally its source doc comment: one source
/// of truth, no hand-copied table to drift. Loaded once, lazily; a missing file degrades to empty
/// descriptions rather than throwing inside a content render.
/// </summary>
internal static class BeckXmlDocs
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Members = new(Load);

    /// <summary>Plain-text summary for an enum value, or empty when none is documented.</summary>
    public static string ForEnumValue(Type enumType, string name) =>
        Get($"F:{enumType.FullName}.{name}");

    /// <summary>Plain-text summary for a type, or empty when none is documented.</summary>
    public static string ForType(Type type) =>
        Get($"T:{type.FullName}");

    private static string Get(string key) =>
        Members.Value.TryGetValue(key, out var v) ? v : string.Empty;

    private static IReadOnlyDictionary<string, string> Load()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var xmlPath = Path.ChangeExtension(typeof(DiagramBuilder).Assembly.Location, ".xml");
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                return map;
            }

            foreach (var member in XDocument.Load(xmlPath).Descendants("member"))
            {
                var name = member.Attribute("name")?.Value;
                var summary = member.Element("summary");
                if (name is not null && summary is not null)
                {
                    map[name] = Normalize(summary.Value);
                }
            }
        }
        catch
        {
            // Missing or unreadable XML → empty descriptions. The previews still render.
        }

        return map;
    }

    /// <summary>Collapse the doc comment's indentation and line breaks into single spaces.</summary>
    private static string Normalize(string s) =>
        string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
