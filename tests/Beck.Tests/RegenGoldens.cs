using System.Text.Json;
using System.Text.Json.Nodes;
using Beck.Rendering;
using Beck.Skia;
using Beck.Rendering.Route;
using Beck.Rendering.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Golden regeneration helper (NOT a gate). Guarded by <c>BECK_REGEN=1</c> so a normal
/// <c>dotnet test</c> skips it. Regenerates the card-size and route goldens from the current C#
/// engine — but only the entries that actually changed — so an intentional visual change lands as a
/// minimal golden diff. Run: <c>BECK_REGEN=1 dotnet test --filter FullyQualifiedName~RegenGoldens</c>.
/// </summary>
public sealed class RegenGoldens
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static readonly string GoldenDir = Path.Combine(AppContext.BaseDirectory, "Goldens");
    private static JsonSerializerOptions Read => new() { PropertyNameCaseInsensitive = true };

    private sealed record RectDto(double X, double Y, double W, double H);
    private sealed record LayoutDto(Dictionary<string, RectDto> Nodes, Dictionary<string, RectDto> Groups, double Width, double Height);

    [Fact]
    public void Regenerate()
    {
        if (Environment.GetEnvironmentVariable("BECK_REGEN") != "1") return;
        using var measurer = new SkiaTextMeasurer(TestFonts.Spec());
        // Order matters: cards feed layouts, layouts feed routes.
        int cardEdits = RegenCards(measurer);
        int layoutEdits = RegenLayouts();
        int routeEdits = RegenRoutes();
        int svgEdits = RegenSvg();
        Assert.True(true, $"regen: {cardEdits} cards, {layoutEdits} layouts, {routeEdits} route files, {svgEdits} svg goldens updated");
    }

    // Re-render the frozen full-SVG goldens (ClassicSvgGoldenTests) from the current engine; rewrite
    // only the files whose bytes changed. Regenerated files land in the test OUTPUT copy of Goldens/
    // — copy them back to tests/Beck.Tests/Goldens/svg to commit an intentional visual change.
    private static int RegenSvg()
    {
        var specs = new (string File, string Yaml, string Suffix, BeckStyle Style)[]
        {
            ("arch-kitchen", "arch-kitchen", "cla551c0", BeckStyle.Classic),
            ("seq-kitchen", "seq-kitchen", "cla551c0", BeckStyle.Classic),
            ("class", "class", "cla551c0", BeckStyle.Classic),
            ("minimal", "arch-kitchen", "min1ma1c", MinimalStyle.Instance),
            ("terminal", "arch-kitchen", "term1na1", TerminalStyle.Instance),
            ("blueprint", "arch-kitchen", "b1uepr1n", BlueprintStyle.Instance),
            ("glow", "arch-kitchen", "g10wg10w", GlowStyle.Instance),
            ("editorial", "arch-kitchen", "ed1t0r1a", EditorialStyle.Instance),
            ("brutalist", "arch-kitchen", "brut4115", BrutalistStyle.Instance),
            ("sketch", "arch-kitchen", "sk3tchg0", SketchStyle.Instance),
            ("extrude", "arch-kitchen", "extrud30", ExtrudeStyle.Instance),
            ("circuit", "arch-kitchen", "c1rcu1t0", CircuitStyle.Instance),
            ("metro", "arch-kitchen", "m3tr0000", MetroStyle.Instance),
        };
        string svgDir = Path.Combine(GoldenDir, "svg");
        int edits = 0;
        foreach (var (file, yamlName, suffix, style) in specs)
        {
            string yaml = File.ReadAllText(Path.Combine(CorpusDir, yamlName + ".yaml"));
            string svg = BeckSvg.Render(yaml, new SvgRenderOptions { IdSuffix = suffix, Style = style });
            string path = Path.Combine(svgDir, file + ".svg");
            if (File.Exists(path) && File.ReadAllText(path) == svg) continue;
            File.WriteAllText(path, svg);
            edits++;
        }
        return edits;
    }

    // Update cards.json entries whose C# size now differs from the stored value by >1px.
    private static int RegenCards(SkiaTextMeasurer measurer)
    {
        string path = Path.Combine(GoldenDir, "measure", "cards.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        int edits = 0;
        foreach (var (file, fileNode) in root)
        {
            DiagramModel model = Validate.LoadDiagram(File.ReadAllText(Path.Combine(CorpusDir, file + ".yaml")));
            var byId = model.Nodes.ToDictionary(n => n.Id);
            foreach (var (id, entry) in fileNode!.AsObject())
            {
                if (!byId.TryGetValue(id, out var node)) continue;
                Size got = CardSizer.Measure(node, measurer);
                var obj = entry!.AsObject();
                double w = obj["w"]!.GetValue<double>(), h = obj["h"]!.GetValue<double>();
                if (Math.Abs(got.W - w) <= 1 && Math.Abs(got.H - h) <= 1) continue;
                obj["w"] = got.W; obj["h"] = got.H;
                edits++;
            }
        }
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        return edits;
    }

    private sealed record RectOut(double x, double y, double w, double h);
    private sealed record LayoutOut(Dictionary<string, RectOut> nodes, Dictionary<string, RectOut> groups, double width, double height);

    // Recompute each layout from the (updated) card sizes; rewrite any that moved >0.5px.
    private static int RegenLayouts()
    {
        string layoutDir = Path.Combine(GoldenDir, "layout");
        var cards = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, RectOut>>>(
            File.ReadAllText(Path.Combine(GoldenDir, "measure", "cards.json")), Read)!;
        int edits = 0;
        static double R(double v) => Math.Round(v, 2);
        foreach (string file in Directory.EnumerateFiles(layoutDir, "*.layout.json").Select(f => Path.GetFileName(f).Replace(".layout.json", "")).ToList())
        {
            if (!cards.TryGetValue(file, out var fileCards)) continue;
            DiagramModel model = Validate.LoadDiagram(File.ReadAllText(Path.Combine(CorpusDir, file + ".yaml")));
            var sizes = fileCards.ToDictionary(kv => kv.Key, kv => new Size(kv.Value.w, kv.Value.h));
            LayoutResult got = LayeredLayout.Compute(model, sizes);

            var golden = JsonSerializer.Deserialize<LayoutDto>(
                File.ReadAllText(Path.Combine(layoutDir, file + ".layout.json")), Read)!;
            bool changed = Math.Abs(golden.Width - got.Width) > 0.5 || Math.Abs(golden.Height - got.Height) > 0.5
                || got.Nodes.Any(kv => !golden.Nodes.TryGetValue(kv.Key, out var g)
                    || Math.Abs(g.X - kv.Value.X) > 0.5 || Math.Abs(g.Y - kv.Value.Y) > 0.5
                    || Math.Abs(g.W - kv.Value.W) > 0.5 || Math.Abs(g.H - kv.Value.H) > 0.5)
                || got.Groups.Any(kv => !golden.Groups.TryGetValue(kv.Key, out var g)
                    || Math.Abs(g.X - kv.Value.X) > 0.5 || Math.Abs(g.Y - kv.Value.Y) > 0.5);
            if (!changed) continue;

            RectOut Out(Rect r) => new(R(r.X), R(r.Y), R(r.W), R(r.H));
            var dto = new LayoutOut(
                got.Nodes.ToDictionary(kv => kv.Key, kv => Out(kv.Value)),
                got.Groups.ToDictionary(kv => kv.Key, kv => Out(kv.Value)),
                R(got.Width), R(got.Height));
            File.WriteAllText(Path.Combine(layoutDir, file + ".layout.json"),
                JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false }));
            edits++;
        }
        return edits;
    }

    // Recompute routes from each golden layout; replace any file whose edge paths changed.
    private static int RegenRoutes()
    {
        string path = Path.Combine(GoldenDir, "route", "edges.json");
        string layoutDir = Path.Combine(GoldenDir, "layout");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        int edits = 0;
        foreach (var file in root.Select(kv => kv.Key).ToList())
        {
            var ld = JsonSerializer.Deserialize<LayoutDto>(
                File.ReadAllText(Path.Combine(layoutDir, file + ".layout.json")), Read)!;
            static Dictionary<string, Rect> Rects(Dictionary<string, RectDto> m) =>
                m.ToDictionary(kv => kv.Key, kv => new Rect(kv.Value.X, kv.Value.Y, kv.Value.W, kv.Value.H));
            var layout = new LayoutResult(Rects(ld.Nodes), Rects(ld.Groups), ld.Width, ld.Height);
            DiagramModel model = Validate.LoadDiagram(File.ReadAllText(Path.Combine(CorpusDir, file + ".yaml")));
            var got = EdgePainter.RouteEdges(model, layout).ToDictionary(r => r.Edge.Id, r => r.D);

            var old = root[file]!.AsObject();
            bool changed = old.Count != got.Count
                || got.Any(kv => old[kv.Key]?.GetValue<string>() != kv.Value);
            if (!changed) continue;
            var fresh = new JsonObject();
            foreach (var (id, d) in got) fresh[id] = d;
            root[file] = fresh;
            edits++;
        }
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return edits;
    }
}
