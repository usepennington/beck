namespace Beck.Docs;

/// <summary>
/// Brand CSS fed to MonorailCSS in two forms, split by what each form is actually good at.
/// <list type="bullet">
///   <item><see cref="Applies"/> — component classes expressed as <b>utility compositions</b>
///     (MonorailCSS's <c>CssFrameworkSettings.Applies</c>: selector → space-separated utilities,
///     expanded into <c>@layer components</c> at build). This is where the repetitive box-model +
///     colour + <c>.dark</c>/<c>:hover</c> twin CSS lived; folding the variants into one
///     <c>dark:</c>/<c>hover:</c> value each is the whole point. <c>Applies</c> emits by explicit
///     selector, so a class being referenced only from a JS string (or from server Razor) is
///     irrelevant — no IL/utility discovery is involved.</item>
///   <item><see cref="ExtraStyles"/> — the residue that genuinely isn't utility-shaped: at-rules
///     (<c>@keyframes</c>, <c>@starting-style</c>, <c>@media</c>), pseudo-elements
///     (<c>::backdrop</c>, <c>::-webkit-scrollbar</c>), custom-property token assignments (the
///     <c>--beck-*</c> accent remap and the named colour schemes), and gnarly literals a utility
///     wouldn't clarify (the dot-grid radial-gradient, the <c>[data-type]</c>/<c>[data-scheme]</c>
///     colour tables, <c>color-mix</c> scrims).</item>
/// </list>
/// Both resolve from the palette variables MonorailCSS emits for the mapped primary/accent/base
/// slots (and flip under <c>.dark</c>), so this CSS themes itself.
///
/// <para><b>Cascade note.</b> <see cref="ExtraStyles"/> is appended <i>unlayered</i> (it wins over
/// every <c>@layer</c>), whereas <see cref="Applies"/> lands in <c>@layer components</c> (which
/// utilities beat). That only matters if an element carried both a class below and a utility
/// touching the same property — none do: every consumer (playground dropdowns, syntax chips, API
/// nav, icon cards) uses these classes in isolation, at most with a state class like
/// <c>is-active</c>.</para>
/// </summary>
internal static class BrandStyling
{
    /// <summary>
    /// Component classes as utility compositions, merged into <c>CssFrameworkSettings.Applies</c>.
    /// Keys are full CSS selectors (compound/descendant/sibling/attribute selectors all emit as
    /// written); values are MonorailCSS utility strings with <c>dark:</c>/<c>hover:</c> variants
    /// folded in. <c>font: inherit</c> is dropped throughout — preflight already resets it on
    /// <c>button</c>/<c>input</c>/<c>select</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Applies = new Dictionary<string, string>
    {
        // ---- hand-authored code-sample syntax tokens (hero + API pages) ----
        [".tok-key"]    = "text-primary-700 dark:text-primary-400",
        [".tok-accent"] = "text-primary-700 dark:text-primary-400",
        [".tok-punct"]  = "text-base-400 dark:text-base-500",
        [".tok-str"]    = "text-[#b45309] dark:text-[#e0b341]",

        // ---- lazy style-gallery slots ----
        [".beck-lazy"]           = "grid place-items-center min-h-[680px] border border-dashed border-base-200 rounded-xl mb-6 dark:border-base-800",
        [".beck-lazy > a"]       = "text-[0.85rem] text-base-500",
        [".beck-lazy.is-loaded"] = "block min-h-0 border-0 m-0",

        // NB: the playground toolbar (the .pg-dd* dropdowns, .pg-pill, .pg-swatch) is styled with
        // utilities directly on the elements in Beck.Docs.Client/PlaygroundIsland.razor — not here.
        // Only the adjacent-sibling separators and the per-type/scheme colour tables remain as
        // declarative rules in ExtraStyles (keyed off the .pg-dd-group/.pg-pill/.pg-swatch hooks).

        // ---- syntax cheatsheet: filter chips ----
        [".syntax-chip"]           = "font-medium text-[13px] px-[14px] py-[6px] rounded-full cursor-pointer bg-white text-base-600 border border-base-300 transition-[background-color,color,border-color] duration-150 hover:border-base-400 hover:text-base-900 dark:bg-base-900 dark:text-base-400 dark:border-base-700 dark:hover:border-base-600 dark:hover:text-base-50",
        [".syntax-chip.is-active"] = "bg-primary-600 border-primary-600 text-white dark:bg-primary-500 dark:border-primary-500",

        // ---- API reference sidebar links (scroll-spy state toggled by site.js) ----
        [".api-nav-link"]           = "block w-full text-left font-mono text-[13px] px-[10px] py-[6px] rounded-md no-underline text-base-600 transition-colors duration-150 hover:bg-base-50 hover:text-base-900 dark:text-base-400 dark:hover:bg-base-900 dark:hover:text-base-50",
        [".api-nav-link.is-active"] = "bg-primary-50 text-primary-700 dark:bg-[color-mix(in_srgb,var(--color-primary-950)_40%,transparent)] dark:text-primary-400",

        // ---- icon reference gallery (server-rendered by <IconGallery />) ----
        ["[data-beck-icon-gallery]"]  = "grid grid-cols-[repeat(auto-fill,minmax(128px,1fr))] gap-3",
        [".beck-icon-card"]           = "flex flex-col items-center gap-2 px-[10px] py-4 text-center border border-base-200 rounded-xl bg-white dark:border-base-800 dark:bg-base-900",
        // Distinct from the SVG-internal `.beck-icon-chip` <rect> the engine emits.
        [".beck-icon-card-chip"]      = "flex items-center justify-center w-10 h-10 rounded-[10px] bg-[color-mix(in_srgb,var(--color-primary-600)_12%,var(--color-base-100))] text-primary-700 dark:bg-[color-mix(in_srgb,var(--color-primary-500)_18%,var(--color-base-800))] dark:text-primary-400",
        [".beck-icon-card-chip svg"]  = "w-[22px] h-[22px]",
        [".beck-icon-key"]            = "font-mono text-[12.5px] text-base-900 dark:text-base-50",
        [".beck-icon-aliases"]        = "font-mono text-[10.5px] leading-[1.4] text-base-400 break-words dark:text-base-500",
    };

    // lang=css
    public const string ExtraStyles = """
        /* ---- scroll offset for the sticky nav (box-sizing + margin reset come from preflight) ---- */
        html { scroll-padding-top: 5rem; }

        /* ---- fonts: IBM Plex is part of the brand identity ---- */
        :root {
          --font-sans: 'IBM Plex Sans', system-ui, sans-serif;
          --font-mono: 'IBM Plex Mono', ui-monospace, SFMono-Regular, monospace;
        }
        body { font-family: var(--font-sans); }

        /* ---- signature dot-grid surface (apply to our own diagram frames) ---- */
        /* Dots only — no solid fill, so the panel sits transparently on the page surface
           instead of reading as a tinted card. */
        .dot-grid {
          background-image: radial-gradient(var(--color-base-200) 1px, transparent 1px);
          background-size: 16px 16px;
        }
        .dark .dot-grid {
          background-image: radial-gradient(var(--color-base-800) 1px, transparent 1px);
        }

        /* ---- Beck embeds ---- */
        /* A rendered ```beck fence becomes <div class="beck-embed"><svg …>; frame it on the
           dot-grid surface so every live diagram reads as a framed preview. */
        .beck-embed {
          position: relative;
          border: 1px solid var(--color-base-200);
          border-radius: 12px;
          background-color: var(--color-base-50);
          background-image: radial-gradient(var(--color-base-200) 1px, transparent 1px);
          background-size: 16px 16px;
          padding: 26px 18px;
          margin: 0 0 24px;
          display: flex;
          /* Center via the child's auto margins (see below), NOT align-items:center —
             a diagram taller than a height-constrained embed would be centre-clipped
             with its top (the title) pushed above the unreachable scroll origin. */
          min-height: 200px;
          max-width: 100%;
          overflow: auto;
        }
        .dark .beck-embed {
          border-color: var(--color-base-800);
          background-color: var(--color-base-900);
          background-image: radial-gradient(var(--color-base-800) 1px, transparent 1px);
        }
        /* The C# engine emits the <svg> directly into the embed; auto margins center it but
           collapse to the top edge when it's taller than a constrained frame, so the title
           stays scrollable. */
        .beck-embed > .beck-svg { max-width: 100%; height: auto; margin: auto; }
        .beck-embed--error { border-color: var(--color-red-400, #f87171); }
        .beck-embed--error pre { margin: 0; width: 100%; overflow: auto; font-family: var(--font-mono); font-size: 12px; }
        /* Bare: drop the fence frame so a diagram sits directly on its host surface (the
           homepage hero renders onto its own dot-grid card). The `.dark` variant must be
           repeated — `.dark .beck-embed` above outranks a lone `.beck-embed--bare`, which
           would repaint a solid panel over the host's dots in dark mode. */
        .beck-embed--bare, .dark .beck-embed--bare { border: 0; background: none; padding: 0; margin: 0; min-height: 0; }
        /* The playground renders into its own canvas — strip the fence frame there. */
        .pg-preview-canvas .beck-embed {
          border: 0; background: none; padding: 0; margin: 0; min-height: 0;
        }

        /* ---- diagram fullscreen zoom ---- */
        /* BeckSvgPreprocessor emits the .beck-zoom button into each embed; site.js opens the
           .beck-lightbox <dialog>. The button fades in on embed hover (always visible on touch,
           where there is no hover). Kept declarative for the color-mix scrim + hover chain. */
        .beck-zoom {
          position: absolute; right: 10px; bottom: 10px;
          display: flex; align-items: center; justify-content: center;
          width: 30px; height: 30px; padding: 0; cursor: pointer;
          border: 1px solid var(--color-base-300); border-radius: 8px;
          background: color-mix(in srgb, var(--color-base-50) 88%, transparent);
          color: var(--color-base-500);
          opacity: 0; transition: opacity .15s, color .15s, border-color .15s;
        }
        .beck-embed:hover .beck-zoom, .beck-zoom:focus-visible { opacity: 1; }
        .beck-zoom:hover { color: var(--color-base-900); border-color: var(--color-base-400); }
        .dark .beck-zoom {
          border-color: var(--color-base-700);
          background: color-mix(in srgb, var(--color-base-900) 88%, transparent);
          color: var(--color-base-400);
        }
        .dark .beck-zoom:hover { color: var(--color-base-50); border-color: var(--color-base-600); }
        @media (hover: none) { .beck-zoom { opacity: 1; } }

        /* The lightbox: a transparent full-viewport <dialog> whose ::backdrop dims and blurs
           the page. site.js builds the dialog (its box is utility classes there) and keys it
           `.beck-lightbox` purely as the hook for what utilities can't reach — the ::backdrop,
           its @starting-style entrance, and the cloned SVG's sizing below. The cloned SVG
           renders at its natural size (its width/height attributes), only scaling DOWN when it
           exceeds the viewport — never up, which made small diagrams look comically large. */
        /* The page behind goes blurred, washed out, and sunk into the app surface, so the
           diagram is the only saturated thing on screen. The scrim can't say
           `var(--beck-surface)`: that token is declared on the per-diagram `.b-{hash}` scope
           inside each SVG, so it doesn't resolve out here on a <dialog> in <body>. Instead
           this mirrors the token's own definition (base-50 / base-950), which is what makes
           the scrim read as the page receding rather than a black wash.

           `@starting-style` gives the ::backdrop — which only exists while the dialog is
           open, and so is always newly rendered — a from-state to transition out of; site.js
           removes the dialog on close, so this is an entrance-only cue. */
        .beck-lightbox::backdrop {
          background-color: color-mix(in srgb, var(--color-base-50, #ffffff) 55%, transparent);
          backdrop-filter: blur(12px) saturate(0.1);
          -webkit-backdrop-filter: blur(12px) saturate(0.1);
          transition: background-color .18s ease-out, backdrop-filter .18s ease-out,
                      -webkit-backdrop-filter .18s ease-out;
        }
        .dark .beck-lightbox::backdrop {
          background-color: color-mix(in srgb, var(--color-base-950, #0d1117) 60%, transparent);
        }
        /* One from-state serves both themes — fully transparent either way. */
        @starting-style {
          .beck-lightbox[open]::backdrop {
            background-color: transparent;
            backdrop-filter: blur(0px) saturate(1);
            -webkit-backdrop-filter: blur(0px) saturate(1);
          }
        }
        @media (prefers-reduced-motion: reduce) {
          .beck-lightbox::backdrop { transition: none; }
        }
        .beck-lightbox .beck-svg { width: auto; height: auto; max-width: 94vw; max-height: 90vh; }
        /* (The lightbox close button is fully utility-styled in site.js — no rule here.) */

        /* ---- Beck accent remap: differentiate from the emerald brand ---- */
        /* The engine maps `success`->emerald and `info`->violet. On this emerald-branded
           site `success` would collide with `primary` (both emerald), and the violet ramp is
           never emitted (MonorailCSS only emits ramps a utility class references), so `info`
           can't adopt the live palette. Re-point both at ramps this site DOES emit: `success`
           -> green (a distinct green from the emerald primary) and `info` -> sky (also recolours
           the `db` kind, whose default accent is info). The `body` prefix lifts specificity
           above the engine's own `.beck-root` defaults, which it injects into <head> AFTER this
           stylesheet (equal-specificity rules would otherwise let the engine default win). */
        body .beck-root {
          --beck-success: var(--color-green-500, #22c55e);
          --beck-info: var(--color-sky-500, #0ea5e9);
        }

        /* ---- playground: elements created / class-toggled by site.js ---- */
        /* Auto margins center the rendered diagram in the preview pane but collapse
           to the top when it's taller than the pane, so the title stays scrollable
           (align-items:center would center-clip it above an unreachable scroll origin). */
        #pg-host { margin: auto; }
        #pg-editor-host { flex: 1 1 0%; min-height: 0; width: 100%; overflow: hidden; background: var(--color-base-50); }
        .dark #pg-editor-host { background: var(--color-base-900); }
        #pg-editor-host .monaco-editor, #pg-editor-host .monaco-editor .overflow-guard { border-radius: 0; }
        #pg-editor-host .monaco-editor { height: 100%; }
        #pg-status.ok  { color: var(--color-primary-600); }
        #pg-status.err { color: #e6685b; }

        /* ---- playground dropdown: adjacent-sibling separators ---- */
        /* The dropdown boxes are utilities on the elements (PlaygroundIsland.razor); these are
           the sibling-combinator bits utilities can't express — the divider between example
           groups and the 2px gap between options. `.pg-dd-group`/`.pg-dd-item` survive as the
           hooks (also used by beck-editor.js). */
        .pg-dd-group { padding: 2px 0; }
        .pg-dd-group + .pg-dd-group { margin-top: 6px; border-top: 1px solid var(--color-base-100); padding-top: 8px; }
        .dark .pg-dd-group + .pg-dd-group { border-top-color: var(--color-base-800); }
        .pg-dd-item + .pg-dd-item { margin-top: 2px; }

        /* ---- playground type pill: colour-coded per diagram type ---- */
        /* The `.pg-pill` base box is utilities in PlaygroundIsland.razor; these are the per-type
           colour tables — literal fallbacks so a pill renders even for ramps MonorailCSS doesn't
           emit (violet/amber aren't always used). */
        .pg-pill[data-type="architecture"] { color: var(--color-emerald-700, #047857); background: color-mix(in srgb, var(--color-emerald-500, #10b981) 14%, transparent); border-color: color-mix(in srgb, var(--color-emerald-500, #10b981) 30%, transparent); }
        .pg-pill[data-type="sequence"]     { color: var(--color-sky-700, #0369a1);     background: color-mix(in srgb, var(--color-sky-500, #0ea5e9) 14%, transparent);     border-color: color-mix(in srgb, var(--color-sky-500, #0ea5e9) 30%, transparent); }
        .pg-pill[data-type="state"]        { color: #b45309;                            background: color-mix(in srgb, #f59e0b 16%, transparent);                            border-color: color-mix(in srgb, #f59e0b 32%, transparent); }
        .pg-pill[data-type="class"]        { color: #6d28d9;                            background: color-mix(in srgb, #8b5cf6 16%, transparent);                            border-color: color-mix(in srgb, #8b5cf6 32%, transparent); }
        .pg-pill[data-type="flowchart"]    { color: #0f766e;                            background: color-mix(in srgb, #14b8a6 15%, transparent);                            border-color: color-mix(in srgb, #14b8a6 30%, transparent); }
        .pg-pill[data-type="mindmap"]      { color: #be123c;                            background: color-mix(in srgb, #f43f5e 14%, transparent);                            border-color: color-mix(in srgb, #f43f5e 30%, transparent); }
        .pg-pill[data-type="chart"]        { color: #0e7490;                            background: color-mix(in srgb, #06b6d4 15%, transparent);                            border-color: color-mix(in srgb, #06b6d4 30%, transparent); }
        .dark .pg-pill[data-type="architecture"] { color: var(--color-emerald-300, #6ee7b7); }
        .dark .pg-pill[data-type="sequence"]     { color: var(--color-sky-300, #7dd3fc); }
        .dark .pg-pill[data-type="state"]        { color: #fcd34d; }
        .dark .pg-pill[data-type="class"]        { color: #c4b5fd; }
        .dark .pg-pill[data-type="flowchart"]    { color: #5eead4; }
        .dark .pg-pill[data-type="mindmap"]      { color: #fda4af; }
        .dark .pg-pill[data-type="chart"]        { color: #67e8f9; }

        /* ---- playground colour-scheme swatch: per-scheme two-tone chips ---- */
        /* The `.pg-swatch` base box is utilities in PlaygroundIsland.razor; these paint each
           palette's signature. */
        .pg-swatch[data-scheme="default"]  { background: linear-gradient(135deg, var(--color-primary-400, #34d399), var(--color-primary-700, #047857)); }
        .pg-swatch[data-scheme="monokai"]  { background: linear-gradient(135deg, #f92672 0 50%, #a6e22e 50% 100%); }
        .pg-swatch[data-scheme="one-dark"] { background: linear-gradient(135deg, #61afef 0 50%, #c678dd 50% 100%); }
        .pg-swatch[data-scheme="hot-dog"]  { background: linear-gradient(135deg, #ff1e1e 0 50%, #ffe500 50% 100%); }

        /* ---- playground colour schemes: override the diagram's --beck-* tokens ----
           Keyed off #pg-host[data-scheme] so the id (1,x,0) outranks the engine's own
           token defaults, making each named scheme a fixed palette regardless of the
           site's light/dark toggle. The C# renderer sets its --beck-* tokens directly on
           the `<svg class="beck-svg">` element, so the override must target that same
           element (an ancestor would lose to a directly-set custom property) — hence
           `.beck-svg`, not the JS engine's `.beck-root` wrapper. The svg background is
           transparent (the title sits on the page), so each scheme also paints its own
           surface as a self-framed card — otherwise a dark palette's light title would
           land on the light preview pane. */
        #pg-host[data-scheme="monokai"] .beck-svg,
        #pg-host[data-scheme="one-dark"] .beck-svg,
        #pg-host[data-scheme="hot-dog"] .beck-svg {
          background: var(--beck-surface); border-radius: 14px; padding: 20px 24px;
          border: 1px solid var(--beck-node-border);
        }
        #pg-host[data-scheme="monokai"] .beck-svg {
          --beck-surface: #272822; --beck-node-bg: #31322b; --beck-node-border: #49483e;
          --beck-node-shadow: 0 1px 3px rgb(0 0 0 / .4), 0 6px 16px rgb(0 0 0 / .45);
          --beck-text: #f8f8f2; --beck-text-muted: #c9c5b0; --beck-text-faint: #90897a;
          --beck-edge: #75715e; --beck-icon-bg: #3e3d32;
          --beck-primary: #66d9ef; --beck-success: #a6e22e; --beck-warn: #fd971f;
          --beck-danger: #f92672; --beck-info: #ae81ff; --beck-neutral: #75715e;
        }
        #pg-host[data-scheme="one-dark"] .beck-svg {
          --beck-surface: #282c34; --beck-node-bg: #2f343e; --beck-node-border: #3e4451;
          --beck-node-shadow: 0 1px 3px rgb(0 0 0 / .4), 0 6px 16px rgb(0 0 0 / .45);
          --beck-text: #dfe3ea; --beck-text-muted: #9aa2b1; --beck-text-faint: #6b7280;
          --beck-edge: #4b5263; --beck-icon-bg: #333842;
          --beck-primary: #61afef; --beck-success: #98c379; --beck-warn: #e5c07b;
          --beck-danger: #e06c75; --beck-info: #c678dd; --beck-neutral: #5c6370;
        }
        /* Hot Dog Stand: the loud yellow/red/black classic. Everything sits on yellow
           with near-black text (readable on the pale-yellow node cards too); red is the
           accent (node borders, edges, groups). */
        #pg-host[data-scheme="hot-dog"] .beck-svg {
          --beck-surface: #ffd400; --beck-node-bg: #fff0a3; --beck-node-border: #d40000;
          --beck-node-shadow: 0 2px 0 rgb(180 0 0 / .55), 0 5px 14px rgb(0 0 0 / .2);
          --beck-text: #241a00; --beck-text-muted: #a30000; --beck-text-faint: #b8500a;
          --beck-edge: #d40000; --beck-icon-bg: #ffe680;
          --beck-primary: #e00000; --beck-success: #0a8a0a; --beck-warn: #ff8c00;
          --beck-danger: #b00000; --beck-info: #0026cc; --beck-neutral: #d40000;
        }

        /* ---- hero: site.js writes the YAML while the diagram renders in step ---- */
        /* #hero-code is the typewriter target (Tailwind classes do the framing); the
           caret site.js slots in needs colouring + a blink, and the host a clean
           overflow. Re-rendering the caret each keystroke restarts the animation, so it
           sits solid while typing and only blinks during the pauses between lines. */
        #hero-code { overflow: hidden; }
        #hero-code .beck-caret { color: var(--color-primary-600); font-weight: 400; animation: beck-caret-blink 1s steps(1) infinite; }
        .dark #hero-code .beck-caret { color: var(--color-primary-400); }
        @keyframes beck-caret-blink { 50% { opacity: 0; } }
        #hero-host .beck-root { max-width: 100%; }

        /* ---- syntax cheatsheet: live-diagram canvas ---- */
        /* Each card renders its diagram server-side via <BeckDiagram> into a .beck-embed.
           Strip the fence's framed-preview chrome so the diagram sits directly on the
           card's own dot-grid cell (no nested frame). */
        .syntax-canvas .beck-embed {
          border: 0; background: none; padding: 0; margin: 0; min-height: 0; width: 100%;
        }

        /* ---- docs sidebar: mobile disclosure chevron ---- */
        /* site.js toggles the nav's `hidden` and the button's aria-expanded; the chevron
           flips off that attribute. */
        .docs-nav-chevron { transition: transform .2s ease; }
        .docs-nav-toggle[aria-expanded="true"] .docs-nav-chevron { transform: rotate(180deg); }

        /* ---- global scrollbar polish ---- */
        ::-webkit-scrollbar { width: 11px; height: 11px; }
        ::-webkit-scrollbar-thumb { background: var(--color-base-300); border-radius: 7px; }
        .dark ::-webkit-scrollbar-thumb { background: var(--color-base-700); }
        ::-webkit-scrollbar-track { background: transparent; }
        """;
}
