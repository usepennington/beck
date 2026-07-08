using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Beck;

namespace Beck.Docs.Components.Reference;

/// <summary>
/// Reads the doc comments compiled into <c>Beck.xml</c> — shipped next to <c>Beck.dll</c> because
/// the engine project enables <c>GenerateDocumentationFile</c>. The gallery and the API reference
/// use it so a description on the docs site is literally its source doc comment: one source of
/// truth, no hand-copied table to drift. Loaded once, lazily; a missing file degrades to empty
/// descriptions rather than throwing inside a content render.
/// </summary>
internal static class BeckXmlDocs
{
    private static readonly Lazy<IReadOnlyDictionary<string, XElement>> Members = new(Load);

    /// <summary>Plain-text summary for an enum value, or empty when none is documented.</summary>
    public static string ForEnumValue(Type enumType, string name) =>
        Normalize(Summary($"F:{enumType.FullName}.{name}")?.Value);

    /// <summary>Plain-text summary for a type, or empty when none is documented.</summary>
    public static string ForType(Type type) =>
        Normalize(Summary($"T:{type.FullName}")?.Value);

    /// <summary>Rich-HTML summary for a type, or empty when none is documented.</summary>
    public static string SummaryHtml(Type type) =>
        RenderInline(Summary($"T:{type.FullName}"));

    /// <summary>
    /// Rich-HTML doc for a method or constructor: the summary plus a "Throws …" line per
    /// <c>&lt;exception&gt;</c> tag. Empty when the member carries no doc comment.
    /// </summary>
    public static string MemberDocHtml(MethodBase method)
    {
        if (!Members.Value.TryGetValue(DocId(method), out var member))
        {
            Debug.WriteLine($"BeckXmlDocs: no doc entry for {DocId(method)}");
            return string.Empty;
        }

        var html = new StringBuilder(RenderInline(member.Element("summary")));
        foreach (var ex in member.Elements("exception"))
        {
            var name = CrefShortName(ex.Attribute("cref")?.Value);
            html.Append("<span class=\"block mt-1.5\">Throws <span class=\"font-mono\">")
                .Append(WebUtility.HtmlEncode(name)).Append("</span> — ")
                .Append(RenderInline(ex)).Append("</span>");
        }
        return html.ToString();
    }

    /// <summary>Raw, dedented C# from a type's <c>&lt;example&gt;&lt;code&gt;</c> block, or null.</summary>
    public static string? ExampleCode(Type type)
    {
        if (!Members.Value.TryGetValue($"T:{type.FullName}", out var member)) return null;
        var code = member.Element("example")?.Element("code")?.Value;
        if (string.IsNullOrWhiteSpace(code)) return null;

        var lines = code.Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0])) lines.RemoveAt(0);
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);
        if (lines.Count == 0) return null;

        var indent = lines.Where(l => !string.IsNullOrWhiteSpace(l))
                          .Min(l => l.Length - l.TrimStart().Length);
        return string.Join('\n', lines.Select(l => l.Length >= indent ? l[indent..] : l));
    }

    /// <summary>
    /// The compiler's doc-comment id for a method or constructor, e.g.
    /// <c>M:Beck.DiagramBuilder.Node(System.String,System.Action{Beck.NodeBuilder})</c>.
    /// Covers what the authoring surface uses: constructed generics, nullable value types,
    /// arrays, <c>params</c> (unmarked). Generic method definitions aren't needed here.
    /// </summary>
    public static string DocId(MethodBase method)
    {
        var name = method.IsConstructor ? "#ctor" : method.Name;
        var id = $"M:{method.DeclaringType!.FullName}.{name}";
        var parameters = method.GetParameters();
        if (parameters.Length == 0) return id;
        return id + "(" + string.Join(",", parameters.Select(p => EncodeType(p.ParameterType))) + ")";
    }

    private static string EncodeType(Type t)
    {
        if (t.IsArray) return EncodeType(t.GetElementType()!) + "[]";
        if (t.IsGenericType)
        {
            var definition = t.GetGenericTypeDefinition().FullName!;
            var backtick = definition.IndexOf('`');
            var args = string.Join(",", t.GetGenericArguments().Select(EncodeType));
            return $"{definition[..backtick]}{{{args}}}";
        }
        return t.FullName!;
    }

    // ---- rich inline rendering -------------------------------------------------------------

    /// <summary>The 12 documented builder types, keyed by name — cref targets become anchor links.</summary>
    private static readonly HashSet<string> LinkableTypes = new(StringComparer.Ordinal)
    {
        nameof(DiagramBuilder), nameof(NodeBuilder), nameof(EdgeBuilder), nameof(GroupBuilder),
        nameof(FlowBuilder), nameof(SequenceDiagramBuilder), nameof(MessageBuilder),
        nameof(StateDiagramBuilder), nameof(StateBuilder), nameof(TransitionBuilder),
        nameof(ClassDiagramBuilder), nameof(ClassBuilder),
    };

    /// <summary>Render a doc element's inline content (text, c, see, paramref) to HTML.</summary>
    private static string RenderInline(XElement? element)
    {
        if (element is null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    sb.Append(WebUtility.HtmlEncode(CollapseWhitespace(text.Value)));
                    break;
                case XElement el when el.Name == "c":
                    sb.Append("<span class=\"font-mono\">").Append(WebUtility.HtmlEncode(el.Value)).Append("</span>");
                    break;
                case XElement el when el.Name == "paramref":
                    sb.Append("<span class=\"font-mono italic\">")
                      .Append(WebUtility.HtmlEncode(el.Attribute("name")?.Value ?? "")).Append("</span>");
                    break;
                case XElement el when el.Name == "see" && el.Attribute("langword") is { } lang:
                    sb.Append("<span class=\"font-mono\">").Append(WebUtility.HtmlEncode(lang.Value)).Append("</span>");
                    break;
                case XElement el when el.Name == "see":
                    sb.Append(RenderCref(el.Attribute("cref")?.Value));
                    break;
                case XElement el:
                    sb.Append(WebUtility.HtmlEncode(CollapseWhitespace(el.Value)));
                    break;
            }
        }
        // Doc-comment line breaks arrive as whitespace runs around child nodes.
        return sb.ToString().Trim();
    }

    /// <summary>A cref becomes an anchor link when it targets a documented builder type, else a mono span.</summary>
    private static string RenderCref(string? cref)
    {
        var name = CrefShortName(cref);
        if (name.Length == 0) return string.Empty;
        if (cref!.StartsWith("T:", StringComparison.Ordinal) && LinkableTypes.Contains(name))
        {
            return $"<a class=\"font-mono underline decoration-dotted underline-offset-2\" " +
                   $"href=\"/api/#{name.ToLowerInvariant()}\">{WebUtility.HtmlEncode(name)}</a>";
        }
        return $"<span class=\"font-mono\">{WebUtility.HtmlEncode(name)}</span>";
    }

    /// <summary>The readable tail of a cref: <c>T:Beck.NodeKind</c> → <c>NodeKind</c>, <c>M:Beck.FlowBuilder.Narrate(…)</c> → <c>FlowBuilder.Narrate</c>.</summary>
    private static string CrefShortName(string? cref)
    {
        if (string.IsNullOrEmpty(cref)) return string.Empty;
        var name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        var paren = name.IndexOf('(');
        if (paren >= 0) name = name[..paren];
        var parts = name.Split('.');
        // Members keep their declaring type for context; types stand alone.
        var keep = cref.StartsWith("T:", StringComparison.Ordinal) ? 1 : 2;
        return string.Join('.', parts.TakeLast(Math.Min(keep, parts.Length)));
    }

    private static XElement? Summary(string key) =>
        Members.Value.TryGetValue(key, out var member) ? member.Element("summary") : null;

    private static IReadOnlyDictionary<string, XElement> Load()
    {
        var map = new Dictionary<string, XElement>(StringComparer.Ordinal);
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
                if (name is not null)
                {
                    map[name] = member;
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
    private static string Normalize(string? s) =>
        s is null ? string.Empty : CollapseWhitespace(s).Trim();

    private static string CollapseWhitespace(string s)
    {
        var sb = new StringBuilder(s.Length);
        var lastWasSpace = false;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
        }
        return sb.ToString();
    }
}
