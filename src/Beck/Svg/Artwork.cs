using System.Text;

namespace Beck.Svg;

/// <summary>
/// The <see cref="StyleArtwork"/> shape seam: emits a node/group/pseudo-state outline as either the
/// classic straight primitive (<see cref="StyleArtwork.Plain"/>) or a hand-drawn wobbly closed
/// <c>&lt;path&gt;</c> (<see cref="StyleArtwork.Sketch"/>). The Plain branch reproduces the exact
/// historical element string byte-for-byte, so any style on <see cref="StyleArtwork.Plain"/> (classic
/// included) is untouched. The Sketch wobble is baked into the path geometry and derived entirely from
/// a deterministic seed (the diagram's content hash + the element's id), so the same input renders the
/// same wobble forever — no RNG, no time, nothing that animates continuously.
/// </summary>
internal static class Artwork
{
    private static string N(double n) => SvgWriter.Num(n);

    /// <summary>
    /// A node/group rect: a straight rounded <c>&lt;rect&gt;</c> under <see cref="StyleArtwork.Plain"/>,
    /// or a wobbly closed <c>&lt;path&gt;</c> carrying the same <paramref name="cls"/> (so every
    /// token-driven fill/stroke/filter still applies) under <see cref="StyleArtwork.Sketch"/>.
    /// <paramref name="styleAttr"/> is an optional inline <c>style="…"</c> value (the group box's
    /// per-accent stroke). <paramref name="seed"/> keys the deterministic jitter. When
    /// <paramref name="shadow"/> is <c>true</c> and the style is <see cref="StyleArtwork.Brutalist"/>,
    /// a solid blur-free offset shadow rect (filled through <c>var(--beck-shadow, …)</c>) is emitted
    /// <em>before</em> — i.e. behind — this rect; the node emitters pass <c>true</c> for card/pill/
    /// class shapes and leave it <c>false</c> for group boxes and ghost/pseudo-state chrome.
    /// </summary>
    public static string Rect(BeckStyle style, string cls, double x, double y, double w, double h,
        double rx, string seed, string? styleAttr = null, bool shadow = false)
    {
        var sa = styleAttr != null ? $" style=\"{styleAttr}\"" : "";
        var sh = Behind(style, shadow, x, y, w, h, rx);
        if (style.Artwork != StyleArtwork.Sketch)
        {
            return $"{sh}<rect class=\"{cls}\" x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(rx)}\"{sa}/>";
        }

        return $"{sh}<path class=\"{cls}\" d=\"{WobbleRoundRect(x, y, w, h, rx, seed)}\"{sa}/>";
    }

    /// <summary>
    /// The behind-the-node depth treatment for the two "slab" artwork families, both drawn
    /// <em>before</em> (behind) the node rect and both filled through <c>--beck-*</c> tokens so no
    /// resolved literal ever touches shape CSS:
    /// <list type="bullet">
    /// <item><see cref="StyleArtwork.Brutalist"/> — a single solid, blur-free rect offset down-right
    /// by <see cref="StyleGeometry.ShadowOffset"/> px (the hard "sticker" shadow), filled through
    /// <c>--beck-shadow</c>.</item>
    /// <item><see cref="StyleArtwork.Extruded"/> — two solid parallelogram faces (right + bottom)
    /// offset down-right by <see cref="StyleGeometry.DepthOffset"/> px as if lit from the top-left,
    /// filled through <c>--beck-depth-right</c> / <c>--beck-depth-bottom</c> (a darker
    /// <c>color-mix</c> of the node surface).</item>
    /// </list>
    /// <item><see cref="StyleArtwork.Circuit"/> — a deterministic ladder of short chip <em>pin
    /// stubs</em> down the node's left and right edges (offset out by
    /// <see cref="StyleGeometry.PinLength"/> px), filled through <c>--beck-pin</c>.</item>
    /// No stroke on either, so only the offset sliver shows past the node's opaque fill. Empty for
    /// every other style (or a zero offset) — byte-identical.
    /// </summary>
    private static string Behind(BeckStyle style, bool want, double x, double y, double w, double h, double rx)
    {
        if (!want)
        {
            return "";
        }

        if (style.Artwork == StyleArtwork.Circuit)
        {
            var len = style.Geometry.PinLength;
            if (len <= 0)
            {
                return "";
            }

            double t = style.Geometry.PinThickness, tuck = 2;
            // Pin count per side derives from the node height (deterministic, no measurement jitter):
            // clamp(round(h / pitch), 2, 6). Pins are evenly distributed down each edge.
            var n = (int)Math.Round(h / style.Geometry.PinPitch, MidpointRounding.AwayFromZero);
            n = Math.Max(2, Math.Min(6, n));
            var sb = new StringBuilder();
            var fill = "fill:var(--beck-pin, var(--beck-edge))";
            for (var i = 0; i < n; i++)
            {
                var cy = y + h * (i + 0.5) / n;
                var py = cy - t / 2;
                // Left pin: protrudes to x-len, tucks `tuck` px under the node (covered by its fill).
                sb.Append($"<rect class=\"beck-pin\" x=\"{N(x - len)}\" y=\"{N(py)}\" width=\"{N(len + tuck)}\" height=\"{N(t)}\" rx=\"{N(t / 2)}\" style=\"{fill}\"/>");
                // Right pin: mirror on the far edge.
                sb.Append($"<rect class=\"beck-pin\" x=\"{N(x + w - tuck)}\" y=\"{N(py)}\" width=\"{N(len + tuck)}\" height=\"{N(t)}\" rx=\"{N(t / 2)}\" style=\"{fill}\"/>");
            }
            return sb.ToString();
        }
        if (style.Artwork == StyleArtwork.Brutalist)
        {
            var o = style.Geometry.ShadowOffset;
            if (o <= 0)
            {
                return "";
            }

            return $"<rect class=\"beck-shadow\" x=\"{N(x + o)}\" y=\"{N(y + o)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(rx)}\" style=\"fill:var(--beck-shadow, var(--beck-node-border))\"/>";
        }
        if (style.Artwork == StyleArtwork.Extruded)
        {
            var d = style.Geometry.DepthOffset;
            if (d <= 0)
            {
                return "";
            }

            double xr = x + w, yb = y + h;
            // Bottom face first, then the right face on top at their shared far corner; light source
            // top-left ⇒ the bottom face reads darkest. Both are closed parallelograms whose front
            // edge sits exactly on the node's edge (the opaque node fill covers it), so only the
            // down-right depth sliver shows. All coordinates are node-local and ≥ 0.
            var bottom = $"<path class=\"beck-depth beck-depth--bottom\" d=\"M{N(x)} {N(yb)}L{N(xr)} {N(yb)}L{N(xr + d)} {N(yb + d)}L{N(x + d)} {N(yb + d)}Z\" style=\"fill:var(--beck-depth-bottom, var(--beck-node-border))\"/>";
            var right = $"<path class=\"beck-depth beck-depth--right\" d=\"M{N(xr)} {N(y)}L{N(xr + d)} {N(y + d)}L{N(xr + d)} {N(yb + d)}L{N(xr)} {N(yb)}Z\" style=\"fill:var(--beck-depth-right, var(--beck-node-border))\"/>";
            return bottom + right;
        }
        return "";
    }

    /// <summary>
    /// A diamond node (flowchart decision groundwork): a clean four-vertex closed <c>&lt;path&gt;</c>
    /// through the bbox face midpoints under <see cref="StyleArtwork.Plain"/>, or a wobbly variant
    /// under <see cref="StyleArtwork.Sketch"/>. The four vertices sit exactly on the bbox face
    /// midpoints, so the router's default face anchors coincide with them. Depth (<paramref name="shadow"/>)
    /// reuses the brutalist hard-offset sticker shadow, offset as a polygon rather than a rect so it
    /// stays coherent with the shape; extruded/circuit depth is rect-edge specific and omitted here.
    /// </summary>
    public static string Diamond(BeckStyle style, string cls, double x, double y, double w, double h,
        string seed, string? styleAttr = null, bool shadow = false)
    {
        var sa = styleAttr != null ? $" style=\"{styleAttr}\"" : "";
        double cx = x + w / 2, cy = y + h / 2;
        var pts = new (double X, double Y)[] { (cx, y), (x + w, cy), (cx, y + h), (x, cy) };
        var sh = BehindPoly(style, shadow, pts);
        if (style.Artwork != StyleArtwork.Sketch)
        {
            return $"{sh}<path class=\"{cls}\" d=\"{Poly(pts)}\"{sa}/>";
        }

        return $"{sh}<path class=\"{cls}\" d=\"{WobblePolygon(pts, Math.Max(w, h), seed)}\"{sa}/>";
    }

    /// <summary>
    /// A parallelogram node (flowchart I/O groundwork): a four-corner closed <c>&lt;path&gt;</c> with a
    /// fixed horizontal skew of <c>min(12, h·0.4)</c>, the top edge shifted right. Plain draws a clean
    /// polygon; sketch wobbles it. The top/bottom edges lie on the bbox faces (so those anchors need no
    /// adjustment); the left/right faces are slanted (the router shifts those anchors by skew/2).
    /// Depth reuses the brutalist offset-polygon shadow as for <see cref="Diamond"/>.
    /// </summary>
    public static string Parallelogram(BeckStyle style, string cls, double x, double y, double w, double h,
        string seed, string? styleAttr = null, bool shadow = false)
    {
        var sa = styleAttr != null ? $" style=\"{styleAttr}\"" : "";
        var skew = ParallelogramSkew(h);
        var pts = new (double X, double Y)[]
        {
            (x + skew, y), (x + w, y), (x + w - skew, y + h), (x, y + h),
        };
        var sh = BehindPoly(style, shadow, pts);
        if (style.Artwork != StyleArtwork.Sketch)
        {
            return $"{sh}<path class=\"{cls}\" d=\"{Poly(pts)}\"{sa}/>";
        }

        return $"{sh}<path class=\"{cls}\" d=\"{WobblePolygon(pts, Math.Max(w, h), seed)}\"{sa}/>";
    }

    /// <summary>The parallelogram's fixed horizontal skew — the amount the top edge shifts right (and the
    /// bottom edge left). The router adds <c>skew/2</c> to left/right anchors so they land on the slant.</summary>
    public static double ParallelogramSkew(double h) => Math.Min(12, h * 0.4);

    /// <summary>A closed straight polygon path (<c>M…L…L…Z</c>) through <paramref name="pts"/>.</summary>
    private static string Poly(IReadOnlyList<(double X, double Y)> pts)
    {
        var sb = new StringBuilder();
        sb.Append('M').Append(F(pts[0]));
        for (var i = 1; i < pts.Count; i++)
        {
            sb.Append('L').Append(F(pts[i]));
        }

        sb.Append('Z');
        return sb.ToString();
    }

    /// <summary>
    /// The behind-the-node depth treatment for arbitrary polygon shapes (diamond/parallelogram). Only
    /// <see cref="StyleArtwork.Brutalist"/>'s hard offset "sticker" shadow generalizes cleanly — the same
    /// solid, blur-free polygon offset down-right by <see cref="StyleGeometry.ShadowOffset"/> px, filled
    /// through <c>--beck-shadow</c>. Extruded's two lit faces and circuit's edge pins are rect-edge
    /// specific, so they are omitted for these shapes (byte-identical for every other style / zero offset).
    /// </summary>
    private static string BehindPoly(BeckStyle style, bool want, IReadOnlyList<(double X, double Y)> pts)
    {
        if (!want || style.Artwork != StyleArtwork.Brutalist)
        {
            return "";
        }

        var o = style.Geometry.ShadowOffset;
        if (o <= 0)
        {
            return "";
        }

        var shifted = pts.Select(p => (p.X + o, p.Y + o)).ToList();
        return $"<path class=\"beck-shadow\" d=\"{Poly(shifted)}\" style=\"fill:var(--beck-shadow, var(--beck-node-border))\"/>";
    }

    /// <summary>
    /// A pseudo-state circle (start/end nodes, the end dot): a true <c>&lt;circle&gt;</c> under
    /// <see cref="StyleArtwork.Plain"/>, or a wobbly closed blob <c>&lt;path&gt;</c> with the same
    /// <paramref name="cls"/> under <see cref="StyleArtwork.Sketch"/>.
    /// </summary>
    public static string Circle(BeckStyle style, string cls, double cx, double cy, double r, string seed)
    {
        if (style.Artwork != StyleArtwork.Sketch)
        {
            return $"<circle class=\"{cls}\" cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(r)}\"/>";
        }

        return $"<path class=\"{cls}\" d=\"{WobbleCircle(cx, cy, r, seed)}\"/>";
    }

    /// <summary>
    /// A metro <em>station dot</em> at an edge anchor point (<see cref="StyleArtwork.Metro"/>): a
    /// filled circle whose fill is the token surface (the "white station") and whose ring takes
    /// <paramref name="ringColor"/> — the edge's own colour, so each transit line's stations match its
    /// hue — drawn over the line. Read straight off the already-computed route geometry by the edge/
    /// message emitters (the router is untouched); the dots are additional sibling elements, never a
    /// split edge path. Returns <c>""</c> for every non-metro style or a zero
    /// <see cref="StyleGeometry.StationRadius"/> — byte-identical.
    /// </summary>
    public static string Station(BeckStyle style, double cx, double cy, string ringColor)
    {
        if (style.Artwork != StyleArtwork.Metro)
        {
            return "";
        }

        var r = style.Geometry.StationRadius;
        if (r <= 0)
        {
            return "";
        }

        return $"<circle class=\"beck-station\" cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(r)}\" "
             + $"style=\"fill:var(--beck-station-fill, var(--beck-surface));stroke:{SvgWriter.Attr(ringColor)};stroke-width:{N(style.Geometry.StationRing)}\"/>";
    }

    /// <summary>
    /// A blueprint <em>dimension line</em> along a group box's top edge (<see cref="StyleArtwork.Blueprint"/>):
    /// a thin horizontal extension rule offset above the edge by <see cref="StyleGeometry.DimensionTick"/> px,
    /// plus two short perpendicular witness ticks rising from the box's top-left and top-right corners past
    /// the rule — the classic drafted-drawing measured-length annotation. Token-coloured through
    /// <c>var(--beck-dimension, var(--beck-group-border))</c> (the fallback keeps it themed even if a custom
    /// style enables ticks without defining the token), stroked at the style's hairline width. Read purely
    /// from the already-computed group rect (<paramref name="x"/>,<paramref name="y"/>,<paramref name="w"/>) —
    /// no router involvement — and drawn inside the group layer, so it sits behind edges and nodes. Returns
    /// <c>""</c> for every non-blueprint style or a zero <see cref="StyleGeometry.DimensionTick"/> —
    /// byte-identical. All coordinates stay non-negative for the corpus's well-margined group boxes.
    /// </summary>
    public static string GroupDimension(BeckStyle style, double x, double y, double w)
    {
        if (style.Artwork != StyleArtwork.Blueprint)
        {
            return "";
        }

        var gap = style.Geometry.DimensionTick;
        if (gap <= 0)
        {
            return "";
        }

        var over = gap / 3;           // witness overshoot past the dimension rule
        var dy = y - gap;             // the dimension rule sits `gap` px above the top edge
        var tickTop = dy - over;      // witness ticks run from the box edge (y) up past the rule
        var col = "stroke:var(--beck-dimension, var(--beck-group-border))";
        var sw = $"stroke-width:{N(style.Geometry.HairlineStroke)}";
        var sb = new StringBuilder();
        sb.Append("<g class=\"beck-dimension\" style=\"fill:none;").Append(col).Append(';').Append(sw).Append("\">");
        // The extension/dimension rule, parallel to the measured top edge.
        sb.Append($"<line x1=\"{N(x)}\" y1=\"{N(dy)}\" x2=\"{N(x + w)}\" y2=\"{N(dy)}\"/>");
        // Two perpendicular witness ticks at each end, from the box corner up past the rule.
        sb.Append($"<line x1=\"{N(x)}\" y1=\"{N(y)}\" x2=\"{N(x)}\" y2=\"{N(tickTop)}\"/>");
        sb.Append($"<line x1=\"{N(x + w)}\" y1=\"{N(y)}\" x2=\"{N(x + w)}\" y2=\"{N(tickTop)}\"/>");
        sb.Append("</g>");
        return sb.ToString();
    }

    /// <summary>
    /// The sketch <em>crayon fill</em>: one continuous back-and-forth colouring pass — even
    /// horizontal rows sweeping the full box width with a fat round-capped stroke, hooking down at
    /// each edge to the next row, like a patient five-year-old filling in a shape. The geometry stays
    /// deliberately smooth and gently jittered; all the wax texture comes from the shared crayon
    /// filter (<c>Stylesheet.StyleDefs</c>): turbulence-displaced pressure edges plus a grain-mask
    /// that eats waxy holes so the paper shows through. Rows inset where they cross the corner
    /// radius, so the fill hugs rounded cards and pills alike. Stroke width scales with the box
    /// (emitted per-path, not in the class rule) and row pitch tracks it for near-total coverage.
    /// Every coordinate comes from the deterministic seed stream (its own <c>:scribble</c> suffix,
    /// so the outline wobble is untouched) — the same YAML scribbles the same way forever. Returns
    /// <c>""</c> for every non-sketch artwork — byte-identical.
    /// </summary>
    public static string Scribble(BeckStyle style, double x, double y, double w, double h, double r, string seed)
    {
        if (style.Artwork != StyleArtwork.Sketch)
        {
            return "";
        }
        // Inset covers the filter's reach: the displacement map shoves wax up to ~scale/2 px outward,
        // so anything tighter lets specks land outside the wobbly border.
        const double Pad = 4;
        double ix = x + Pad, iy = y + Pad, iw = w - 2 * Pad, ih = h - 2 * Pad;
        if (iw < 12 || ih < 8)
        {
            return "";
        }

        var rng = new Rng(seed + ":scribble");
        var rr = Math.Max(0, Math.Min(r - Pad / 2, Math.Min(iw, ih) / 2));
        // Fat crayon: width scales with the box so a short chip and a tall card both read as the
        // same gentle pressure; rows overlap slightly (pitch < width) so coverage is near-total and
        // the grain mask alone decides where paper peeks through.
        var sw = Math.Max(5.5, Math.Min(11, ih / 5.5));
        var pitch = sw * 0.88;
        // Horizontal extent of a row at height yy, pulled in where it crosses the corner circles.
        (double L, double R) Extent(double yy)
        {
            var d = Math.Min(yy - iy, iy + ih - yy);
            var inset = d < rr ? rr - Math.Sqrt(Math.Max(0, rr * rr - (rr - d) * (rr - d))) : 0;
            return (ix + inset, ix + iw - inset);
        }

        var sb = new StringBuilder();
        var rightward = rng.Next() < 0.5;
        double yTop = iy + sw * 0.45, yBot = iy + ih - sw * 0.45;
        var first = true;
        for (var yy = yTop; yy <= yBot + pitch * 0.4; yy += pitch)
        {
            var rowY = Math.Min(yBot, yy) + (rng.Next() - 0.5) * sw * 0.25;
            var (xl, xr) = Extent(rowY);
            double x0 = rightward ? xl : xr, x1 = rightward ? xr : xl;
            // Small per-row overshoot jitter at each end — no two rows start or stop flush.
            x0 += (rightward ? 1 : -1) * rng.Next() * sw * 0.35;
            x1 -= (rightward ? 1 : -1) * rng.Next() * sw * 0.35;
            var yEnd = rowY + (rng.Next() - 0.5) * sw * 0.35;
            if (first) { sb.Append('M').Append(F((x0, rowY))); first = false; }
            else
            {
                sb.Append('L').Append(F((x0, rowY)));   // the edge hook down from the previous row
            }
            // The sweep itself: a lazy cubic with two mid controls drifting a touch off the row line.
            sb.Append('C').Append(F((x0 + (x1 - x0) / 3, rowY + (rng.Next() - 0.5) * sw * 0.5))).Append(' ')
              .Append(F((x0 + 2 * (x1 - x0) / 3, rowY + (rng.Next() - 0.5) * sw * 0.5))).Append(' ')
              .Append(F((x1, yEnd)));
            rightward = !rightward;
        }
        if (first)
        {
            return "";
        }

        return $"<path class=\"beck-scribble\" d=\"{sb}\" stroke-width=\"{N(Math.Round(sw, 2))}\"/>";
    }

    // ---- deterministic wobble geometry ----

    /// <summary>
    /// A rounded-rect outline whose eight tangent endpoints are jittered and whose four straight
    /// edges bow through a jittered midpoint, with the true rectangle corners as the quadratic corner
    /// controls (so corners stay rounded and sane). One continuous closed path — a drop-in for the
    /// rect it replaces. Amplitudes are a few px (rough.js at low roughness).
    /// </summary>
    private static string WobbleRoundRect(double x, double y, double w, double h, double r, string seed)
    {
        var rng = new Rng(seed);
        // Per-node corner-radius variety (sketch, brief §1b: rx 6–9): a rectangular card carries a small
        // incoming radius (sketch sets card/class/ghost to 8), so give it a hash-derived rounding in [6,9]
        // — no two cards round alike, the classic hand-drawn tell. Pills (large h/2 radius) and group boxes
        // (larger radius) keep their shape. Drawn from the same deterministic seed stream, so it's stable
        // forever and consumes one value before the wobble (which is why the sketch golden was regenerated).
        if (r <= 10)
        {
            r = 6 + Math.Floor(rng.Next() * 4);
        }

        r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2));
        // Amplitude grows with node size so the hand-drawn wobble stays legible on large rectangular
        // cards/group boxes (where a fixed few-px jitter proportionally vanished and the sketch read
        // rested on the Shantell font alone) while small pills/ghosts stay subtly wobbled. Keyed off the
        // long dimension: s ramps 0→1 from ~90px to ~310px, lifting the endpoint jitter ~2.2→3.6 and the
        // edge bow ~1.8→2.9. Still a few px — deep inside the card's ≥14px padding, so text still fits
        // (the wobble perturbs only the outline path, never the measured content box).
        var s = Math.Clamp((Math.Max(w, h) - 90) / 220.0, 0, 1);
        var a = 2.2 + 1.4 * s;   // endpoint jitter amplitude
        var b = 1.8 + 1.1 * s;   // edge-bow amplitude
        double J() => (rng.Next() - 0.5) * 2 * a;
        double Bow() => (rng.Next() - 0.5) * 2 * b;

        double x0 = x + r, x1 = x + w - r, y0 = y + r, y1 = y + h - r;
        // Tangent endpoints, clockwise, each jittered a touch off the true rounded-rect tangent.
        (double X, double Y) tlt = (x0 + J(), y + J());     // top edge, left end
        (double X, double Y) trt = (x1 + J(), y + J());     // top edge, right end
        (double X, double Y) trr = (x + w + J(), y0 + J()); // right edge, top end
        (double X, double Y) brr = (x + w + J(), y1 + J()); // right edge, bottom end
        (double X, double Y) brb = (x1 + J(), y + h + J()); // bottom edge, right end
        (double X, double Y) blb = (x0 + J(), y + h + J()); // bottom edge, left end
        (double X, double Y) bll = (x + J(), y1 + J());     // left edge, bottom end
        (double X, double Y) tll = (x + J(), y0 + J());     // left edge, top end

        var sb = new StringBuilder();
        sb.Append('M').Append(F(tlt));
        sb.Append('Q').Append(F(((x0 + x1) / 2, y + Bow()))).Append(' ').Append(F(trt));       // top edge
        sb.Append('Q').Append(F((x + w, y))).Append(' ').Append(F(trr));                       // TR corner
        sb.Append('Q').Append(F((x + w + Bow(), (y0 + y1) / 2))).Append(' ').Append(F(brr));   // right edge
        sb.Append('Q').Append(F((x + w, y + h))).Append(' ').Append(F(brb));                   // BR corner
        sb.Append('Q').Append(F(((x0 + x1) / 2, y + h + Bow()))).Append(' ').Append(F(blb));   // bottom edge
        sb.Append('Q').Append(F((x, y + h))).Append(' ').Append(F(bll));                       // BL corner
        sb.Append('Q').Append(F((x + Bow(), (y0 + y1) / 2))).Append(' ').Append(F(tll));       // left edge
        sb.Append('Q').Append(F((x, y))).Append(' ').Append(F(tlt));                           // TL corner
        sb.Append('Z');
        return sb.ToString();
    }

    /// <summary>A closed hand-drawn circle: N points at jittered radius, smoothed through the segment
    /// midpoints (each vertex is a quadratic control), so the blob stays smooth and closed.</summary>
    private static string WobbleCircle(double cx, double cy, double r, string seed)
    {
        var rng = new Rng(seed);
        const int N = 10;
        var amp = Math.Min(1.6, r * 0.22);
        var p = new (double X, double Y)[N];
        for (var i = 0; i < N; i++)
        {
            var ang = 2 * Math.PI * i / N;
            var rr = r + (rng.Next() - 0.5) * 2 * amp;
            p[i] = (cx + rr * Math.Cos(ang), cy + rr * Math.Sin(ang));
        }
        (double X, double Y) Mid(int i, int j) => ((p[i].X + p[j].X) / 2, (p[i].Y + p[j].Y) / 2);

        var sb = new StringBuilder();
        sb.Append('M').Append(F(Mid(N - 1, 0)));
        for (var i = 0; i < N; i++)
        {
            sb.Append('Q').Append(F(p[i])).Append(' ').Append(F(Mid(i, (i + 1) % N)));
        }

        sb.Append('Z');
        return sb.ToString();
    }

    /// <summary>
    /// A closed hand-drawn polygon: each vertex jittered a touch off true, each straight edge bowed
    /// through a jittered midpoint (the true midpoint as the quadratic control), so the shape stays
    /// closed and gently hand-drawn. Amplitudes track the shape's long dimension exactly as
    /// <see cref="WobbleRoundRect"/> does, and every value comes from the deterministic
    /// <paramref name="seed"/> stream — no RNG, no time. Used by <see cref="Diamond"/>/<see cref="Parallelogram"/>.
    /// </summary>
    private static string WobblePolygon(IReadOnlyList<(double X, double Y)> pts, double longDim, string seed)
    {
        var rng = new Rng(seed);
        var s = Math.Clamp((longDim - 90) / 220.0, 0, 1);
        var a = 2.2 + 1.4 * s;   // vertex jitter amplitude
        var b = 1.8 + 1.1 * s;   // edge-bow amplitude
        double J() => (rng.Next() - 0.5) * 2 * a;
        double Bow() => (rng.Next() - 0.5) * 2 * b;

        var v = pts.Select(p => (X: p.X + J(), Y: p.Y + J())).ToList();
        var sb = new StringBuilder();
        sb.Append('M').Append(F(v[0]));
        for (var i = 0; i < v.Count; i++)
        {
            var p = v[i];
            var q = v[(i + 1) % v.Count];
            (double X, double Y) mid = ((p.X + q.X) / 2 + Bow(), (p.Y + q.Y) / 2 + Bow());
            sb.Append('Q').Append(F(mid)).Append(' ').Append(F(q));
        }

        sb.Append('Z');
        return sb.ToString();
    }

    private static string F((double X, double Y) pt) => N(pt.X) + " " + N(pt.Y);
}