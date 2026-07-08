using System.Text.Json;
using Beck.Rendering;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// M3 gate: <see cref="LayeredLayout"/> reproduces the reference <c>layeredLayout</c>
/// (node/group rects + canvas size) to ±0.5px when fed the same browser-measured
/// SizeMap. Both consume Goldens/measure/cards.json, so any disagreement is a
/// porting bug, not measurement noise. Goldens: <c>npx vite-node tools/oracle-layout.ts</c>.
/// </summary>
public sealed class LayoutParityTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static readonly string MeasureGolden = Path.Combine(AppContext.BaseDirectory, "Goldens", "measure", "cards.json");
    private static readonly string LayoutDir = Path.Combine(AppContext.BaseDirectory, "Goldens", "layout");

    private sealed record RectDto(double X, double Y, double W, double H);
    private sealed record LayoutDto(Dictionary<string, RectDto> Nodes, Dictionary<string, RectDto> Groups, double Width, double Height);
    private sealed record WH(double W, double H);

    private static readonly Dictionary<string, Dictionary<string, WH>> Cards =
        JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, WH>>>(
            File.ReadAllText(MeasureGolden), Opts)!;

    private static JsonSerializerOptions Opts => new() { PropertyNameCaseInsensitive = true };

    public static IEnumerable<object[]> Files() =>
        Directory.EnumerateFiles(LayoutDir, "*.layout.json")
            .Select(f => new object[] { Path.GetFileName(f).Replace(".layout.json", "") });

    [Theory]
    [MemberData(nameof(Files))]
    public void Layout_MatchesOracle(string file)
    {
        var golden = JsonSerializer.Deserialize<LayoutDto>(
            File.ReadAllText(Path.Combine(LayoutDir, file + ".layout.json")), Opts)!;
        DiagramModel model = Validate.LoadDiagram(File.ReadAllText(Path.Combine(CorpusDir, file + ".yaml")));
        var sizes = Cards[file].ToDictionary(kv => kv.Key, kv => new Size(kv.Value.W, kv.Value.H));

        LayoutResult got = LayeredLayout.Compute(model, sizes);

        var fails = new List<string>();
        void Cmp(string what, double a, double b) { if (Math.Abs(a - b) > 0.5) fails.Add($"  {what}: oracle={a:0.###} got={b:0.###} Δ={b - a:+0.###;-0.###}"); }

        foreach (var (id, r) in golden.Nodes)
        {
            Assert.True(got.Nodes.ContainsKey(id), $"{file}: missing node {id}");
            Rect g = got.Nodes[id];
            Cmp($"node {id}.x", r.X, g.X); Cmp($"node {id}.y", r.Y, g.Y);
            Cmp($"node {id}.w", r.W, g.W); Cmp($"node {id}.h", r.H, g.H);
        }
        foreach (var (id, r) in golden.Groups)
        {
            Assert.True(got.Groups.ContainsKey(id), $"{file}: missing group {id}");
            Rect g = got.Groups[id];
            Cmp($"group {id}.x", r.X, g.X); Cmp($"group {id}.y", r.Y, g.Y);
            Cmp($"group {id}.w", r.W, g.W); Cmp($"group {id}.h", r.H, g.H);
        }
        Cmp("width", golden.Width, got.Width);
        Cmp("height", golden.Height, got.Height);

        Assert.True(fails.Count == 0, $"{file} layout mismatch:\n{string.Join("\n", fails)}");
    }
}
