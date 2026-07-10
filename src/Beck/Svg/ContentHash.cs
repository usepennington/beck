using System.Security.Cryptography;
using System.Text;

namespace Beck.Svg;

/// <summary>
/// Derives the 8-character content hash that scopes every id, CSS class, and
/// keyframe name in a rendered SVG (design decision D8). Keyframe names and
/// <c>url(#…)</c> ids are document-global, so multiple diagrams on one page must
/// not collide — and the suffix is a hash of (YAML + options), never a counter
/// or timestamp, so identical input yields byte-identical output (goal G6).
/// </summary>
public static class ContentHash
{
    /// <summary>
    /// An 8-character lowercase hex suffix derived from <paramref name="content"/>.
    /// Stable across runs, processes, and machines for the same input.
    /// </summary>
    public static string Of(string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        var sb = new StringBuilder(8);
        for (int i = 0; i < 4; i++)
            sb.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
