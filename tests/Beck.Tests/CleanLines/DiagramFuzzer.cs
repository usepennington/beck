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

    private static readonly string[] _flowVerbs =
    [
        "Validate", "Fetch", "Normalize", "Check", "Compute", "Persist", "Notify", "Enqueue",
        "Retry", "Transform", "Merge", "Dispatch", "Archive", "Review", "Approve", "Escalate",
    ];

    /// <summary>
    /// Deterministic <c>type: flowchart</c> generator mirroring <see cref="Yaml"/>'s DAG-by-construction
    /// shape, but over steps/links with a random mix of kinds: exactly one <c>start</c>, a chance of one
    /// <c>end</c>, and a spread of decision/io/process/terminator steps for the interior. Every decision
    /// gets exactly two outgoing labeled links ("yes"/"no") when at least two downstream targets exist;
    /// otherwise it degrades to whatever targets are available so the diagram never becomes unparsable.
    /// </summary>
    public static string FlowchartYaml(int seed)
    {
        var rng = new Random(seed);
        var n = 4 + rng.Next(9);                  // 4..12 steps
        var dir = _directions[rng.Next(_directions.Length)];
        var hasEnd = rng.Next(10) < 7;             // ~70%

        // step 0 is always "start"; the last step is "end" when hasEnd, else an ordinary step.
        var kinds = new string[n];
        kinds[0] = "start";
        for (var i = 1; i < n; i++)
        {
            if (hasEnd && i == n - 1)
            {
                kinds[i] = "end";
                continue;
            }

            var roll = rng.Next(100);
            kinds[i] = roll switch
            {
                < 25 => "decision",
                < 40 => "io",
                < 55 => "terminator",
                _ => "process",
            };
        }

        var sb = new StringBuilder();
        sb.AppendLine("type: flowchart");
        sb.AppendLine(CultureInfo.InvariantCulture, $"meta: {{ direction: {dir} }}");
        sb.AppendLine("steps:");
        var titles = new string[n];
        for (var i = 0; i < n; i++)
        {
            titles[i] = kinds[i] switch
            {
                "start" => "Start",
                "end" => "End",
                _ => string.Join(' ', Enumerable.Range(0, 1 + rng.Next(2)).Select(_ => _flowVerbs[rng.Next(_flowVerbs.Length)])),
            };
            sb.Append(CultureInfo.InvariantCulture, $"  - {{ id: s{i}, text: \"{titles[i]}\", kind: {kinds[i]} }}");
            sb.AppendLine();
        }

        // Links: every non-start step takes 1..3 parents from earlier steps (DAG-by-construction).
        // Decisions among the parent pool get exactly two labeled outgoing links to distinct
        // downstream targets when available, so both branches of every diamond render.
        var links = new List<(int From, int To, string? Label)>();
        var seen = new HashSet<(int, int)>();
        void AddLink(int f, int t, string? label = null)
        {
            if (seen.Add((f, t)))
            {
                links.Add((f, t, label));
            }
        }

        for (var i = 1; i < n; i++)
        {
            var parents = 1 + rng.Next(Math.Min(3, i));
            for (var p = 0; p < parents; p++)
            {
                var from = rng.Next(i);
                AddLink(from, i);
            }
        }

        // Give every decision step exactly two outgoing links (yes/no) when it has at least two
        // distinct downstream targets available; otherwise leave its natural fan-out alone.
        for (var i = 0; i < n; i++)
        {
            if (kinds[i] != "decision")
            {
                continue;
            }

            var downstream = Enumerable.Range(i + 1, n - i - 1).ToList();
            if (downstream.Count < 2)
            {
                continue;
            }

            // Drop any links this decision already produced so we can lay down exactly two.
            links.RemoveAll(l => l.From == i);
            seen.RemoveWhere(k => k.Item1 == i);

            var t1 = downstream[rng.Next(downstream.Count)];
            int t2;
            do
            {
                t2 = downstream[rng.Next(downstream.Count)];
            } while (t2 == t1);

            AddLink(i, t1, "yes");
            AddLink(i, t2, "no");
        }

        // Occasional merge fan-in: one extra parent wired into a later step.
        if (n >= 5 && rng.Next(2) == 0)
        {
            var to = 2 + rng.Next(n - 2);
            var from = rng.Next(to);
            AddLink(from, to);
        }

        // A back edge (against the flow) roughly a third of the time, a self loop a sixth —
        // never sourced from a decision (its two branches are already fixed above) or targeting start.
        if (n >= 4 && rng.Next(3) == 0)
        {
            int to = rng.Next(n / 2), from = n / 2 + rng.Next(n - n / 2);
            if (from != to && kinds[from] != "decision" && to != 0)
            {
                AddLink(from, to);
            }
        }
        if (rng.Next(6) == 0)
        {
            var s = rng.Next(n);
            if (kinds[s] != "decision")
            {
                AddLink(s, s);
            }
        }

        sb.AppendLine("links:");
        foreach (var (f, t, label) in links)
        {
            sb.Append(CultureInfo.InvariantCulture, $"  - {{ from: s{f}, to: s{t}");
            if (label is not null)
            {
                sb.Append(CultureInfo.InvariantCulture, $", label: \"{label}\"");
            }
            else if (rng.Next(4) == 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $", label: \"{_flowVerbs[rng.Next(_flowVerbs.Length)].ToLowerInvariant()}\"");
            }

            sb.AppendLine(" }");
        }
        return sb.ToString();
    }

    private static readonly string[] _mindTitleWords =
    [
        "Rendering", "Layout", "Routing", "Model", "Text", "Animation", "Packages", "Engine",
        "Authoring", "Schema", "Determinism", "Pipeline", "Measure", "Theming", "Sequence",
        "State", "Class", "Butterfly", "Accent", "Cycle", "Flow", "Packet", "Trail", "Caption",
    ];

    private static readonly string[] _mindItemWords =
    [
        "model", "measure", "layout", "route", "svg", "animate", "coerce", "validate",
        "defaults", "keyframes", "easing", "schedule", "compiler", "metrics", "obstacle",
        "avoidance", "anchor", "spread", "virtual", "node",
    ];

    private static readonly string[] _mindSentences =
    [
        "Same YAML, same SVG.", "Everything renders server side.", "No client JavaScript at all.",
        "Colors are CSS variables only.", "The model fills every default.",
        "Deterministic output every time.", "One continuous path per edge.",
    ];

    private static readonly string[] _mindAccents = ["primary", "info", "success", "warn", "danger", "neutral"];

    /// <summary>
    /// Deterministic <c>type: mindmap</c> generator: a nested topic tree (max rank 1..3, so 2–4
    /// levels deep counting the root), 1–5 children per topic, total topics capped near 25. Each
    /// topic draws a random content mix — heading-only, heading+items (1–5), heading+body (1–2
    /// sentences), or items+body together — with occasional explicit <c>accent:</c> overrides and
    /// occasional long titles / multi-word items to force card wrapping. A single first-level branch
    /// (a lopsided butterfly) and a full five-branch fan are both reachable across the seed range.
    /// Seed → YAML is pure; a failing case reproduces from its seed alone.
    /// </summary>
    public static string MindMapYaml(int seed)
    {
        var rng = new Random(seed);
        var maxRank = 1 + rng.Next(3);   // deepest rank 1..3 → 2..4 levels including the root
        const int cap = 25;
        var count = 1;                   // the root itself

        string Words(string[] bank, int min, int max)
        {
            var n = min + rng.Next(max - min + 1);
            return string.Join(' ', Enumerable.Range(0, n).Select(_ => bank[rng.Next(bank.Length)]));
        }

        // Usually 1–2 words; occasionally a long 3–4 word title to force wrapping.
        string TopicTitle() => rng.Next(6) == 0 ? Words(_mindTitleWords, 3, 4) : Words(_mindTitleWords, 1, 2);
        // Mostly a single word; occasionally a 2–3 word phrase (wrapping again).
        string Item() => rng.Next(4) == 0 ? Words(_mindItemWords, 2, 3) : Words(_mindItemWords, 1, 1);

        var sb = new StringBuilder();
        sb.AppendLine("type: mindmap");
        sb.AppendLine(CultureInfo.InvariantCulture, $"meta: {{ title: \"{Words(_mindTitleWords, 1, 3)}\" }}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"root: \"{Words(_mindTitleWords, 1, 2)}\"");
        sb.AppendLine("topics:");

        // A topic list item begins at `dash` (its "- " marker); its sibling keys align two columns
        // in; a nested `children:` sequence indents its own items four columns past `dash`.
        void EmitTopic(string dash, int rank)
        {
            count++;
            var key = dash + "  ";
            sb.Append(dash).Append(CultureInfo.InvariantCulture, $"- title: \"{TopicTitle()}\"").Append('\n');

            if (rng.Next(5) == 0)
            {
                sb.Append(key).Append("accent: ").Append(_mindAccents[rng.Next(_mindAccents.Length)]).Append('\n');
            }

            var kind = rng.Next(4); // 0 heading-only · 1 items · 2 body · 3 items+body
            if (kind is 1 or 3)
            {
                var items = string.Join(", ", Enumerable.Range(0, 1 + rng.Next(5)).Select(_ => $"\"{Item()}\""));
                sb.Append(key).Append("items: [").Append(items).Append("]\n");
            }
            if (kind is 2 or 3)
            {
                var body = string.Join(' ', Enumerable.Range(0, 1 + rng.Next(2)).Select(_ => _mindSentences[rng.Next(_mindSentences.Length)]));
                sb.Append(key).Append(CultureInfo.InvariantCulture, $"body: \"{body}\"").Append('\n');
            }

            // Recurse ~75% of the time while depth and the topic cap allow it, so both shallow leaf
            // pills and deep subtrees appear, and the total stays near the cap.
            if (rank < maxRank && count < cap && rng.Next(4) != 0)
            {
                var maxKids = Math.Min(5, cap - count);
                sb.Append(key).Append("children:\n");
                var childDash = dash + "    ";
                for (var i = 0; i < maxKids && count < cap && (i == 0 || rng.Next(3) != 0); i++)
                {
                    EmitTopic(childDash, rank + 1);
                }
            }
        }

        var rootBranches = 1 + rng.Next(5); // 1..5 first-level branches
        for (var i = 0; i < rootBranches && count < cap; i++)
        {
            EmitTopic("  ", 1);
        }

        return sb.ToString();
    }
}