using System.Text.Json.Nodes;
using Beck.Model;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// M1 gate: the C# model, serialized canonically, byte-matches the TS oracle's
/// <c>JSON.stringify(loadDiagram(yaml))</c> over the frozen corpus. Regenerate the
/// goldens with <c>npx vite-node tools/oracle-model.ts</c> when the corpus changes.
/// </summary>
public sealed class ModelParityTests
{
    private static readonly string _corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static readonly string _goldenDir = Path.Combine(AppContext.BaseDirectory, "Goldens", "model");

    public static IEnumerable<object[]> Corpus() =>
        Directory.EnumerateFiles(_corpusDir, "*.yaml")
            .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

    [Theory]
    [MemberData(nameof(Corpus))]
    public void Model_MatchesOracle(string name)
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, name + ".yaml"));
        var golden = File.ReadAllText(Path.Combine(_goldenDir, name + ".model.json")).Trim();

        var model = Validate.LoadDiagram(yaml);
        var actual = ModelJson.Canonical(model).Trim();

        if (actual != golden)
        {
            var diff = FirstDifference(golden, actual);
            Assert.Fail($"Model parity mismatch for '{name}':\n{diff}\n\nexpected: {golden}\n\nactual:   {actual}");
        }
    }

    /// <summary>Structural first-difference path for a readable failure message.</summary>
    private static string? FirstDifference(string expectedJson, string actualJson)
    {
        var e = JsonNode.Parse(expectedJson);
        var a = JsonNode.Parse(actualJson);
        return Walk("$", e, a);
    }

    private static string? Walk(string path, JsonNode? e, JsonNode? a)
    {
        if (e is null && a is null)
        {
            return null;
        }

        if (e is null)
        {
            return $"{path}: expected <absent>, got {a}";
        }

        if (a is null)
        {
            return $"{path}: expected {e}, got <absent>";
        }

        switch (e)
        {
            case JsonObject eo when a is JsonObject ao:
                {
                    foreach (var key in eo.Select(p => p.Key).Union(ao.Select(p => p.Key)))
                    {
                        var d = Walk($"{path}.{key}", eo[key], ao[key]);
                        if (d != null)
                        {
                            return d;
                        }
                    }
                    return null;
                }
            case JsonArray ea when a is JsonArray aa:
                {
                    if (ea.Count != aa.Count)
                    {
                        return $"{path}: array length {ea.Count} vs {aa.Count}";
                    }

                    for (var i = 0; i < ea.Count; i++)
                    {
                        var d = Walk($"{path}[{i}]", ea[i], aa[i]);
                        if (d != null)
                        {
                            return d;
                        }
                    }
                    return null;
                }
            default:
                return e.ToJsonString() == a.ToJsonString() ? null
                    : $"{path}: expected {e.ToJsonString()}, got {a.ToJsonString()}";
        }
    }
}