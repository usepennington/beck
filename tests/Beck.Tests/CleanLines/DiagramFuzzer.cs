using System.Globalization;
using System.Text;

namespace Beck.Tests.CleanLines;

/// <summary>
/// The clean-line chaos monkey's generator: a seeded, reproducible walk over the shapes
/// the layered engine actually meets — fan-in, fan-out, chains, rank-skipping long edges,
/// back edges, self loops, mixed card widths, all four directions.
///
/// Every diagram is a DAG by construction (edges always run low-index → high-index) plus
/// an optional sprinkle of back edges and self loops, so the ranking stage sees both the
/// clean case and the cycle-break path. Seed → YAML is a pure function: a failing case
/// reproduces from its seed alone.
/// </summary>
internal static class DiagramFuzzer
{
    private static readonly string[] _words =
    [
        "Browser", "Kestrel", "Crawler", "Pipeline", "Output", "Gateway", "Cache", "Queue",
        "Worker", "Index", "Store", "Renderer", "Parser", "Router", "Sink", "Source",
        "Middleware", "Registry", "Scheduler", "Bus",
    ];

    private static readonly string[] _subtitles =
    [
        "dev serve", "in-process", "middleware · renderers", "discover · parse · render",
        "batched", "read-through", "", "", "",
    ];

    private static readonly string[] _directions = ["TB", "BT", "LR", "RL"];
    private static readonly string[] _kinds = ["", "", "", "user", "db", "queue"];

    public static string Yaml(int seed)
    {
        var rng = new Random(seed);
        var n = 3 + rng.Next(10);                 // 3..12 nodes
        var dir = _directions[rng.Next(_directions.Length)];

        var sb = new StringBuilder();
        sb.AppendLine("type: architecture");
        sb.AppendLine(CultureInfo.InvariantCulture, $"meta: {{ direction: {dir} }}");
        sb.AppendLine("nodes:");
        for (var i = 0; i < n; i++)
        {
            var title = string.Join(' ', Enumerable.Range(0, 1 + rng.Next(2)).Select(_ => _words[rng.Next(_words.Length)]));
            var sub = _subtitles[rng.Next(_subtitles.Length)];
            var kind = _kinds[rng.Next(_kinds.Length)];
            sb.Append(CultureInfo.InvariantCulture, $"  - {{ id: n{i}, title: \"{title}\"");
            if (sub.Length > 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $", subtitle: \"{sub}\"");
            }

            if (kind.Length > 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $", kind: {kind}");
            }

            sb.AppendLine(" }");
        }

        // Edges: every node past the first takes 1..3 parents drawn from earlier nodes.
        // Drawing a parent from anywhere earlier (not just the previous rank) is what
        // produces the rank-skipping long edges that force virtual nodes and lane detours.
        var edges = new List<(int From, int To)>();
        var seen = new HashSet<(int, int)>();
        for (var i = 1; i < n; i++)
        {
            var parents = 1 + rng.Next(Math.Min(3, i));
            for (var p = 0; p < parents; p++)
            {
                var from = rng.Next(i);
                if (seen.Add((from, i)))
                {
                    edges.Add((from, i));
                }
            }
        }
        // A back edge (against the flow) roughly a third of the time, a self loop a sixth.
        if (n >= 4 && rng.Next(3) == 0)
        {
            int to = rng.Next(n / 2), from = n / 2 + rng.Next(n - n / 2);
            if (from != to && seen.Add((from, to)))
            {
                edges.Add((from, to));
            }
        }
        if (rng.Next(6) == 0) { var s = rng.Next(n); if (seen.Add((s, s)))
            {
                edges.Add((s, s));
            }
        }

        sb.AppendLine("edges:");
        foreach (var (f, t) in edges)
        {
            sb.Append(CultureInfo.InvariantCulture, $"  - {{ from: n{f}, to: n{t}");
            if (rng.Next(4) == 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $", label: \"{_words[rng.Next(_words.Length)].ToLowerInvariant()}\"");
            }

            sb.AppendLine(" }");
        }
        return sb.ToString();
    }
}