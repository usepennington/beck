using System.Text;

namespace Beck.Rendering.Svg;

/// <summary>
/// Edge end-marker defs — a port of the marker bodies in <c>src/route/svg.ts</c>.
/// Every shape points +x with its tip at <c>refX</c>, so one def serves either end
/// via <c>orient="auto-start-reverse"</c>. Deduped per (shape, color); ids carry
/// the content hash so multiple diagrams don't collide.
/// </summary>
internal sealed class Markers
{
    private readonly string _hash;
    private readonly Dictionary<string, string> _byKey = new();
    private readonly StringBuilder _defs = new();
    private int _seq;

    public Markers(string hash) => _hash = hash;

    /// <summary>The accumulated <c>&lt;marker&gt;</c> defs.</summary>
    public string Defs => _defs.ToString();

    /// <summary>Ensure a marker for a shape+color exists; returns its id.</summary>
    public string Ensure(string color, MarkerShape shape)
    {
        string key = $"{shape}|{color}";
        if (_byKey.TryGetValue(key, out string? hit)) return hit;

        string id = $"beck-arrow-{_seq++}-{_hash}";
        var (body, viewBox, refX, w, h) = Body(shape, color);
        string refY = viewBox.Split(' ')[3] == "12" ? "6" : "5";
        _defs.Append($"<marker id=\"{id}\" viewBox=\"{viewBox}\" refX=\"{SvgWriter.Num(refX)}\" refY=\"{refY}\" ")
             .Append($"markerWidth=\"{SvgWriter.Num(w)}\" markerHeight=\"{SvgWriter.Num(h)}\" orient=\"auto-start-reverse\">")
             .Append(body).Append("</marker>");
        _byKey[key] = id;
        return id;
    }

    private static (string Body, string ViewBox, double RefX, double W, double H) Body(MarkerShape shape, string color)
    {
        string c = SvgWriter.Attr(color);
        return shape switch
        {
            MarkerShape.ArrowOpen => (
                $"<polyline points=\"2,1.5 9,5 2,8.5\" fill=\"none\" stroke=\"{c}\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>",
                "0 0 10 10", 8, 7, 7),
            MarkerShape.Triangle => (
                $"<path d=\"M 1 1.5 L 11 6 L 1 10.5 Z\" fill=\"var(--beck-surface)\" stroke=\"{c}\" stroke-width=\"1.3\" stroke-linejoin=\"round\"/>",
                "0 0 12 12", 10.5, 10, 10),
            MarkerShape.Diamond => (
                $"<path d=\"M 1 5 L 7 1.2 L 13 5 L 7 8.8 Z\" fill=\"{c}\"/>",
                "0 0 14 10", 12.5, 11, 8),
            MarkerShape.DiamondOpen => (
                $"<path d=\"M 1 5 L 7 1.2 L 13 5 L 7 8.8 Z\" fill=\"var(--beck-surface)\" stroke=\"{c}\" stroke-width=\"1.3\" stroke-linejoin=\"round\"/>",
                "0 0 14 10", 12.5, 11, 8),
            _ => (
                $"<polygon points=\"0,1 10,5 0,9\" fill=\"{c}\"/>",
                "0 0 10 10", 8, 6, 6),
        };
    }
}
