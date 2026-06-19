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
///   <item>framing for the <c>.beck-embed</c> wrapper the Beck engine injects when it
///         hydrates a <c>```beck</c> fence — we can't put utility classes on DOM we don't render;</item>
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

        /* ---- Beck engine embeds (Beck injects this DOM; can't utility-class it) ---- */
        /* A hydrated ```beck fence becomes <div class="beck-embed">; frame it on the
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
          align-items: center;
          justify-content: center;
          min-height: 200px;
          max-width: 100%;
          overflow: auto;
        }
        .dark .beck-embed {
          border-color: var(--color-base-800);
          background-color: var(--color-base-900);
          background-image: radial-gradient(var(--color-base-800) 1px, transparent 1px);
        }
        .prose .beck-embed > .beck-root { max-width: 100%; }
        beck-diagram { display: block; max-width: 100%; }
        /* The playground renders into its own canvas — strip the fence frame there. */
        .pg-preview-canvas .beck-embed {
          border: 0; background: none; padding: 0; margin: 0; min-height: 0;
        }

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
        #pg-editor-host { flex: 1 1 0%; min-height: 0; width: 100%; overflow: hidden; background: var(--color-base-50); }
        .dark #pg-editor-host { background: var(--color-base-900); }
        #pg-editor-host .monaco-editor, #pg-editor-host .monaco-editor .overflow-guard { border-radius: 0; }
        .pg-editor {
          flex: 1; border: none; outline: none; resize: none; width: 100%;
          padding: 16px 18px; font-family: var(--font-mono); font-size: 14px; line-height: 1.8;
          background: var(--color-base-50); color: var(--color-base-900); tab-size: 2;
        }
        .dark .pg-editor { background: var(--color-base-900); color: var(--color-base-50); }
        #pg-status.ok  { color: var(--color-primary-600); }
        #pg-status.err { color: #e6685b; }

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
        /* Each card emits a ```beck fence the client hydrates into a light-DOM .beck-embed.
           Hide the raw YAML until then, and strip the fence's framed-preview chrome so the
           diagram sits directly on the card's own dot-grid cell (no nested frame). */
        .syntax-canvas > pre { display: none; }
        .syntax-canvas .beck-embed {
          border: 0; background: none; padding: 0; margin: 0; min-height: 0; width: 100%;
        }

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

        /* ---- global scrollbar polish ---- */
        ::-webkit-scrollbar { width: 11px; height: 11px; }
        ::-webkit-scrollbar-thumb { background: var(--color-base-300); border-radius: 7px; }
        .dark ::-webkit-scrollbar-thumb { background: var(--color-base-700); }
        ::-webkit-scrollbar-track { background: transparent; }
        """;
}
