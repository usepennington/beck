namespace Beck.Rendering;

// The normalized diagram model — a faithful port of src/model/schema.ts. Authoring
// YAML is parsed and validated into these shapes with every default filled in, so
// downstream stages (layout, route, render, animate) never see optional/raw input.
// Types are `internal`: consumers touch only BeckSvg + SvgRenderOptions.

internal sealed record Spacing(double Rank, double Node, double CornerRadius);

/// <summary>Narration caption behaviour + reading-time pacing.</summary>
internal sealed record NarrationOptions(bool Enabled, double Wpm, double Min, double Pad);

internal sealed record DiagramMeta
{
    public required DiagramType Type { get; init; }
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    /// <summary>The raw <c>meta.style</c> token (<c>[a-z0-9-]+</c>), or null when unset/malformed.
    /// Resolved to a <c>BeckStyle</c> in <c>BeckSvg</c> (resolution needs the render options the
    /// model never sees), mirroring how <see cref="Title"/> stays a plain string here.</summary>
    public string? StyleName { get; init; }
    public required Direction Direction { get; init; }
    public required ThemeMode Theme { get; init; }
    /// <summary>Mutable: the class builder forces this false when no flow is authored.</summary>
    public required bool Animate { get; set; }
    public required bool Loop { get; init; }
    public required FitMode Fit { get; init; }
    public required Spacing Spacing { get; init; }
    public required NarrationOptions Narration { get; init; }
}

internal sealed record NodeModel
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    /// <summary>Named icon key OR raw inline <c>&lt;svg&gt;</c> markup. Resolved at render time.</summary>
    public string? Icon { get; init; }
    public required NodeKind Kind { get; init; }
    public required NodeVariant Variant { get; init; }
    public string? Status { get; init; }
    /// <summary>CSS color value, e.g. <c>var(--beck-primary)</c> or a raw hex.</summary>
    public required string Accent { get; init; }
    public string? Href { get; init; }
    public string? Target { get; init; }
    public string? Surface { get; init; }
    public string? TextColor { get; init; }
    public double? Width { get; init; }
    public double? Rank { get; init; }
    public double? Order { get; init; }
    public string? Group { get; init; }
    public required NodeShape Shape { get; init; }
    public string? Stereotype { get; init; }
    public required IReadOnlyList<string> Fields { get; init; }
    public required IReadOnlyList<string> Methods { get; init; }
}

internal sealed record GroupModel
{
    public required string Id { get; init; }
    public required string Label { get; set; }
    /// <summary>Node or nested-group ids; populated during group building.</summary>
    public required List<string> Members { get; init; }
    public required string Accent { get; init; }
}

internal sealed record EdgeModel
{
    public required string Id { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public string? Label { get; init; }
    public required EdgeStyle Style { get; init; }
    public required EdgeCurve Curve { get; init; }
    public required EdgeKind Kind { get; init; }
    /// <summary>CSS color value for the stroke.</summary>
    public required string Color { get; init; }
    public required ArrowEnds Arrow { get; init; }
    public string? Note { get; init; }
    public Side? FromSide { get; init; }
    public Side? ToSide { get; init; }
    public MarkerShape? MarkerStart { get; init; }
    public MarkerShape? MarkerEnd { get; init; }
    public string? FromLabel { get; init; }
    public string? ToLabel { get; init; }
    /// <summary>Sequence only: a dashed return message.</summary>
    public required bool Reply { get; init; }
    /// <summary>Sequence only: force/suppress an activation bar; null lets the heuristic decide.</summary>
    public bool? Activate { get; init; }
}

/// <summary>Shared <c>packet</c>/<c>burst</c> motion knobs; unset ones fall back to edge-kind defaults.</summary>
internal sealed record PacketKnobs
{
    public PacketShape? Shape { get; init; }
    public double? Size { get; init; }
    public double? Speed { get; init; }
    public bool? Glow { get; init; }
    public bool? Impact { get; init; }
    public PacketEase? Ease { get; init; }
}

/// <summary>A single flow step. Discriminated subtypes mirror the TS <c>FlowStep</c> union.</summary>
internal abstract record FlowStep
{
    /// <summary>The wire discriminator (<c>packet</c>, <c>burst</c>, …).</summary>
    public abstract string Kind { get; }
}

internal sealed record PacketStep : FlowStep
{
    public override string Kind => "packet";
    public required string From { get; init; }
    public required string To { get; init; }
    public IReadOnlyList<string>? Via { get; init; }
    public string? Edge { get; init; }
    public string? Color { get; init; }
    public string? Label { get; init; }
    public required PacketKnobs Knobs { get; init; }
}

internal sealed record BurstStep : FlowStep
{
    public override string Kind => "burst";
    public required string From { get; init; }
    /// <summary>Single target (mutually exclusive with <see cref="ToList"/>).</summary>
    public string? To { get; init; }
    /// <summary>Fan-out targets when authored as a list.</summary>
    public IReadOnlyList<string>? ToList { get; init; }
    public IReadOnlyList<string>? Via { get; init; }
    public required int Count { get; init; }
    public required double Stagger { get; init; }
    public string? Color { get; init; }
    public string? Label { get; init; }
    public required PacketKnobs Knobs { get; init; }

    /// <summary>The targets as a flat list, however they were authored.</summary>
    public IEnumerable<string> Targets => ToList ?? (To is null ? [] : [To]);
}

internal sealed record StatusStep : FlowStep
{
    public override string Kind => "status";
    public required string Node { get; init; }
    public required string Text { get; init; }
    public string? Color { get; init; }
}

internal sealed record HighlightStep : FlowStep
{
    public override string Kind => "highlight";
    public required string Node { get; init; }
    public string? Color { get; init; }
}

internal sealed record PulseStep : FlowStep
{
    public override string Kind => "pulse";
    public required string Node { get; init; }
    public string? Color { get; init; }
}

internal sealed record ActivateStep : FlowStep
{
    public override string Kind => "activate";
    public required string From { get; init; }
    public required string To { get; init; }
    public string? Color { get; init; }
}

internal sealed record StreamStep : FlowStep
{
    public override string Kind => "stream";
    public required string From { get; init; }
    public required string To { get; init; }
    public string? Color { get; init; }
}

internal sealed record WorkingStep : FlowStep
{
    public override string Kind => "working";
    public required string Node { get; init; }
    public string? Color { get; init; }
}

internal sealed record IdleStep : FlowStep
{
    public override string Kind => "idle";
    public required string Node { get; init; }
}

internal sealed record FailStep : FlowStep
{
    public override string Kind => "fail";
    public required string Node { get; init; }
    public string? Text { get; init; }
    public string? Color { get; init; }
}

internal sealed record NarrateStep : FlowStep
{
    public override string Kind => "narrate";
    public required string Text { get; init; }
    public double? Hold { get; init; }
    public string? Color { get; init; }
}

internal sealed record PhaseStep : FlowStep
{
    public override string Kind => "phase";
    public required string Label { get; init; }
}

internal sealed record WaitStep : FlowStep
{
    public override string Kind => "wait";
    public required double Seconds { get; init; }
}

internal sealed record ResetStep : FlowStep
{
    public override string Kind => "reset";
}

internal sealed record ParallelStep : FlowStep
{
    public override string Kind => "parallel";
    public required IReadOnlyList<FlowStep> Steps { get; init; }
}

internal sealed record FlowModel
{
    /// <summary>-1 infinite / 0 once / N. Mutable: <c>meta.loop:false</c> forces 0.</summary>
    public required double Repeat { get; set; }
    public required double RepeatDelay { get; init; }
    public required IReadOnlyList<FlowStep> Steps { get; init; }
    /// <summary>True when the flow was auto-derived from the edges.</summary>
    public required bool Derived { get; init; }
}

/// <summary>A labelled horizontal band in a sequence diagram, drawn before message <c>At</c>.</summary>
internal sealed record SectionMark(string Label, int At, string Accent);

internal sealed record DiagramModel
{
    public required DiagramMeta Meta { get; init; }
    public required IReadOnlyList<NodeModel> Nodes { get; init; }
    public required IReadOnlyList<GroupModel> Groups { get; init; }
    public required IReadOnlyList<EdgeModel> Edges { get; init; }
    public required FlowModel Flow { get; init; }
    public required IReadOnlyList<SectionMark> Sections { get; init; }
}
