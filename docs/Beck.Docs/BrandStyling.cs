namespace Beck.Docs;

/// <summary>
/// Raw CSS appended to the generated <c>/styles.css</c> via
/// <c>MonorailCssOptions.ExtraStyles</c>. Everything here is something MonorailCSS's
/// utility/discovery pipeline can't (or shouldn't) produce:
/// <list type="bullet">
///   <item>the IBM Plex font wiring (<c>--font-sans</c> / <c>--font-mono</c>);</item>
///   <item>the signature dot-grid surface (<c>.dot-grid</c>) reused by every diagram frame;</item>
///   <item>syntax tokens for the <b>hand-authored</b> code snippets in the hero/API pages
///         (Markdig fences are colored by <c>SyntaxTheme</c> instead);</item>
///   <item>framing for the <c>.beck-embed</c> wrapper the build-time fence renderer and
///         <c>&lt;BeckDiagram&gt;</c> emit around each SVG — it lives in rendered markdown, so
///         it stays declarative CSS rather than utility classes;</item>
///   <item>playground elements created or class-toggled by <c>site.js</c>: the runtime
///         IL scan that discovers utility classes never sees a class that only exists in a
///         JavaScript string, so those stay declarative CSS.</item>
/// </list>
/// All colors resolve from the palette variables MonorailCSS emits for the mapped
/// primary/accent/base slots (and flip under <c>.dark</c>), so this CSS themes itself.
/// </summary>
internal static class BrandStyling
{
    public const string ExtraStyles = """
        /* ---- reset safety net (harmless if MonorailCSS preflight already does it) ---- */
        *, *::before, *::after { box-sizing: border-box; }
        body { margin: 0; }
        html { scroll-padding-top: 5rem; }

        /* ---- fonts: IBM Plex is part of the brand identity ---- */
        :root {
          --font-sans: 'IBM Plex Sans', system-ui, sans-serif;
          --font-mono: 'IBM Plex Mono', ui-monospace, SFMono-Regular, monospace;
        }
        body { font-family: var(--font-sans); }

        /* ---- signature dot-grid surface (apply to our own diagram frames) ---- */
        .dot-grid {
          background-color: var(--color-base-50);
          background-image: radial-gradient(var(--color-base-200) 1px, transparent 1px);
          background-size: 16px 16px;
        }
        .dark .dot-grid {
          background-color: var(--color-base-900);
          background-image: radial-gradient(var(--color-base-800) 1px, transparent 1px);
        }

        /* ---- hand-authored code-sample syntax tokens (hero + API pages) ---- */
        .tok-key    { color: var(--color-primary-700); }
        .tok-accent { color: var(--color-primary-700); }
        .tok-punct  { color: var(--color-base-400); }
        .tok-str    { color: #b45309; }
        .dark .tok-key    { color: var(--color-primary-400); }
        .dark .tok-accent { color: var(--color-primary-400); }
        .dark .tok-punct  { color: var(--color-base-500); }
        .dark .tok-str    { color: #e0b341; }

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
           homepage hero renders onto its own dot-grid card). */
        .beck-embed--bare { border: 0; background: none; padding: 0; margin: 0; min-height: 0; }
        /* The playground renders into its own canvas — strip the fence frame there. */
        .pg-preview-canvas .beck-embed {
          border: 0; background: none; padding: 0; margin: 0; min-height: 0;
        }

        /* ---- lazy style-gallery slots ---- */
        /* The styles guide holds one .beck-lazy placeholder per style; site.js fetches the
           build-baked fragment (three .beck-embed diagrams) as it scrolls near and injects
           it. Until then the slot reserves roughly one gallery's height (three ~200px
           embeds + gaps) so late loads don't yank the scroll position, showing the no-JS
           fallback link centred on the embed dot-grid frame. Once loaded, the slot
           collapses to its injected content. */
        .beck-lazy {
          display: grid; place-items: center;
          min-height: 680px;
          border: 1px dashed var(--color-base-200); border-radius: 12px;
          margin: 0 0 24px;
        }
        .dark .beck-lazy { border-color: var(--color-base-800); }
        .beck-lazy > a { font-size: 0.85rem; color: var(--color-base-500); }
        .beck-lazy.is-loaded {
          display: block; min-height: 0; border: 0; margin: 0;
        }

        /* ---- diagram fullscreen zoom ---- */
        /* BeckSvgPreprocessor emits the .beck-zoom button into each embed; site.js opens the
           .beck-lightbox <dialog>. Both exist only in C#/JS strings, so they stay declarative.
           The button fades in on embed hover (always visible on touch, where there is no hover). */
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

        /* The lightbox: a transparent full-viewport dialog whose ::backdrop dims and blurs the
           page. The cloned SVG renders at its natural size (its width/height attributes), only
           scaling DOWN when it exceeds the viewport — never up, which made small diagrams look
           comically large. In-page embeds constrain diagrams to the article column, so natural
           size is still a real zoom for anything sizable. */
        .beck-lightbox {
          border: 0; padding: 0; margin: auto; background: transparent;
          width: 100vw; height: 100vh; max-width: 100vw; max-height: 100vh;
          overflow: hidden; cursor: zoom-out;
        }
        .beck-lightbox[open] { display: flex; align-items: center; justify-content: center; }
        .beck-lightbox::backdrop {
          background: rgb(0 0 0 / 0.25);
          backdrop-filter: blur(6px);
          -webkit-backdrop-filter: blur(6px);
        }
        .beck-lightbox .beck-svg { width: auto; height: auto; max-width: 94vw; max-height: 90vh; }
        .beck-lightbox-close {
          position: fixed; top: 18px; right: 18px;
          display: flex; align-items: center; justify-content: center;
          width: 38px; height: 38px; padding: 0; cursor: pointer;
          border: 1px solid rgb(255 255 255 / 0.25); border-radius: 9999px;
          background: rgb(0 0 0 / 0.35); color: #fff;
          transition: background-color .15s;
        }
        .beck-lightbox-close:hover { background: rgb(0 0 0 / 0.55); }

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
        /* IL discovery can't see classes that exist only inside a JS string, so these
           stay declarative CSS rather than utilities. */
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

        /* ---- playground toolbar: custom dropdowns (examples + colour scheme) ---- */
        /* The controls are SSR markup wired by site.js (wireDropdown). Structure +
           colour live here because the classes are shared across the trigger/panel/
           items and several are toggled from JS (is-open / is-active). */
        .pg-dd { position: relative; }
        .pg-dd-trigger {
          display: inline-flex; align-items: center; gap: 8px; line-height: 1;
          font: inherit; font-size: 13px; font-weight: 500; cursor: pointer;
          color: var(--color-base-700); background: #fff;
          border: 1px solid var(--color-base-300); border-radius: 8px; padding: 7px 11px;
          transition: border-color .15s, background-color .15s, box-shadow .15s;
        }
        .dark .pg-dd-trigger { color: var(--color-base-300); background: var(--color-base-900); border-color: var(--color-base-700); }
        .pg-dd-trigger:hover { border-color: var(--color-base-400); }
        .dark .pg-dd-trigger:hover { border-color: var(--color-base-600); }
        .pg-dd.is-open .pg-dd-trigger { border-color: var(--color-primary-500); box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-primary-500) 20%, transparent); }
        .pg-dd-value { white-space: nowrap; }
        /* Dimmed lead-in label inside a trigger (e.g. "Style  Classic") so the value reads
           as the selection while the control still says what it controls. */
        .pg-dd-trigger-label { font-weight: 500; color: var(--color-base-400); }
        .dark .pg-dd-trigger-label { color: var(--color-base-500); }
        /* Hint line at the top of a panel (the style dropdown's precedence note). */
        .pg-dd-note {
          font-size: 11.5px; line-height: 1.45; color: var(--color-base-500);
          padding: 6px 12px 8px; margin-bottom: 4px;
          border-bottom: 1px solid var(--color-base-100);
        }
        .dark .pg-dd-note { color: var(--color-base-400); border-bottom-color: var(--color-base-800); }
        .pg-dd-note code { font-family: var(--font-mono); font-size: 11px; }
        .pg-dd-caret { width: 13px; height: 13px; opacity: .5; transition: transform .18s ease; }
        .pg-dd.is-open .pg-dd-caret { transform: rotate(180deg); }

        .pg-dd-panel {
          position: absolute; top: calc(100% + 7px); left: 0; z-index: 40;
          min-width: 384px; max-height: min(66vh, 560px); overflow-y: auto;
          background: #fff; border: 1px solid var(--color-base-200);
          border-radius: 13px; padding: 8px;
          box-shadow: 0 12px 34px -10px rgb(0 0 0 / .28), 0 3px 10px -3px rgb(0 0 0 / .12);
        }
        .dark .pg-dd-panel { background: var(--color-base-900); border-color: var(--color-base-700); box-shadow: 0 14px 40px -10px rgb(0 0 0 / .7); }
        .pg-dd-panel--right { left: auto; right: 0; min-width: 216px; }
        /* Click-catcher behind an open panel (z-index 30 < panel's 40) so an outside
           click closes the dropdown while its items stay clickable. */
        .pg-dd-backdrop { position: fixed; inset: 0; z-index: 30; }

        .pg-dd-group { padding: 2px 0; }
        .pg-dd-group + .pg-dd-group { margin-top: 6px; border-top: 1px solid var(--color-base-100); padding-top: 8px; }
        .dark .pg-dd-group + .pg-dd-group { border-top-color: var(--color-base-800); }
        .pg-dd-group-label {
          font-family: var(--font-mono); font-size: 10px; font-weight: 600;
          letter-spacing: .09em; text-transform: uppercase; color: var(--color-base-400);
          padding: 4px 11px 8px;
        }
        .dark .pg-dd-group-label { color: var(--color-base-500); }

        .pg-dd-item {
          display: flex; align-items: center; gap: 14px; width: 100%;
          text-align: left; font: inherit; cursor: pointer;
          background: none; border: 0; border-radius: 9px; padding: 9px 12px;
          color: var(--color-base-800);
        }
        .pg-dd-item + .pg-dd-item { margin-top: 2px; }
        .dark .pg-dd-item { color: var(--color-base-100); }
        .pg-dd-item.is-active, .pg-dd-item:hover { background: var(--color-base-100); }
        .dark .pg-dd-item.is-active, .dark .pg-dd-item:hover { background: var(--color-base-800); }
        .pg-dd-item[aria-selected="true"] { background: var(--color-primary-50); }
        .dark .pg-dd-item[aria-selected="true"] { background: color-mix(in srgb, var(--color-primary-950) 45%, transparent); }
        .pg-dd-item--scheme { gap: 10px; padding: 8px 12px; }
        .pg-dd-item-main { display: flex; flex-direction: column; gap: 3px; min-width: 0; flex: 1 1 auto; }
        .pg-dd-item-title { font-size: 13px; font-weight: 500; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .pg-dd-item-desc { font-size: 11.5px; line-height: 1.4; color: var(--color-base-500); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .dark .pg-dd-item-desc { color: var(--color-base-400); }

        /* Type pill: colour-coded per diagram type, literal fallbacks so it renders
           even for ramps MonorailCSS doesn't emit (violet/amber aren't always used). */
        .pg-pill {
          flex: none; font-family: var(--font-mono); font-size: 10px; font-weight: 600;
          padding: 3px 8px; border-radius: 999px; white-space: nowrap;
          border: 1px solid transparent;
        }
        .pg-pill[data-type="architecture"] { color: var(--color-emerald-700, #047857); background: color-mix(in srgb, var(--color-emerald-500, #10b981) 14%, transparent); border-color: color-mix(in srgb, var(--color-emerald-500, #10b981) 30%, transparent); }
        .pg-pill[data-type="sequence"]     { color: var(--color-sky-700, #0369a1);     background: color-mix(in srgb, var(--color-sky-500, #0ea5e9) 14%, transparent);     border-color: color-mix(in srgb, var(--color-sky-500, #0ea5e9) 30%, transparent); }
        .pg-pill[data-type="state"]        { color: #b45309;                            background: color-mix(in srgb, #f59e0b 16%, transparent);                            border-color: color-mix(in srgb, #f59e0b 32%, transparent); }
        .pg-pill[data-type="class"]        { color: #6d28d9;                            background: color-mix(in srgb, #8b5cf6 16%, transparent);                            border-color: color-mix(in srgb, #8b5cf6 32%, transparent); }
        .dark .pg-pill[data-type="architecture"] { color: var(--color-emerald-300, #6ee7b7); }
        .dark .pg-pill[data-type="sequence"]     { color: var(--color-sky-300, #7dd3fc); }
        .dark .pg-pill[data-type="state"]        { color: #fcd34d; }
        .dark .pg-pill[data-type="class"]        { color: #c4b5fd; }

        /* Colour-scheme swatch: a two-tone chip previewing each palette's signature. */
        .pg-swatch { flex: none; width: 15px; height: 15px; border-radius: 4px; border: 1px solid rgb(0 0 0 / .18); }
        .dark .pg-swatch { border-color: rgb(255 255 255 / .22); }
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

        /* ---- syntax cheatsheet: filter chips (class-toggled by site.js) ---- */
        /* site.js swaps `.is-active` between chips; declarative so the toggle never
           depends on IL-discovered utility classes that live only in a JS string. */
        .syntax-chip {
          font: inherit; font-weight: 500; font-size: 13px;
          padding: 6px 14px; border-radius: 9999px; cursor: pointer;
          background: #fff; color: var(--color-base-600);
          border: 1px solid var(--color-base-300);
          transition: background-color .15s, color .15s, border-color .15s;
        }
        .dark .syntax-chip {
          background: var(--color-base-900); color: var(--color-base-400);
          border-color: var(--color-base-700);
        }
        .syntax-chip:hover { border-color: var(--color-base-400); color: var(--color-base-900); }
        .dark .syntax-chip:hover { border-color: var(--color-base-600); color: var(--color-base-50); }
        .syntax-chip.is-active {
          background: var(--color-primary-600); border-color: var(--color-primary-600); color: #fff;
        }
        .dark .syntax-chip.is-active {
          background: var(--color-primary-500); border-color: var(--color-primary-500); color: #fff;
        }

        /* ---- syntax cheatsheet: live-diagram canvas ---- */
        /* Each card renders its diagram server-side via <BeckDiagram> into a .beck-embed.
           Strip the fence's framed-preview chrome so the diagram sits directly on the
           card's own dot-grid cell (no nested frame). */
        .syntax-canvas .beck-embed {
          border: 0; background: none; padding: 0; margin: 0; min-height: 0; width: 100%;
        }

        /* ---- icon reference gallery ---- */
        /* The <IconGallery /> component renders these cards server-side from the C# engine's
           icon registry (BeckSvg.IconRegistry). The chip echoes a node's accent-tinted icon
           chip, in brand primary. */
        [data-beck-icon-gallery] {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(128px, 1fr));
          gap: 12px;
        }
        .beck-icon-card {
          display: flex; flex-direction: column; align-items: center; gap: 8px;
          padding: 16px 10px; text-align: center;
          border: 1px solid var(--color-base-200); border-radius: 12px; background: #fff;
        }
        .dark .beck-icon-card { border-color: var(--color-base-800); background: var(--color-base-900); }
        /* NB: distinct from the SVG-internal `.beck-icon-chip` <rect> the engine emits — reusing
           that exact name here would leak width/height onto every rendered node's chip. */
        .beck-icon-card-chip {
          display: flex; align-items: center; justify-content: center;
          width: 40px; height: 40px; border-radius: 10px;
          background: color-mix(in srgb, var(--color-primary-600) 12%, var(--color-base-100));
          color: var(--color-primary-700);
        }
        .dark .beck-icon-card-chip {
          background: color-mix(in srgb, var(--color-primary-500) 18%, var(--color-base-800));
          color: var(--color-primary-400);
        }
        .beck-icon-card-chip svg { width: 22px; height: 22px; }
        .beck-icon-key { font-family: var(--font-mono); font-size: 12.5px; color: var(--color-base-900); }
        .dark .beck-icon-key { color: var(--color-base-50); }
        .beck-icon-aliases { font-family: var(--font-mono); font-size: 10.5px; line-height: 1.4; color: var(--color-base-400); word-break: break-word; }
        .dark .beck-icon-aliases { color: var(--color-base-500); }

        /* ---- API reference sidebar links (scroll-spy class-toggled by site.js) ---- */
        .api-nav-link {
          display: block; width: 100%; text-align: left;
          font-family: var(--font-mono); font-size: 13px;
          padding: 6px 10px; border-radius: 6px; text-decoration: none;
          color: var(--color-base-600);
          transition: background-color .15s, color .15s;
        }
        .dark .api-nav-link { color: var(--color-base-400); }
        .api-nav-link:hover { background: var(--color-base-50); color: var(--color-base-900); }
        .dark .api-nav-link:hover { background: var(--color-base-900); color: var(--color-base-50); }
        .api-nav-link.is-active { background: var(--color-primary-50); color: var(--color-primary-700); }
        .dark .api-nav-link.is-active {
          background: color-mix(in srgb, var(--color-primary-950) 40%, transparent);
          color: var(--color-primary-400);
        }

        /* ---- docs sidebar: mobile disclosure chevron ---- */
        /* site.js toggles the nav's `hidden` and the button's aria-expanded; the chevron
           flips off that attribute (a JS-only class wouldn't be IL-discovered). */
        .docs-nav-chevron { transition: transform .2s ease; }
        .docs-nav-toggle[aria-expanded="true"] .docs-nav-chevron { transform: rotate(180deg); }

        /* ---- global scrollbar polish ---- */
        ::-webkit-scrollbar { width: 11px; height: 11px; }
        ::-webkit-scrollbar-thumb { background: var(--color-base-300); border-radius: 7px; }
        .dark ::-webkit-scrollbar-thumb { background: var(--color-base-700); }
        ::-webkit-scrollbar-track { background: transparent; }
        """;
}
