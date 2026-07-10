using System.Text.RegularExpressions;
using Beck.Authoring;
using BlazorMonaco;
using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using Microsoft.JSInterop;

namespace Beck.Docs.Client;

/// <summary>
/// Schema-aware Monaco completion + hover for Beck YAML, a C# port of the playground's
/// original JS IntelliSense. The vocabularies come from <see cref="BeckSchema"/> — which
/// itself sources value tokens from the engine — so completions can never suggest a token
/// the parser rejects. Registered once against Monaco's global <c>yaml</c> language.
/// </summary>
internal static class BeckCompletions
{
    // `<field>: <partial>` at the caret — captures the field name and the value fragment
    // typed so far (so the replace range covers exactly the fragment).
    private static readonly Regex _valueContext = new(@"(?:^|[\s{,])([A-Za-z_][\w-]*)\s*:\s*([\w-]*)$", RegexOptions.Compiled);
    private static readonly Regex _insideBrace = new(@"\{[^}]*$", RegexOptions.Compiled);
    private static readonly Regex _sectionHeader = new(@"^(meta|nodes|edges|groups|flow)\s*:", RegexOptions.Compiled);
    // Every `id:` value in the document — node and group ids are both valid edge endpoints
    // and group members, and `id:` is the field for both.
    private static readonly Regex _idDecl = new(@"(?:^|[\s{,])id\s*:\s*([A-Za-z0-9_-]+)", RegexOptions.Compiled);

    private static IJSRuntime? _js;
    private static bool _registered;

    /// <summary>Register the providers against Monaco's global <c>yaml</c> language (idempotent).</summary>
    public static async Task RegisterAsync(IJSRuntime js)
    {
        _js = js;
        if (_registered)
        {
            return;
        }

        _registered = true;

        await BlazorMonaco.Languages.Global.RegisterCompletionItemProvider(js,
            "yaml",
            new CompletionItemProvider(
                [":", " ", "-", "{", "[", ","],
                ProvideCompletionsAsync));

        await BlazorMonaco.Languages.Global.RegisterHoverProviderAsync(js, "yaml", ProvideHoverAsync);
    }

    // ---- completion ---------------------------------------------------------
    private static async Task<CompletionList> ProvideCompletionsAsync(string modelUri, Position position, CompletionContext _)
    {
        var empty = new CompletionList { Suggestions = [] };
        var model = await BlazorMonaco.Editor.Global.GetModel(_js!, modelUri);
        if (model is null)
        {
            return empty;
        }

        // Pass an explicit EOL preference — Monaco's getValue rejects a null one.
        var text = await model.GetValue(EndOfLinePreference.TextDefined, false);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var lineIdx = position.LineNumber - 1;
        if (lineIdx < 0 || lineIdx >= lines.Length)
        {
            return empty;
        }

        var line = lines[lineIdx];
        var col = position.Column; // 1-based; the char before the caret is line[col-2]
        var pre = line[..Math.Min(col - 1, line.Length)];
        var section = SectionAt(lines, lineIdx);

        // ---- value context: `<field>: <partial>` ----
        var vm = _valueContext.Match(pre);
        if (vm.Success)
        {
            var field = vm.Groups[1].Value;
            var partial = vm.Groups[2].Value;
            var vrange = new BlazorMonaco.Range
            {
                StartLineNumber = position.LineNumber,
                EndLineNumber = position.LineNumber,
                StartColumn = position.Column - partial.Length,
                EndColumn = position.Column,
            };

            if (BeckSchema.IdValuedFields.Contains(field))
            {
                return new CompletionList { Suggestions = Items(DeclaredIds(text), CompletionItemKind.Value, vrange, "id") };
            }

            var values = BeckSchema.ValuesFor(field, section);
            if (values is not null)
            {
                var kind = field == "icon" ? CompletionItemKind.Color : CompletionItemKind.EnumMember;
                return new CompletionList { Suggestions = Items(values, kind, vrange, field) };
            }
            return empty;
        }

        // ---- key context ----
        (var start, var end) = WordUntil(line, col);
        var krange = new BlazorMonaco.Range
        {
            StartLineNumber = position.LineNumber,
            EndLineNumber = position.LineNumber,
            StartColumn = start,
            EndColumn = end,
        };
        var insideBrace = _insideBrace.IsMatch(pre);
        var indent = pre.Length - pre.TrimStart().Length;

        var keys =
            insideBrace ? (section == "edges" ? BeckSchema.EdgeKeys : section == "groups" ? BeckSchema.GroupKeys : BeckSchema.NodeKeys)
            : indent == 0 ? BeckSchema.TopKeys
            : section == "meta" ? BeckSchema.MetaKeys
            : section == "edges" ? BeckSchema.EdgeKeys
            : section == "groups" ? BeckSchema.GroupKeys
            : section == "flow" ? BeckSchema.FlowSteps
            : BeckSchema.NodeKeys;

        var suggestions = keys.Select(k =>
        {
            var it = new CompletionItem
            {
                LabelAsString = k,
                Kind = CompletionItemKind.Property,
                InsertText = k + ": ",
                RangeAsObject = krange,
            };
            if (BeckSchema.Docs.TryGetValue(k, out var doc))
            {
                it.DocumentationAsObject = new MarkdownString { Value = doc };
            }

            return it;
        }).ToList();
        return new CompletionList { Suggestions = suggestions };
    }

    private static List<CompletionItem> Items(IReadOnlyList<string> values, CompletionItemKind kind, BlazorMonaco.Range range, string detail)
        => values.Select(v =>
        {
            var it = new CompletionItem
            {
                LabelAsString = v,
                Kind = kind,
                InsertText = v,
                Detail = detail,
                RangeAsObject = range,
            };
            if (BeckSchema.Docs.TryGetValue(v, out var doc))
            {
                it.DocumentationAsObject = new MarkdownString { Value = doc };
            }

            return it;
        }).ToList();

    // ---- hover --------------------------------------------------------------
    private static async Task<Hover?> ProvideHoverAsync(string modelUri, Position position, HoverContext _)
    {
        var model = await BlazorMonaco.Editor.Global.GetModel(_js!, modelUri);
        if (model is null)
        {
            return null;
        }

        var word = await model.GetWordAtPosition(position);
        if (word is null || !BeckSchema.Docs.TryGetValue(word.Word, out var doc))
        {
            return null;
        }

        return new Hover
        {
            Contents = [new MarkdownString { Value = $"**{word.Word}** — {doc}" }],
            Range = new BlazorMonaco.Range
            {
                StartLineNumber = position.LineNumber,
                EndLineNumber = position.LineNumber,
                StartColumn = word.StartColumn,
                EndColumn = word.EndColumn,
            },
        };
    }

    // ---- context helpers ----------------------------------------------------
    // Nearest top-level section header at or above `lineIdx` (block + flow styles).
    private static string? SectionAt(string[] lines, int lineIdx)
    {
        for (var i = lineIdx; i >= 0; i--)
        {
            var m = _sectionHeader.Match(lines[i]);
            if (m.Success)
            {
                return m.Groups[1].Value;
            }
        }
        return null;
    }

    private static IReadOnlyList<string> DeclaredIds(string text)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>();
        foreach (Match m in _idDecl.Matches(text))
        {
            var id = m.Groups[1].Value;
            if (seen.Add(id))
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    // Monaco getWordUntilPosition: the run of identifier chars ending at the caret. Returns
    // 1-based [start, end) columns; when no word is being typed both equal the caret column.
    private static (int Start, int End) WordUntil(string line, int col)
    {
        var caret = col - 1; // 0-based index of the char after the caret
        var start = caret;
        while (start > 0 && IsWordChar(line[start - 1]))
        {
            start--;
        }

        return (start + 1, col);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}