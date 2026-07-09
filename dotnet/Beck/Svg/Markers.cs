using System.Text;

namespace Beck.Rendering.Svg;

/// <summary>
/// Edge end-marker defs — a port of the marker bodies in <c>src/route/svg.ts</c>, now
/// <em>style-aware</em> (<see cref="StyleEdges"/>): the arrowhead presentation (filled vs. hand-drawn
/// open-V vs. mono chevron) and the marker sizing model (default strokeWidth units vs. sanely-scaled
/// <c>userSpaceOnUse</c>) come from the style. Every shape points +x with its tip at <c>refX</c>, so
/// one def serves either end via <c>orient="auto-start-reverse"</c>. Deduped per (shape, color,
/// presentation, size); ids carry the content hash so multiple diagrams don't collide. At classic
/// edge settings (filled, scale 1, strokeWidth units) the emitted element is byte-identical to today.
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

    /// <summary>
    /// Ensure a marker for a shape+color under the given edge presentation exists; returns its id.
    /// <paramref name="edgeWidth"/> only matters when the style scales markers to the edge width
    /// (<see cref="StyleEdges.MarkerScaleToWidth"/>).
    /// </summary>
    public string Ensure(string color, MarkerShape shape, StyleEdges edges, double edgeWidth)
    {
        // The dedupe key carries the presentation + size discriminants so distinct styles never share a
        // marker — but at classic values (filled, scale 1, strokeWidth units) they collapse to the exact
        // historical "{shape}|{color}" behaviour, so dedupe order → _seq → ids stay byte-identical.
        string disc = edges.Arrow switch
        {
            EdgeArrow.OpenV => "|v",
            EdgeArrow.Chevron => "|c",
            _ => "",
        };
        string sizeDisc = edges.MarkerScaleToWidth
            ? "|w" + SvgWriter.Num(edgeWidth) + "x" + SvgWriter.Num(edges.MarkerScale)
            : edges.MarkerScale != 1.0 ? "|x" + SvgWriter.Num(edges.MarkerScale) : "";
        string key = $"{shape}|{color}{disc}{sizeDisc}";
        if (_byKey.TryGetValue(key, out string? hit)) return hit;

        string id = $"beck-arrow-{_seq++}-{_hash}";
        var (body, viewBox, refX, w, h) = Body(shape, color, edges);
        string refY = viewBox.Split(' ')[3] == "12" ? "6" : "5";
        // Marker size + units. Classic (no width-scaling, scale 1) emits the historical markerWidth/
        // markerHeight and NO markerUnits attribute (SVG default = strokeWidth) — byte-identical. A
        // scaling style either multiplies those strokeWidth-unit dims (MarkerScale) or switches to an
        // absolute userSpaceOnUse size that grows sub-linearly (√) with the edge width, so a thick line
        // no longer blows the arrowhead into a blob.
        string units;
        double mw, mh;
        if (edges.MarkerScaleToWidth)
        {
            double f = edges.MarkerScale * Math.Sqrt(Math.Max(edgeWidth, 1));
            (mw, mh) = (w * f, h * f);
            units = " markerUnits=\"userSpaceOnUse\"";
        }
        else
        {
            (mw, mh) = (w * edges.MarkerScale, h * edges.MarkerScale);
            units = "";
        }
        _defs.Append($"<marker id=\"{id}\" viewBox=\"{viewBox}\" refX=\"{SvgWriter.Num(refX)}\" refY=\"{refY}\"{units} ")
             .Append($"markerWidth=\"{SvgWriter.Num(mw)}\" markerHeight=\"{SvgWriter.Num(mh)}\" orient=\"auto-start-reverse\">")
             .Append(body).Append("</marker>");
        _byKey[key] = id;
        return id;
    }

    private static (string Body, string ViewBox, double RefX, double W, double H) Body(MarkerShape shape, string color, StyleEdges edges)
    {
        string c = SvgWriter.Attr(color);
        // OpenV (sketch): the plain filled arrowhead becomes TWO round-capped strokes running back from
        // the tip — a hand-drawn open V — as two separate <line> elements. Closed UML ends keep their
        // bodies (the inheritance triangle / composition diamond stay solid, per the sketch brief).
        if (edges.Arrow == EdgeArrow.OpenV && shape == MarkerShape.Arrow)
            return (
                $"<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"1.5\" stroke=\"{c}\" stroke-width=\"1.8\" stroke-linecap=\"round\"/>"
                + $"<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"8.5\" stroke=\"{c}\" stroke-width=\"1.8\" stroke-linecap=\"round\"/>",
                "0 0 12 10", 10, 8, 8);
        // Chevron (terminal): the plain arrowheads (the filled Arrow and the open ArrowOpen) become a mono
        // `>` — TWO hard butt-capped strokes running back from the tip. Butt caps (vs. OpenV's round) give
        // the crisp console read; the closed UML ends (Triangle/Diamond) fall through and keep their bodies,
        // so a class inheritance triangle stays closed exactly as in the mock. orient="auto-start-reverse"
        // (on every marker) makes a reply's reversed path draw the same glyph as `<` — no separate def.
        if (edges.Arrow == EdgeArrow.Chevron && shape is MarkerShape.Arrow or MarkerShape.ArrowOpen)
            return (
                $"<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"1.5\" stroke=\"{c}\" stroke-width=\"1.6\" stroke-linecap=\"butt\"/>"
                + $"<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"8.5\" stroke=\"{c}\" stroke-width=\"1.6\" stroke-linecap=\"butt\"/>",
                "0 0 12 10", 10, 8, 8);
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
