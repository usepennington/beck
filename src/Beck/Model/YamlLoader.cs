using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Beck.Model;

/// <summary>
/// Parses a YAML source (via YamlDotNet) into an untyped tree of <c>object?</c>:
/// mappings → <c>Dictionary&lt;string, object?&gt;</c>, sequences →
/// <c>List&lt;object?&gt;</c>, plain scalars resolved to <c>null</c>/<c>bool</c>/
/// <c>string</c>, quoted scalars kept as strings.
/// </summary>
/// <remarks>
/// Only <c>null</c> and <c>bool</c> are type-resolved — the cases the TS code
/// branches on (<c>x === true</c>, <c>typeof x === 'boolean'</c>). Numbers stay
/// strings because the coercion layer re-parses <c>"150"</c> → <c>150</c>
/// identically, so the model comes out the same. (js-yaml additionally resolves
/// plain numbers, dates, <c>.inf</c>, etc.; matching those exactly buys nothing on
/// real diagrams and is deliberately skipped.)
/// </remarks>
internal static class YamlLoader
{
    /// <summary>Parse a diagram source; <c>yaml.load(src) ?? {}</c> semantics (empty/null → empty map).</summary>
    public static object? ParseYaml(string src)
    {
        try
        {
            var stream = new YamlStream();
            using var reader = new StringReader(src);
            stream.Load(reader);
            if (stream.Documents.Count == 0) return new Dictionary<string, object?>();
            object? value = Convert(stream.Documents[0].RootNode);
            return value ?? new Dictionary<string, object?>();
        }
        catch (YamlException ex)
        {
            int? line = ex.Start.Line > 0 ? (int)ex.Start.Line : null;
            throw new BeckYamlException($"YAML parse error: {ex.Message}", line);
        }
    }

    private static object? Convert(YamlNode node) => node switch
    {
        YamlMappingNode map => ConvertMap(map),
        YamlSequenceNode seq => ConvertSeq(seq),
        YamlScalarNode scalar => ConvertScalar(scalar),
        _ => null,
    };

    private static Dictionary<string, object?> ConvertMap(YamlMappingNode map)
    {
        var result = new Dictionary<string, object?>();
        foreach (var (key, val) in map.Children)
        {
            string keyStr = key is YamlScalarNode ks ? ks.Value ?? "" : key.ToString() ?? "";
            result[keyStr] = Convert(val);
        }
        return result;
    }

    private static List<object?> ConvertSeq(YamlSequenceNode seq)
    {
        var result = new List<object?>(seq.Children.Count);
        foreach (var item in seq.Children) result.Add(Convert(item));
        return result;
    }

    private static object? ConvertScalar(YamlScalarNode scalar)
    {
        // Quoted / literal / folded scalars are always strings.
        if (scalar.Style != ScalarStyle.Plain) return scalar.Value ?? "";
        return (scalar.Value ?? "") switch
        {
            "" or "~" or "null" or "Null" or "NULL" => null,
            "true" or "True" or "TRUE" => true,
            "false" or "False" or "FALSE" => false,
            var s => s,
        };
    }
}
