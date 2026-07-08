using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Beck;

/// <summary>
/// Builds a <c>type: class</c> Beck diagram — UML class cards (name, fields,
/// methods) joined by inheritance / composition / association relations.
/// Build cards by hand with <see cref="Class(string, Action{ClassBuilder})"/>, or reflect
/// real CLR types with <see cref="FromTypes"/> so the diagram can never drift
/// from the code: <c>ClassDiagramBuilder.FromTypes(typeof(Order), typeof(Customer)).ToFence()</c>.
/// </summary>
/// <example>
/// <code>
/// var fence = new ClassDiagramBuilder("Order Model")
///     .Class("order", c => c.Name("Order").Stereotype("aggregate")
///         .Field("Status: OrderStatus").Method("Submit()"))
///     .Class("line", c => c.Name("OrderLine").Field("Sku: string"))
///     .Composition("order", "line", fromCard: "1", toCard: "*")
///     .ToFence();
/// </code>
/// </example>
public sealed class ClassDiagramBuilder
{
    private readonly List<ClassBuilder> _classes = new();
    private readonly List<GroupBuilder> _groups = new();
    private readonly List<string> _relations = new();
    private readonly List<(string From, string To)> _relationEndpoints = new();
    private readonly MetaOptions _meta = new();
    private FlowBuilder? _flow;

    /// <summary>Create an empty class diagram.</summary>
    public ClassDiagramBuilder() { }

    /// <summary>Create a class diagram with a title.</summary>
    public ClassDiagramBuilder(string title) => _meta.Title = title;

    /// <summary>
    /// Reflect CLR types into cards and infer the relations among them — base
    /// types become <c>inherits</c>, interfaces <c>implements</c>, property
    /// types labelled associations (collections get a <c>*</c> multiplicity),
    /// enums «enum» cards. Types outside the set are ignored, so the diagram
    /// stays scoped to what you pass in.
    /// </summary>
    public static ClassDiagramBuilder FromTypes(params Type[] types) => new ClassDiagramBuilder().AddTypes(types);

    /// <summary>Set the diagram title.</summary>
    public ClassDiagramBuilder Title(string title) { _meta.Title = title; return this; }

    /// <summary>Set the diagram subtitle.</summary>
    public ClassDiagramBuilder Subtitle(string subtitle) { _meta.Subtitle = subtitle; return this; }

    /// <summary>Set the layout direction — <see cref="Beck.Direction.TB"/> (default) reads the hierarchy top-down.</summary>
    public ClassDiagramBuilder Direction(Direction direction) { _meta.Direction = direction; return this; }

    /// <summary>Set the theme: <see cref="ThemeMode.Auto"/> (default), <see cref="ThemeMode.Light"/>, or <see cref="ThemeMode.Dark"/>.</summary>
    public ClassDiagramBuilder Theme(ThemeMode theme) { _meta.Theme = theme; return this; }

    /// <summary>Enable or disable the flow animation.</summary>
    public ClassDiagramBuilder Animate(bool animate) { _meta.Animate = animate; return this; }

    /// <summary>Loop the flow (default) or play it through once.</summary>
    public ClassDiagramBuilder Loop(bool loop) { _meta.Loop = loop; return this; }

    /// <summary>How the diagram behaves when wider than its container.</summary>
    public ClassDiagramBuilder Fit(FitMode fit) { _meta.Fit = fit; return this; }

    /// <summary>Toggle + tune the narration caption (drive it with explicit
    /// <see cref="FlowBuilder.Narrate"/> steps); the knobs pace each caption's
    /// on-screen time by its length.</summary>
    public ClassDiagramBuilder Narrate(bool enabled = true, int? wpm = null, double? min = null, double? pad = null)
    {
        _meta.Narrate = enabled;
        if (wpm is { } w) _meta.NarrateWpm = w;
        if (min is { } m) _meta.NarrateMin = m;
        if (pad is { } p) _meta.NarratePad = p;
        return this;
    }

    /// <summary>Tune layout spacing: rank gap (along the hierarchy), node gap (across), and corner radius (px).</summary>
    public ClassDiagramBuilder Spacing(int? rank = null, int? node = null, int? cornerRadius = null)
    {
        if (rank is { } r) _meta.SpacingRank = r;
        if (node is { } n) _meta.SpacingNode = n;
        if (cornerRadius is { } c) _meta.SpacingCornerRadius = c;
        return this;
    }

    /// <summary>Add a class card and configure it via <see cref="ClassBuilder"/> — name, stereotype, fields, methods.</summary>
    public ClassDiagramBuilder Class(string id, Action<ClassBuilder>? configure = null)
    {
        var c = new ClassBuilder(id);
        configure?.Invoke(c);
        _classes.Add(c);
        return this;
    }

    /// <summary>The terse overload: add a class card with a display name.</summary>
    public ClassDiagramBuilder Class(string id, string name)
    {
        _classes.Add(new ClassBuilder(id).Name(name));
        return this;
    }

    /// <summary>Add a labelled boundary — a namespace or module box around related classes.</summary>
    public ClassDiagramBuilder Group(string id, Action<GroupBuilder> configure)
    {
        var g = new GroupBuilder(id);
        configure(g);
        _groups.Add(g);
        return this;
    }

    /// <summary>Child extends parent — solid line, hollow triangle at the parent.</summary>
    public ClassDiagramBuilder Inherits(string child, string parent)
        => Relation(child, parent, RelationKind.Inherits, null);

    /// <summary>Class implements an interface — dashed line, hollow triangle at the interface.</summary>
    public ClassDiagramBuilder Implements(string child, string iface)
        => Relation(child, iface, RelationKind.Implements, null);

    /// <summary>A plain directed association, with optional multiplicities at each end.</summary>
    public ClassDiagramBuilder Association(string from, string to, string? label = null, string? fromCard = null, string? toCard = null)
        => Relation(from, to, RelationKind.Association, label, fromCard, toCard);

    /// <summary>Whole–part where the part outlives the whole — hollow diamond at the whole.</summary>
    public ClassDiagramBuilder Aggregation(string whole, string part, string? label = null, string? fromCard = null, string? toCard = null)
        => Relation(whole, part, RelationKind.Aggregation, label, fromCard, toCard);

    /// <summary>Whole–part where the part's lifetime is owned — filled diamond at the whole.</summary>
    public ClassDiagramBuilder Composition(string whole, string part, string? label = null, string? fromCard = null, string? toCard = null)
        => Relation(whole, part, RelationKind.Composition, label, fromCard, toCard);

    /// <summary>A usage dependency — dashed line, open arrowhead.</summary>
    public ClassDiagramBuilder DependsOn(string from, string to, string? label = null)
        => Relation(from, to, RelationKind.Dependency, label);

    /// <summary>The general form the named relation methods delegate to.</summary>
    public ClassDiagramBuilder Relation(string from, string to, RelationKind kind, string? label, string? fromCard = null, string? toCard = null)
    {
        var pairs = new List<(string, string)>
        {
            ("from", YamlWriter.Scalar(from)),
            ("to", YamlWriter.Scalar(to)),
            ("kind", Tokens.Of(kind)),
        };
        if (label != null) pairs.Add(("label", YamlWriter.Scalar(label)));
        if (fromCard != null) pairs.Add(("fromCard", YamlWriter.Scalar(fromCard)));
        if (toCard != null) pairs.Add(("toCard", YamlWriter.Scalar(toCard)));
        _relations.Add(YamlWriter.FlowMap(pairs));
        _relationEndpoints.Add((from, to));
        return this;
    }

    /// <summary>Script the animation explicitly. Without this each inheritance level lights up in turn.</summary>
    public ClassDiagramBuilder Flow(Action<FlowBuilder> configure)
    {
        _flow ??= new FlowBuilder();
        configure(_flow);
        return this;
    }

    // ---- reflection ----

    /// <summary>
    /// Reflect more types into an existing builder — the instance form of
    /// <see cref="FromTypes"/>. Base type → <see cref="RelationKind.Inherits"/>,
    /// directly implemented interfaces → <see cref="RelationKind.Implements"/>,
    /// property types → <see cref="RelationKind.Association"/> (collections get
    /// a <c>*</c> multiplicity). Types outside the set are ignored, so the
    /// diagram stays scoped to what you pass in.
    /// </summary>
    public ClassDiagramBuilder AddTypes(params Type[] types)
    {
        var set = new Dictionary<Type, string>();
        foreach (var t in types)
        {
            if (set.ContainsKey(t)) continue;
            var id = ReflectionModel.IdFor(t, set.Values);
            set.Add(t, id);
        }

        foreach (var (type, id) in set)
        {
            var c = new ClassBuilder(id).Name(ReflectionModel.FriendlyName(type));
            var stereo = ReflectionModel.Stereotype(type);
            if (stereo != null) c.Stereotype(stereo);
            foreach (var f in ReflectionModel.Fields(type)) c.Field(f);
            foreach (var m in ReflectionModel.Methods(type)) c.Method(m);
            _classes.Add(c);
        }

        foreach (var (type, id) in set)
        {
            if (type.BaseType != null && set.TryGetValue(type.BaseType, out var parentId))
                Inherits(id, parentId);
            foreach (var iface in ReflectionModel.DirectInterfaces(type))
                if (set.TryGetValue(iface, out var ifaceId))
                    Implements(id, ifaceId);
            foreach (var (target, name, many) in ReflectionModel.Associations(type))
                if (set.TryGetValue(target, out var targetId) && target != type)
                    Association(id, targetId, label: name, toCard: many ? "*" : null);
        }
        return this;
    }

    /// <summary>Render the diagram as Beck YAML.</summary>
    /// <exception cref="InvalidOperationException">A relation references an id that is not a declared class.</exception>
    public string ToYaml()
    {
        if (_classes.Count == 0)
            throw new InvalidOperationException("A class diagram needs at least one Class().");
        var ids = new HashSet<string>(_classes.Select(c => c.Id), StringComparer.Ordinal);
        foreach (var g in _groups) ids.Add(g.Id);
        foreach (var (from, to) in _relationEndpoints)
        {
            if (!ids.Contains(from))
                throw new InvalidOperationException($"Relation references unknown class '{from}' — declare it with Class() first.");
            if (!ids.Contains(to))
                throw new InvalidOperationException($"Relation references unknown class '{to}' — declare it with Class() first.");
        }

        var sb = new StringBuilder();
        sb.Append("type: class\n");
        _meta.AppendYaml(sb);
        sb.Append("classes:\n");
        foreach (var c in _classes) sb.Append("  - ").Append(c.ToFlow()).Append('\n');
        if (_groups.Count > 0)
        {
            sb.Append("groups:\n");
            foreach (var g in _groups) sb.Append("  - ").Append(g.ToFlow()).Append('\n');
        }
        if (_relations.Count > 0)
        {
            sb.Append("relations:\n");
            foreach (var r in _relations) sb.Append("  - ").Append(r).Append('\n');
        }
        _flow?.AppendYaml(sb);
        return sb.ToString();
    }

    /// <summary>Render as a fenced <c>```beck</c> Markdown block — drop it into any Markdown page and it renders to a static SVG.</summary>
    public string ToFence() => BeckMarkdown.Fence(ToYaml());

    /// <inheritdoc/>
    public override string ToString() => ToYaml();
}

/// <summary>
/// Configures one UML class card inside a <c>Class(id, c => …)</c> callback.
/// Field and method compartment lines are plain strings, so write them the way
/// your team reads them.
/// </summary>
public sealed class ClassBuilder
{
    private readonly string _id;
    private readonly List<string> _fields = new();
    private readonly List<string> _methods = new();
    private string? _name;
    private string? _stereotype;
    private string? _accent;
    private string? _href;
    private string? _target;
    private string? _group;
    private int? _width;
    private int? _rank;
    private int? _order;

    internal ClassBuilder(string id) => _id = id;

    internal string Id => _id;

    /// <summary>Set the display name (defaults to the id).</summary>
    public ClassBuilder Name(string name) { _name = name; return this; }

    /// <summary>Set the «stereotype» line above the name — <c>interface</c>, <c>abstract</c>, <c>enum</c>, <c>aggregate</c>, anything.</summary>
    public ClassBuilder Stereotype(string stereotype) { _stereotype = stereotype; return this; }

    /// <summary>Set the accent to a semantic token (follows the theme).</summary>
    public ClassBuilder Accent(AccentToken token) { _accent = Tokens.Of(token); return this; }

    /// <summary>Set the accent to a raw CSS color.</summary>
    public ClassBuilder Accent(string color) { _accent = color; return this; }

    /// <summary>Add one line to the fields compartment (e.g. <c>"Id: Guid"</c>).</summary>
    public ClassBuilder Field(string field) { _fields.Add(field); return this; }

    /// <summary>Add several field lines at once.</summary>
    public ClassBuilder Fields(params string[] fields) { _fields.AddRange(fields); return this; }

    /// <summary>Add one line to the methods compartment (e.g. <c>"Submit()"</c>).</summary>
    public ClassBuilder Method(string method) { _methods.Add(method); return this; }

    /// <summary>Add several method lines at once.</summary>
    public ClassBuilder Methods(params string[] methods) { _methods.AddRange(methods); return this; }

    /// <summary>Make the card a link — to the type's source or docs, say.</summary>
    public ClassBuilder Link(string href, string? target = null) { _href = href; _target = target; return this; }

    /// <summary>Assign the class to a namespace group inline.</summary>
    public ClassBuilder Group(string groupId) { _group = groupId; return this; }

    /// <summary>Fix the card width in pixels.</summary>
    public ClassBuilder Width(int px) { _width = px; return this; }

    /// <summary>Force the class into a specific layout rank.</summary>
    public ClassBuilder Rank(int rank) { _rank = rank; return this; }

    /// <summary>Tie-break order within the rank.</summary>
    public ClassBuilder Order(int order) { _order = order; return this; }

    internal string ToFlow()
    {
        var pairs = new List<(string, string)> { ("id", YamlWriter.Scalar(_id)) };
        if (_name != null) pairs.Add(("name", YamlWriter.Scalar(_name)));
        if (_stereotype != null) pairs.Add(("stereotype", YamlWriter.Scalar(_stereotype)));
        if (_accent != null) pairs.Add(("accent", YamlWriter.Scalar(_accent)));
        if (_fields.Count > 0) pairs.Add(("fields", YamlWriter.FlowSeq(_fields.Select(YamlWriter.Scalar))));
        if (_methods.Count > 0) pairs.Add(("methods", YamlWriter.FlowSeq(_methods.Select(YamlWriter.Scalar))));
        if (_href != null) pairs.Add(("href", YamlWriter.Scalar(_href)));
        if (_target != null) pairs.Add(("target", YamlWriter.Scalar(_target)));
        if (_group != null) pairs.Add(("group", YamlWriter.Scalar(_group)));
        if (_width is { } w) pairs.Add(("width", w.ToString(CultureInfo.InvariantCulture)));
        if (_rank is { } r) pairs.Add(("rank", r.ToString(CultureInfo.InvariantCulture)));
        if (_order is { } o) pairs.Add(("order", o.ToString(CultureInfo.InvariantCulture)));
        return YamlWriter.FlowMap(pairs);
    }
}

/// <summary>Reflection → class-card model helpers for <see cref="ClassDiagramBuilder.AddTypes"/>.</summary>
internal static class ReflectionModel
{
    private const int MaxEnumMembers = 8;

    public static string IdFor(Type t, IEnumerable<string> taken)
    {
        var baseName = BareName(t);
        var id = char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);
        var existing = new HashSet<string>(taken, StringComparer.Ordinal);
        if (!existing.Contains(id)) return id;
        var n = 2;
        while (existing.Contains(id + n.ToString(CultureInfo.InvariantCulture))) n++;
        return id + n.ToString(CultureInfo.InvariantCulture);
    }

    public static string? Stereotype(Type t)
    {
        if (t.IsInterface) return "interface";
        if (t.IsEnum) return "enum";
        if (IsRecord(t)) return t.IsValueType ? "record struct" : "record";
        if (t.IsValueType) return "struct";
        if (t.IsAbstract) return "abstract";
        return null;
    }

    public static IEnumerable<string> Fields(Type t)
    {
        if (t.IsEnum)
        {
            var names = Enum.GetNames(t);
            foreach (var n in names.Take(MaxEnumMembers)) yield return n;
            if (names.Length > MaxEnumMembers) yield return $"… {names.Length - MaxEnumMembers} more";
            yield break;
        }
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var p in t.GetProperties(flags))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            if (IsRecord(t) && p.Name == "EqualityContract") continue;
            yield return $"{p.Name}: {FriendlyName(p.PropertyType)}";
        }
        foreach (var f in t.GetFields(flags))
            yield return $"{f.Name}: {FriendlyName(f.FieldType)}";
    }

    private static readonly HashSet<string> SkippedMethods = new(StringComparer.Ordinal)
    {
        "Equals", "GetHashCode", "ToString", "GetType", "Deconstruct", "PrintMembers", "CompareTo",
    };

    public static IEnumerable<string> Methods(Type t)
    {
        if (t.IsEnum) yield break;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var m in t.GetMethods(flags))
        {
            if (m.IsSpecialName || SkippedMethods.Contains(m.Name)) continue;
            if (m.Name.StartsWith("<", StringComparison.Ordinal)) continue; // compiler-generated (e.g. record Clone)
            var args = string.Join(", ", m.GetParameters().Select(p => p.Name));
            yield return $"{m.Name}({args})";
        }
    }

    /// <summary>Interfaces implemented directly by <paramref name="t"/> (not via base type or another interface).</summary>
    public static IEnumerable<Type> DirectInterfaces(Type t)
    {
        var all = t.GetInterfaces();
        var inherited = new HashSet<Type>(t.BaseType?.GetInterfaces() ?? Type.EmptyTypes);
        foreach (var i in all)
            foreach (var ii in i.GetInterfaces())
                inherited.Add(ii);
        return all.Where((i) => !inherited.Contains(i));
    }

    /// <summary>Property-type references: (target type, property name, is-collection).</summary>
    public static IEnumerable<(Type Target, string Name, bool Many)> Associations(Type t)
    {
        if (t.IsEnum) yield break;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var p in t.GetProperties(flags))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            var pt = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            var element = ElementType(pt);
            if (element != null) yield return (element, p.Name, true);
            else yield return (pt, p.Name, false);
        }
    }

    /// <summary>The element type of a collection-ish type, or null for scalars (string is a scalar).</summary>
    private static Type? ElementType(Type t)
    {
        if (t == typeof(string)) return null;
        if (t.IsArray) return t.GetElementType();
        if (!typeof(IEnumerable).IsAssignableFrom(t)) return null;
        if (t.IsGenericType && t.GetGenericArguments().Length == 1) return t.GetGenericArguments()[0];
        var ienum = t.GetInterfaces().FirstOrDefault((i) => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return ienum?.GetGenericArguments()[0];
    }

    private static bool IsRecord(Type t) => t.GetMethod("<Clone>$") != null;

    private static string BareName(Type t)
    {
        var name = t.Name;
        var tick = name.IndexOf('`');
        return tick >= 0 ? name.Substring(0, tick) : name;
    }

    private static readonly Dictionary<Type, string> Primitives = new()
    {
        [typeof(void)] = "void",
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(object)] = "object",
        [typeof(short)] = "short",
        [typeof(string)] = "string",
        [typeof(uint)] = "uint",
        [typeof(ulong)] = "ulong",
    };

    /// <summary>C#-style type name: <c>List&lt;OrderLine&gt;</c>, <c>int?</c>, <c>Money</c>.</summary>
    public static string FriendlyName(Type t)
    {
        if (Primitives.TryGetValue(t, out var prim)) return prim;
        var nullable = Nullable.GetUnderlyingType(t);
        if (nullable != null) return FriendlyName(nullable) + "?";
        if (t.IsArray) return FriendlyName(t.GetElementType()!) + "[]";
        if (t.IsGenericType)
        {
            var args = string.Join(", ", t.GetGenericArguments().Select(FriendlyName));
            return $"{BareName(t)}<{args}>";
        }
        return BareName(t);
    }
}
