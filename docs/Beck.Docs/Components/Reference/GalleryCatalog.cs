using Beck;

namespace Beck.Docs.Components.Reference;

/// <summary>One value of a token enum, ready to render as a gallery card.</summary>
internal sealed record GalleryItem(string Name, string Token, string Summary, string Yaml);

/// <summary>
/// Backs <see cref="BeckGallery"/>. Each gallery <c>kind</c> names a Beck token enum; the values are
/// pulled by reflection (so a new enum member shows up with no doc edit), the description comes from
/// the source doc comment via <see cref="BeckXmlDocs"/>, and a small per-kind template turns the
/// token into a live <c>beck</c> diagram demonstrating it. The token rule mirrors the engine's
/// internal <c>Beck.Tokens.Of</c>.
/// </summary>
internal static class GalleryCatalog
{
    /// <summary>Cards for a <c>&lt;BeckGallery Of="kind" /&gt;</c>, or null when the kind is unknown.</summary>
    public static IReadOnlyList<GalleryItem>? Build(string kind) => kind switch
    {
        "node-kinds" => Items<NodeKind>(NodeKindYaml),
        "node-variants" => Items<NodeVariant>(NodeVariantYaml),
        "accents" => Items<AccentToken>(AccentYaml),
        "edge-kinds" => Items<EdgeKind>(EdgeKindYaml),
        "edge-curves" => Items<EdgeCurve>(EdgeCurveYaml),
        "edge-styles" => Items<EdgeStyle>(EdgeStyleYaml),
        "arrowheads" => Items<ArrowEnds>(ArrowYaml),
        "directions" => Items<Direction>(DirectionYaml),
        "packet-shapes" => Items<PacketShape>(PacketShapeYaml),
        "eases" => Items<PacketEase>(EaseYaml),
        _ => null,
    };

    private static List<GalleryItem> Items<TEnum>(Func<string, string, string> yaml)
        where TEnum : struct, Enum
    {
        var items = new List<GalleryItem>();
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var name = value.ToString();
            var token = Token(value);
            items.Add(new GalleryItem(name, token, BeckXmlDocs.ForEnumValue(typeof(TEnum), name), yaml(token, name)));
        }

        return items;
    }

    // Mirrors Beck.Tokens.Of (internal): lowercase the enum name, except Direction (kept
    // uppercase — TB/BT/LR/RL) and EdgeCurve.StepRound, which spells to "step-round".
    private static string Token(Enum value) => value switch
    {
        Direction d => d.ToString(),
        EdgeCurve.StepRound => "step-round",
        _ => value.ToString().ToLowerInvariant(),
    };

    // ---- Static previews (animate: false) ----

    private static string NodeKindYaml(string token, string name) =>
        $"meta: {{ animate: false }}\nnodes:\n  - {{ id: n, title: {name}, kind: {token} }}";

    private static string NodeVariantYaml(string token, string name) =>
        $"meta: {{ animate: false }}\nnodes:\n  - {{ id: n, title: {name}, variant: {token} }}";

    private static string AccentYaml(string token, string name) =>
        $"meta: {{ animate: false }}\nnodes:\n  - {{ id: n, title: {name}, accent: {token} }}";

    private static string EdgeCurveYaml(string token, string name) =>
        "meta: { animate: false }\n" +
        "nodes: [ { id: a, title: A }, { id: b, title: B }, { id: c, title: C } ]\n" +
        $"edges: [ {{ from: a, to: b, curve: {token} }}, {{ from: a, to: c }} ]";

    private static string EdgeStyleYaml(string token, string name) =>
        "meta: { animate: false, direction: LR }\n" +
        "nodes: [ { id: a, title: A }, { id: b, title: B } ]\n" +
        $"edges: [ {{ from: a, to: b, style: {token} }} ]";

    private static string ArrowYaml(string token, string name) =>
        "meta: { animate: false, direction: LR }\n" +
        "nodes: [ { id: a, title: A }, { id: b, title: B } ]\n" +
        $"edges: [ {{ from: a, to: b, arrow: {token} }} ]";

    private static string DirectionYaml(string token, string name) =>
        $"meta: {{ animate: false, direction: {token} }}\n" +
        "nodes: [ { id: a, title: A }, { id: b, title: B }, { id: c, title: C } ]\n" +
        "edges: [ { from: a, to: b }, { from: b, to: c } ]";

    // ---- Motion previews (a looping packet so the difference is legible) ----

    // No flow block: the engine auto-derives one, and the packet inherits the edge kind's
    // size, speed, glow, and ease — which is the whole point of the per-edge-kind gallery.
    private static string EdgeKindYaml(string token, string name) =>
        "meta: { direction: LR }\n" +
        "nodes: [ { id: a, title: A }, { id: b, title: B } ]\n" +
        $"edges: [ {{ from: a, to: b, kind: {token} }} ]";

    private static string PacketShapeYaml(string token, string name) =>
        "meta: { direction: LR }\n" +
        "nodes: [ { id: a, title: Source }, { id: b, title: Sink } ]\n" +
        "edges: [ { from: a, to: b } ]\n" +
        "flow:\n  repeat: -1\n  repeatDelay: 0.5\n  steps:\n" +
        $"    - packet: {{ from: a, to: b, shape: {token}, speed: 200, impact: true }}\n" +
        "    - wait: 0.4\n" +
        "    - reset: true";

    // A deliberately slow, glowing dot makes each easing curve readable.
    private static string EaseYaml(string token, string name) =>
        "meta: { direction: LR }\n" +
        "nodes: [ { id: a, title: Start }, { id: b, title: End } ]\n" +
        "edges: [ { from: a, to: b } ]\n" +
        "flow:\n  repeat: -1\n  repeatDelay: 0.5\n  steps:\n" +
        $"    - packet: {{ from: a, to: b, ease: {token}, size: 9, speed: 180, glow: true }}\n" +
        "    - wait: 0.4\n" +
        "    - reset: true";
}
