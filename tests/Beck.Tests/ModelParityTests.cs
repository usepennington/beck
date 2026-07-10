using System.Text.Json;
using System.Text.Json.Nodes;
using Beck.Model;
using Beck.Rendering;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// M1 gate: the C# model, serialized canonically, byte-matches the TS oracle's
/// <c>JSON.stringify(loadDiagram(yaml))</c> over the frozen corpus. Regenerate the
/// goldens with <c>npx vite-node tools/oracle-model.ts</c> when the corpus changes.
/// </summary>
public sealed class ModelParityTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static readonly string GoldenDir = Path.Combine(AppContext.BaseDirectory, "Goldens", "model");

    public static IEnumerable<object[]> Corpus() =>
        Directory.EnumerateFiles(CorpusDir, "*.yaml")
            .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

    [Theory]
    [MemberData(nameof(Corpus))]
    public void Model_MatchesOracle(string name)
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, name + ".yaml"));
        string golden = File.ReadAllText(Path.Combine(GoldenDir, name + ".model.json")).Trim();

        DiagramModel model = Validate.LoadDiagram(yaml);
        string actual = ModelJson.Canonical(model).Trim();

        if (actual != golden)
        {
            string? diff = FirstDifference(golden, actual);
            Assert.Fail($"Model parity mismatch for '{name}':\n{diff}\n\nexpected: {golden}\n\nactual:   {actual}");
        }
    }

    /// <summary>Structural first-difference path for a readable failure message.</summary>
    private static string? FirstDifference(string expectedJson, string actualJson)
    {
        JsonNode? e = JsonNode.Parse(expectedJson);
        JsonNode? a = JsonNode.Parse(actualJson);
        return Walk("$", e, a);
    }

    private static string? Walk(string path, JsonNode? e, JsonNode? a)
    {
        if (e is null && a is null) return null;
        if (e is null) return $"{path}: expected <absent>, got {a}";
        if (a is null) return $"{path}: expected {e}, got <absent>";

        switch (e)
        {
            case JsonObject eo when a is JsonObject ao:
            {
                foreach (var key in eo.Select(p => p.Key).Union(ao.Select(p => p.Key)))
                {
                    string? d = Walk($"{path}.{key}", eo[key], ao[key]);
                    if (d != null) return d;
                }
                return null;
            }
            case JsonArray ea when a is JsonArray aa:
            {
                if (ea.Count != aa.Count) return $"{path}: array length {ea.Count} vs {aa.Count}";
                for (int i = 0; i < ea.Count; i++)
                {
                    string? d = Walk($"{path}[{i}]", ea[i], aa[i]);
                    if (d != null) return d;
                }
                return null;
            }
            default:
                return e.ToJsonString() == a.ToJsonString() ? null
                    : $"{path}: expected {e.ToJsonString()}, got {a.ToJsonString()}";
        }
    }
}
