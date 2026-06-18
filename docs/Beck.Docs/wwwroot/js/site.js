/* Beck docs — theme toggle, copy buttons, the hero typewriter, and the live playground.
   The Beck engine (beck.global.js) watches <html data-theme> and re-themes any
   hydrated ```beck fence automatically; we drive the imperative hero + playground
   diagrams' theme (and Monaco's) explicitly. */
(function () {
  'use strict';

  function currentTheme() {
    return document.documentElement.classList.contains('dark') ? 'dark' : 'light';
  }

  // <beck-diagram> custom elements pick their theme from the `mode` attribute, not from
  // <html data-theme> (that's only the hydrated-fence path). Sync them to the site theme so
  // mode="auto" elements don't get stuck following the OS while the site toggles.
  function syncDiagramModes(theme) {
    document.querySelectorAll('beck-diagram').forEach(function (el) { el.setAttribute('mode', theme); });
  }

  function setTheme(theme) {
    var root = document.documentElement;
    // MonorailCSS keys dark mode off the `.dark` class; the Beck engine keys its
    // hydrated-fence theming off `data-theme`. Drive both from one toggle. The
    // sun/moon glyphs swap purely in CSS via the `.dark` class — no JS needed.
    root.classList.toggle('dark', theme === 'dark');
    root.setAttribute('data-theme', theme);
    try { localStorage.setItem('theme', theme); } catch (e) {}
    syncDiagramModes(theme);
    window.dispatchEvent(new CustomEvent('beck:themechange', { detail: { theme: theme } }));
  }

  function toggleTheme() { setTheme(currentTheme() === 'dark' ? 'light' : 'dark'); }
  window.beckToggleTheme = toggleTheme;

  // ---- palette bridge -----------------------------------------------------
  // MonorailCSS emits the brand palette as --color-* CSS variables. The ramp does
  // NOT flip under `.dark`: --color-base-50 is always the lightest and -900/-950 the
  // darkest, in both themes. Dark mode is achieved by *selecting a different shade per
  // mode* (e.g. bg = dark ? base-900 : base-50; fg = dark ? base-100 : base-800), not
  // by the var values changing — so every shade picked below must be chosen for the
  // active mode (picking the same var for both modes, as fg once did, paints dark-on-
  // dark in the suggest widget). Monaco needs literal hex, so we resolve the *computed*
  // colour of each token through a hidden probe. The catch: MonorailCSS emits `oklch()`,
  // and getComputedStyle returns that verbatim (modern engines no longer down-convert
  // to rgb) — so the rgb() regex alone silently fails and every colour falls back to
  // its literal, drifting the editor off the live palette. We paint the resolved colour
  // onto a 1x1 canvas and read sRGB bytes back, which normalises any format
  // (hex/rgb/oklch/lab/color()). We redefine the single 'beck' Monaco theme on every
  // theme change so the re-picked shades take effect.
  function rgbToHex(rgb) {
    var m = /rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)/.exec(rgb || '');
    if (!m) return null;
    return '#' + [1, 2, 3].map(function (i) {
      return ('0' + Math.round(parseFloat(m[i])).toString(16)).slice(-2);
    }).join('');
  }
  var _hexCanvas = null;
  function colorToHex(str) {
    if (!str) return null;
    var fast = rgbToHex(str);
    if (fast) return fast;
    // Non-rgb (oklch/lab/color()/named): round-trip through a canvas. A sentinel
    // fill detects an engine that can't parse the colour (assignment is ignored,
    // leaving the sentinel) so we degrade to the caller's literal fallback.
    try {
      if (!_hexCanvas) { _hexCanvas = document.createElement('canvas'); _hexCanvas.width = _hexCanvas.height = 1; }
      var ctx = _hexCanvas.getContext('2d');
      ctx.fillStyle = 'rgb(1,2,3)';
      ctx.fillStyle = str;
      ctx.fillRect(0, 0, 1, 1);
      var d = ctx.getImageData(0, 0, 1, 1).data;
      if (d[0] === 1 && d[1] === 2 && d[2] === 3) return null;
      return '#' + [d[0], d[1], d[2]].map(function (x) { return ('0' + x.toString(16)).slice(-2); }).join('');
    } catch (e) { return null; }
  }
  var _probe = null;
  function cssVarHex(name, fallback) {
    fallback = fallback || '#000000';
    try {
      if (!_probe) { _probe = document.createElement('span'); _probe.style.display = 'none'; document.body.appendChild(_probe); }
      _probe.style.color = 'var(' + name + ', ' + fallback + ')';
      return colorToHex(getComputedStyle(_probe).color) || fallback;
    } catch (e) { return fallback; }
  }

  // ---- copy buttons -------------------------------------------------------
  function wireCopy() {
    document.querySelectorAll('[data-copy]').forEach(function (el) {
      if (el.__wired) return; el.__wired = true;
      el.addEventListener('click', function () {
        var text = el.getAttribute('data-copy');
        if (text === '' || text == null) {
          var host = el.closest('.code-block, .install-chip');
          var body = host && (host.querySelector('.code-body') || host.querySelector('.cmd'));
          text = body ? body.innerText : '';
        }
        navigator.clipboard && navigator.clipboard.writeText(text);
        var prev = el.textContent; el.textContent = 'copied'; setTimeout(function () { el.textContent = prev; }, 1200);
      });
    });
  }

  // ---- Beck readiness -----------------------------------------------------
  function whenBeck(cb) {
    if (window.Beck && window.Beck.renderDiagram) return cb();
    var tries = 0;
    var t = setInterval(function () {
      if (window.Beck && window.Beck.renderDiagram) { clearInterval(t); cb(); }
      else if (++tries > 100) { clearInterval(t); }
    }, 50);
  }

  function countYaml(text) {
    var lines = (text || '').split('\n');
    var nodes = 0, edges = 0, section = null;
    for (var i = 0; i < lines.length; i++) {
      var t = lines[i].trim();
      if (/^nodes:/.test(t)) { section = 'nodes'; continue; }
      if (/^edges:/.test(t)) { section = 'edges'; continue; }
      if (/^groups:/.test(t)) { section = 'groups'; continue; }
      if (/^(meta|flow|title|direction):/.test(t)) { section = null; continue; }
      if (!t.startsWith('-')) continue;
      if (section === 'nodes') nodes++;
      else if (section === 'edges') edges++;
    }
    return { nodes: nodes, edges: edges };
  }

  // ---- hero: typed.js writes the YAML, the diagram grows in step ----------
  function escHtml(s) {
    return s.replace(/[&<>]/g, function (c) { return c === '&' ? '&amp;' : c === '<' ? '&lt;' : '&gt;'; });
  }
  // Wrap a Beck-YAML string in tok-* spans (same vocabulary BrandStyling colours).
  // The plain-text content is byte-for-byte the input, so typed.js progress
  // (element.textContent length) maps cleanly onto line offsets.
  function highlightYaml(src) {
    return src.split('\n').map(function (line) {
      var out = '', i = 0, m;
      while (i < line.length) {
        var rest = line.slice(i);
        if (rest[0] === ' ') { out += ' '; i++; continue; }
        if ((m = /^[A-Za-z_][\w-]*(?=\s*:)/.exec(rest))) { out += '<span class="tok-key">' + m[0] + '</span>'; i += m[0].length; continue; }
        if (/^[:{}\[\],]/.test(rest)) { out += '<span class="tok-punct">' + rest[0] + '</span>'; i++; continue; }
        if (rest[0] === '-' && (rest.length === 1 || rest[1] === ' ')) { out += '<span class="tok-punct">-</span>'; i++; continue; }
        m = /^[^:{}\[\],]+/.exec(rest);
        var run = m ? m[0] : rest[0];
        var val = run.replace(/\s+$/, ''), trail = run.slice(val.length);
        out += (val ? '<span class="tok-str">' + escHtml(val) + '</span>' : '') + trail;
        i += run.length;
      }
      return out;
    }).join('\n');
  }

  function initHero() {
    var codeEl = document.getElementById('hero-code');
    var host = document.getElementById('hero-host');
    if (!codeEl || !host) return;

    var lines = [
      'meta:',
      '  title: Web Platform',
      '  direction: TB',
      'nodes:',
      '  - { id: client, title: Client, kind: user }',
      '  - { id: api, title: API Gateway, kind: gateway }',
      '  - { id: orders, title: Orders }',
      '  - { id: users, title: Users, kind: db }',
      'edges:',
      '  - { from: client, to: api }',
      '  - { from: api, to: orders }',
      '  - { from: api, to: users }'
    ];
    var fullText = lines.join('\n');
    var upto = function (last) { return lines.slice(0, last + 1).join('\n'); };
    var endAt = function (last) { return upto(last).length; };

    // Type-then-pause: drop a short typed.js pause (`^ms`, stripped from the output so
    // it doesn't disturb the stage offsets) once each node / edge line lands, giving
    // every new element and connector a beat on screen before the next one types in.
    var PAUSE_AFTER = { 4: 1, 5: 1, 6: 1, 7: 1, 9: 1, 10: 1 };
    var typedHtml = lines.map(function (line, i) {
      return highlightYaml(line) + (PAUSE_AFTER[i] ? '^650' : '');
    }).join('\n');

    // Each stage is a COMPLETE, valid snapshot rendered once typing passes its
    // offset, so the preview never flashes a parse error. The build-up frames are
    // static; the finished diagram animates its flow.
    var stages = [
      { at: endAt(4), yaml: upto(4), animate: false },   // client
      { at: endAt(5), yaml: upto(5), animate: false },   // + api
      { at: endAt(6), yaml: upto(6), animate: false },   // + orders
      { at: endAt(7), yaml: upto(7), animate: false },   // + users (all four nodes)
      { at: endAt(9), yaml: upto(9), animate: false },   // + client → api
      { at: endAt(10), yaml: upto(10), animate: false }, // + api → orders
      { at: endAt(11), yaml: fullText, animate: true }   // + api → users, live
    ];

    var handle = null, rendered = -1;
    function renderStage(i) {
      if (i <= rendered) return;
      rendered = i;
      try { if (handle && handle.destroy) handle.destroy(); } catch (e) {}
      try { handle = window.Beck.renderDiagram(host, stages[i].yaml, { theme: currentTheme(), animate: stages[i].animate }); } catch (e) {}
    }
    function syncToLength(len) {
      for (var i = stages.length - 1; i >= 0; i--) {
        if (len >= stages[i].at) { renderStage(i); return; }
      }
    }

    window.addEventListener('beck:themechange', function (e) {
      if (handle && handle.setTheme) handle.setTheme(e.detail.theme);
    });

    whenBeck(function () {
      // Drive the diagram off the typed text growing: a MutationObserver watches the
      // element's textContent length and fires the matching stage. Decoupled from
      // typed.js internals, so it's robust to the library's HTML-typing quirks.
      var obs = new MutationObserver(function () { syncToLength((codeEl.textContent || '').length); });
      obs.observe(codeEl, { childList: true, characterData: true, subtree: true });

      if (window.Typed) {
        // eslint-disable-next-line no-new
        new window.Typed('#hero-code', {
          strings: [typedHtml],
          contentType: 'html',
          typeSpeed: 11,
          startDelay: 300,
          showCursor: true,
          cursorChar: '▍',
          loop: false,
          onComplete: function (self) {
            syncToLength(fullText.length);
            if (self && self.cursor) setTimeout(function () { self.cursor.style.opacity = '0'; }, 1600);
          }
        });
      } else {
        // typed.js CDN blocked — render the finished state immediately.
        codeEl.innerHTML = highlightYaml(fullText);
        syncToLength(fullText.length);
      }
    });
  }

  // ---- Monaco: the VS Code editor, loaded from CDN with a worker shim -----
  var MONACO_BASE = 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs';
  function loadMonaco(cb) {
    if (window.monaco && window.monaco.editor) return cb();
    if (window.__monacoCbs) { window.__monacoCbs.push(cb); return; }
    window.__monacoCbs = [cb];
    // Cross-origin workers can't be loaded directly from a CDN; proxy via a data: URL.
    window.MonacoEnvironment = {
      getWorkerUrl: function () {
        return 'data:text/javascript;charset=utf-8,' + encodeURIComponent(
          "self.MonacoEnvironment={baseUrl:'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/'};" +
          "importScripts('https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs/base/worker/workerMain.js');");
      }
    };
    var s = document.createElement('script');
    s.src = MONACO_BASE + '/loader.js';
    s.onload = function () {
      window.require.config({ paths: { vs: MONACO_BASE } });
      window.require(['vs/editor/editor.main'], function () {
        var cbs = window.__monacoCbs; window.__monacoCbs = null;
        (cbs || []).forEach(function (f) { f(false); });
      });
    };
    s.onerror = function () {
      var cbs = window.__monacoCbs; window.__monacoCbs = null;
      (cbs || []).forEach(function (f) { f(true); });
    };
    document.head.appendChild(s);
  }

  // ---- Monaco theme: derived from the live brand palette ------------------
  function noHash(h) { return (h || '#000000').replace('#', ''); }
  function withAlpha(hex, aa) { return (hex || '#000000') + aa; }

  function applyBeckTheme(monaco) {
    var dark = currentTheme() === 'dark';
    // Pick the mode-appropriate shade for each role (the ramp doesn't flip — see the
    // palette-bridge note). Fallbacks only bite if /styles.css hasn't loaded yet.
    // fg MUST track the mode: base-800 is dark in both themes, so reusing it in dark
    // mode paints dark text on the dark suggest-widget/editor background.
    var fg = cssVarHex(dark ? '--color-base-100' : '--color-base-800', dark ? '#e7e7ec' : '#2a2a30');
    var bg = cssVarHex(dark ? '--color-base-900' : '--color-base-50', dark ? '#16181d' : '#ffffff');
    var line = cssVarHex(dark ? '--color-base-600' : '--color-base-400', dark ? '#56565f' : '#8c8c97');
    var key = cssVarHex(dark ? '--color-primary-400' : '--color-primary-700', dark ? '#34c77b' : '#19794d');
    var accent = cssVarHex(dark ? '--color-accent-400' : '--color-accent-600', dark ? '#a78bfa' : '#7c3aed');
    var punct = cssVarHex(dark ? '--color-base-500' : '--color-base-400', dark ? '#7a7a85' : '#8c8c97');
    var sel = cssVarHex(dark ? '--color-primary-900' : '--color-primary-100', dark ? '#10341f' : '#d6f0e1');
    var border = cssVarHex(dark ? '--color-base-700' : '--color-base-200', dark ? '#3a3a42' : '#e7e7ec');
    // A neutral row-highlight for the suggest/hover lists — green here kills contrast.
    var rowSel = cssVarHex(dark ? '--color-base-700' : '--color-base-200', dark ? '#3a3a42' : '#e7e7ec');
    var str = dark ? '#e0b341' : '#b45309';

    monaco.editor.defineTheme('beck', {
      base: dark ? 'vs-dark' : 'vs',
      inherit: true,
      rules: [
        { token: '', foreground: noHash(fg) },
        { token: 'type', foreground: noHash(key) },
        { token: 'key', foreground: noHash(key) },
        { token: 'tag', foreground: noHash(key) },
        { token: 'attribute.name', foreground: noHash(key) },
        { token: 'string', foreground: noHash(str) },
        { token: 'string.yaml', foreground: noHash(str) },
        { token: 'attribute.value', foreground: noHash(str) },
        { token: 'number', foreground: noHash(accent) },
        { token: 'keyword', foreground: noHash(accent) },
        { token: 'comment', foreground: noHash(punct), fontStyle: 'italic' },
        { token: 'delimiter', foreground: noHash(punct) },
        { token: 'operators', foreground: noHash(punct) }
      ],
      colors: {
        'editor.background': bg,
        'editor.foreground': fg,
        'editorLineNumber.foreground': line,
        'editorLineNumber.activeForeground': key,
        'editorCursor.foreground': key,
        'editor.selectionBackground': sel,
        'editor.inactiveSelectionBackground': withAlpha(sel, '99'),
        'editor.lineHighlightBackground': '#00000000',
        'editor.lineHighlightBorder': '#00000000',
        'editorIndentGuide.background': border,
        'editorIndentGuide.background1': border,
        'editorIndentGuide.activeBackground': line,
        'editorIndentGuide.activeBackground1': line,
        'editorWhitespace.foreground': border,
        'editorBracketMatch.background': withAlpha(key, '22'),
        'editorBracketMatch.border': key,
        'editorSuggestWidget.background': bg,
        'editorSuggestWidget.border': border,
        'editorSuggestWidget.foreground': fg,
        'editorSuggestWidget.selectedBackground': rowSel,
        'editorSuggestWidget.selectedForeground': fg,
        'editorSuggestWidget.highlightForeground': key,
        'editorSuggestWidget.focusHighlightForeground': key,
        'editorSuggestWidget.selectedIconForeground': key,
        'list.hoverBackground': withAlpha(rowSel, '99'),
        'editorHoverWidget.background': bg,
        'editorHoverWidget.border': border,
        'editorWidget.background': bg,
        'editorWidget.border': border,
        'input.background': bg,
        'input.border': border,
        'focusBorder': '#00000000',
        'scrollbarSlider.background': withAlpha(line, '55'),
        'scrollbarSlider.hoverBackground': withAlpha(line, '99'),
        'scrollbarSlider.activeBackground': withAlpha(line, 'cc')
      }
    });
    monaco.editor.setTheme('beck');
  }

  // ---- Monaco intellisense: the Beck YAML schema --------------------------
  var SCHEMA = {
    KINDS: ['service', 'db', 'queue', 'cache', 'gateway', 'external', 'user', 'ghost'],
    ACCENTS: ['primary', 'success', 'warn', 'danger', 'info', 'neutral'],
    DIRECTIONS: ['TB', 'BT', 'LR', 'RL'],
    THEMES: ['auto', 'light', 'dark'],
    VARIANTS: ['solid', 'subtle', 'ghost'],
    EDGE_STYLES: ['solid', 'dashed'],
    CURVES: ['step-round', 'straight', 's'],
    ARROWS: ['none', 'end', 'start', 'both', 'true', 'false'],
    EDGE_KINDS: ['data', 'control', 'async', 'dependency'],
    SIDES: ['top', 'bottom', 'left', 'right'],
    BOOL: ['true', 'false'],
    FLOW_STEPS: ['packet', 'status', 'highlight', 'pulse', 'activate', 'stream', 'working', 'idle', 'fail', 'phase', 'wait', 'reset', 'parallel'],
    ICONS: ('service server db database queue cache redis memory bolt gateway shield external globe user cloud lock ' +
      'key terminal code api function mobile browser loadbalancer lb cdn ingress firewall vault secret container pod ' +
      'kubernetes k8s lambda serverless bucket storage warehouse file stream kafka topic event webhook brain model ' +
      'llm ai agent vector embeddings chart metrics analytics monitor search bell notification clock scheduler cron ' +
      'mail email git repo').split(' '),
    TOP_KEYS: ['meta', 'nodes', 'edges', 'groups', 'flow'],
    META_KEYS: ['title', 'subtitle', 'direction', 'theme', 'animate', 'loop', 'spacing'],
    NODE_KEYS: ['id', 'title', 'subtitle', 'icon', 'kind', 'variant', 'status', 'accent', 'href', 'target', 'surface', 'textColor', 'width', 'rank', 'order', 'group'],
    EDGE_KEYS: ['from', 'to', 'label', 'style', 'curve', 'kind', 'color', 'arrow', 'fromSide', 'toSide'],
    GROUP_KEYS: ['id', 'label', 'members', 'accent']
  };

  var DOCS = {
    meta: 'Diagram-wide settings — title, direction, theme, animation.',
    nodes: 'The boxes in your diagram. Each needs an `id`; `title`/`kind`/`icon` are optional.',
    edges: 'Connections between nodes. `from` + `to` are required.',
    groups: 'Boundaries that wrap members (nodes or nested groups) in a labelled box.',
    flow: 'A scripted animation: packets, highlights and status changes over time.',
    direction: 'Layout flow: `TB`, `BT`, `LR` or `RL`.',
    kind: 'Preset that picks an icon, accent and shape.',
    icon: 'Named icon key (or inline `<svg>`). e.g. `db`, `gateway`, `brain`, `kafka`.',
    accent: 'Colour token: primary, success, warn, danger, info, neutral.',
    variant: 'Visual weight: `solid`, `subtle` or `ghost`.',
    status: 'A small status pill shown on the card.',
    from: 'Source node (or group) id.',
    to: 'Target node (or group) id.',
    label: 'Text drawn on the edge (or the group box heading).',
    curve: 'Edge shape: `step-round`, `straight` or `s`.',
    arrow: 'Arrowheads: `none`, `end`, `start`, `both` (or `true`/`false`).',
    style: 'Line style: `solid` or `dashed`.',
    members: 'Ids of the nodes (or nested groups) this group contains.',
    title: 'Display name on the card / diagram heading.',
    subtitle: 'Secondary line under the title.',
    theme: '`auto`, `light` or `dark`.',
    animate: 'Set `false` to render a static frame (no flow animation).',
    loop: 'Set `false` to play the flow once instead of looping.',
    spacing: 'Fine-tune `rank`/`node` gaps and `cornerRadius`.',
    group: 'Inline membership: place this node in the named group.',
    href: 'Make the card a link to this URL.',
    fromSide: 'Pin the edge start to a side: top, bottom, left, right.',
    toSide: 'Pin the edge end to a side: top, bottom, left, right.',
    width: 'Override the card width, in pixels.',
    color: 'Edge stroke colour — an accent token or a CSS colour.',
    // value docs
    solid: 'Default card weight (full surface + border).', subtle: 'Dimmed card for lower emphasis.',
    service: 'A generic service box.', db: 'A database (cylinder).', queue: 'A message queue.',
    cache: 'A cache store.', gateway: 'An API gateway / entry point.', external: 'A third-party / external system.',
    user: 'A person or client.', ghost: 'A faded placeholder node.',
    data: 'Solid data edge (default).', control: 'A control-flow edge.', async: 'A dashed asynchronous edge.',
    dependency: 'A dashed dependency edge.',
    TB: 'Top → bottom.', BT: 'Bottom → top.', LR: 'Left → right.', RL: 'Right → left.'
  };

  // Nearest top-level section header above `lineNumber` (block + flow styles).
  function sectionAt(model, lineNumber) {
    for (var ln = lineNumber; ln >= 1; ln--) {
      var m = /^(meta|nodes|edges|groups|flow)\s*:/.exec(model.getLineContent(ln));
      if (m) return m[1];
    }
    return null;
  }

  // Every `id:` value in the document — both node and group ids are valid edge
  // endpoints / group members, and `id:` is the field for both.
  function declaredIds(model) {
    var ids = [], seen = {}, re = /(?:^|[\s{,])id\s*:\s*([A-Za-z0-9_-]+)/g, m, text = model.getValue();
    while ((m = re.exec(text))) { if (!seen[m[1]]) { seen[m[1]] = 1; ids.push(m[1]); } }
    return ids;
  }

  function registerBeckLanguageFeatures(monaco) {
    if (monaco.__beckWired) return;
    monaco.__beckWired = true;
    var K = monaco.languages.CompletionItemKind;

    function items(values, kind, range, detail) {
      return values.map(function (v) {
        var it = { label: v, kind: kind, insertText: v, range: range };
        if (detail) it.detail = detail;
        if (DOCS[v]) it.documentation = { value: DOCS[v] };
        return it;
      });
    }

    monaco.languages.registerCompletionItemProvider('yaml', {
      triggerCharacters: [':', ' ', '-', '{', '[', ','],
      provideCompletionItems: function (model, position) {
        var pre = model.getLineContent(position.lineNumber).substr(0, position.column - 1);
        var section = sectionAt(model, position.lineNumber);

        // ---- value context: `<field>: <partial>` ----
        var vm = /(?:^|[\s{,])([A-Za-z_][\w-]*)\s*:\s*([\w-]*)$/.exec(pre);
        if (vm) {
          var field = vm[1], partial = vm[2] || '';
          var vrange = {
            startLineNumber: position.lineNumber, endLineNumber: position.lineNumber,
            startColumn: position.column - partial.length, endColumn: position.column
          };
          var map = {
            direction: SCHEMA.DIRECTIONS, theme: SCHEMA.THEMES, variant: SCHEMA.VARIANTS,
            style: SCHEMA.EDGE_STYLES, curve: SCHEMA.CURVES, arrow: SCHEMA.ARROWS,
            icon: SCHEMA.ICONS, accent: SCHEMA.ACCENTS, color: SCHEMA.ACCENTS,
            fromSide: SCHEMA.SIDES, toSide: SCHEMA.SIDES, animate: SCHEMA.BOOL, loop: SCHEMA.BOOL
          };
          if (field === 'kind') return { suggestions: items(section === 'edges' ? SCHEMA.EDGE_KINDS : SCHEMA.KINDS, K.EnumMember, vrange, 'kind') };
          if (field === 'from' || field === 'to' || field === 'via' || field === 'members' || field === 'node') {
            return { suggestions: items(declaredIds(model), K.Value, vrange, 'id') };
          }
          if (map[field]) return { suggestions: items(map[field], field === 'icon' ? K.Color : K.EnumMember, vrange, field) };
          return { suggestions: [] };
        }

        // ---- key context ----
        var word = model.getWordUntilPosition(position);
        var krange = {
          startLineNumber: position.lineNumber, endLineNumber: position.lineNumber,
          startColumn: word.startColumn, endColumn: word.endColumn
        };
        var insideBrace = /\{[^}]*$/.test(pre);
        var indent = (/^(\s*)/.exec(pre)[1] || '').length;
        var keys;
        if (insideBrace) keys = section === 'edges' ? SCHEMA.EDGE_KEYS : section === 'groups' ? SCHEMA.GROUP_KEYS : SCHEMA.NODE_KEYS;
        else if (indent === 0) keys = SCHEMA.TOP_KEYS;
        else if (section === 'meta') keys = SCHEMA.META_KEYS;
        else if (section === 'edges') keys = SCHEMA.EDGE_KEYS;
        else if (section === 'groups') keys = SCHEMA.GROUP_KEYS;
        else if (section === 'flow') keys = SCHEMA.FLOW_STEPS;
        else keys = SCHEMA.NODE_KEYS;

        var sugg = keys.map(function (k) {
          var it = { label: k, kind: K.Property, insertText: k + ': ', range: krange };
          if (DOCS[k]) it.documentation = { value: DOCS[k] };
          return it;
        });

        // Handy scaffold snippets in the relevant sections.
        var SNIP = monaco.languages.CompletionItemKind.Snippet;
        var ins = monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet;
        function snip(label, text, doc) { sugg.push({ label: label, kind: SNIP, insertText: text, insertTextRules: ins, range: krange, documentation: { value: doc }, detail: 'snippet' }); }
        if (insideBrace) { /* mid-object: keys only */ }
        else if (section === 'nodes') snip('node', '- { id: ${1:id}, title: ${2:Title}, kind: ${3:service} }', 'A node entry.');
        else if (section === 'edges') snip('edge', '- { from: ${1:a}, to: ${2:b} }', 'An edge entry.');
        else if (section === 'groups') snip('group', '- { id: ${1:id}, label: ${2:Label}, members: [${3:}] }', 'A group entry.');
        else if (indent === 0) {
          snip('nodes', 'nodes:\n  - { id: ${1:id}, title: ${2:Title} }', 'Start the nodes list.');
          snip('edges', 'edges:\n  - { from: ${1:a}, to: ${2:b} }', 'Start the edges list.');
        }
        return { suggestions: sugg };
      }
    });

    monaco.languages.registerHoverProvider('yaml', {
      provideHover: function (model, position) {
        var word = model.getWordAtPosition(position);
        if (!word) return null;
        var doc = DOCS[word.word];
        if (!doc) return null;
        return {
          range: new monaco.Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn),
          contents: [{ value: '**' + word.word + '**' }, { value: doc }]
        };
      }
    });
  }

  // ---- playground ---------------------------------------------------------
  function initPlayground() {
    var mount = document.getElementById('pg-editor-host');
    var host = document.getElementById('pg-host');
    if (!mount || !host) return;

    var initialEl = document.getElementById('pg-initial');
    var initial = initialEl ? initialEl.value : '';
    var statusEl = document.getElementById('pg-status');
    var nodesEl = document.getElementById('pg-nodes');
    var edgesEl = document.getElementById('pg-edges');
    var examples = document.getElementById('pg-examples');
    var handle = null, timer = null, editor = null, fallbackTa = null;

    function getYaml() { return editor ? editor.getValue() : (fallbackTa ? fallbackTa.value : initial); }
    function setYaml(v) { if (editor) editor.setValue(v); else if (fallbackTa) fallbackTa.value = v; }

    function render() {
      var yaml = getYaml();
      var counts = countYaml(yaml);
      if (nodesEl) nodesEl.textContent = counts.nodes + ' nodes';
      if (edgesEl) edgesEl.textContent = counts.edges + ' edges';
      try { if (handle && handle.destroy) handle.destroy(); } catch (e) {}
      try {
        handle = window.Beck.renderDiagram(host, yaml, { theme: currentTheme() });
        if (statusEl) { statusEl.textContent = '✓ valid'; statusEl.className = 'ok'; }
      } catch (err) {
        if (statusEl) { statusEl.textContent = '✗ ' + (err && err.message ? err.message : 'invalid'); statusEl.className = 'err'; }
      }
    }
    function schedule() { clearTimeout(timer); timer = setTimeout(render, 220); }

    function useTextarea() {
      fallbackTa = document.createElement('textarea');
      fallbackTa.className = 'pg-editor';
      fallbackTa.spellcheck = false;
      fallbackTa.value = initial;
      mount.appendChild(fallbackTa);
      fallbackTa.addEventListener('input', schedule);
      whenBeck(render);
    }

    loadMonaco(function (failed) {
      if (failed || !(window.monaco && window.monaco.editor)) { useTextarea(); return; }
      applyBeckTheme(window.monaco);
      registerBeckLanguageFeatures(window.monaco);
      editor = window.monaco.editor.create(mount, {
        value: initial,
        language: 'yaml',
        theme: 'beck',
        automaticLayout: true,
        minimap: { enabled: false },
        fontSize: 14,
        fontFamily: "'IBM Plex Mono', ui-monospace, monospace",
        lineNumbers: 'on',
        renderLineHighlight: 'none',
        scrollBeyondLastLine: false,
        tabSize: 2,
        insertSpaces: true,
        padding: { top: 14, bottom: 14 },
        wordWrap: 'off',
        overviewRulerLanes: 0,
        quickSuggestions: { other: true, comments: false, strings: true },
        suggestOnTriggerCharacters: true,
        tabCompletion: 'on',
        scrollbar: { verticalScrollbarSize: 10, horizontalScrollbarSize: 10 }
      });
      editor.onDidChangeModelContent(schedule);
      whenBeck(render);
    });

    if (examples) {
      examples.addEventListener('change', function () {
        var opt = examples.options[examples.selectedIndex];
        var src = opt && opt.getAttribute('data-src');
        if (!src) return;
        fetch(src).then(function (r) { return r.text(); }).then(function (txt) { setYaml(txt); render(); });
      });
    }
    window.addEventListener('beck:themechange', function (e) {
      if (window.monaco && window.monaco.editor) applyBeckTheme(window.monaco);
      if (handle && handle.setTheme) handle.setTheme(e.detail.theme);
    });
  }

  // ---- syntax cheatsheet: category filter ---------------------------------
  // The chips and cards are static SSR markup (Razor); wire the filtering here.
  // A chip's data-filter is matched against each card's data-cat ('all' shows
  // everything). Active state is a single class so it themes via BrandStyling.
  function initSyntaxFilter() {
    var bar = document.getElementById('syntax-filter');
    var grid = document.getElementById('syntax-grid');
    if (!bar || !grid) return;
    var chips = [].slice.call(bar.querySelectorAll('[data-filter]'));
    var cards = [].slice.call(grid.querySelectorAll('[data-cat]'));

    function apply(filter) {
      chips.forEach(function (c) { c.classList.toggle('is-active', c.getAttribute('data-filter') === filter); });
      cards.forEach(function (card) {
        var cats = (card.getAttribute('data-cat') || '').split(/\s+/);
        card.style.display = (filter === 'all' || cats.indexOf(filter) !== -1) ? '' : 'none';
      });
    }

    chips.forEach(function (c) {
      c.addEventListener('click', function () { apply(c.getAttribute('data-filter') || 'all'); });
    });
    apply('all');
  }

  // ---- API reference: scroll-spy the sidebar -------------------------------
  // Highlight the sidebar link for whichever class section is currently at the
  // top of the viewport. Anchors still jump on click (scroll-mt keeps the
  // heading clear of the sticky nav); this just tracks the active one.
  function initApiNav() {
    var nav = document.getElementById('api-nav');
    if (!nav) return;
    // Links are page-absolute (/api/#id, not #id) so <base href="/"> can't hijack
    // them to the homepage; a.hash gives just the fragment either way.
    var map = [].slice.call(nav.querySelectorAll('a[href*="#"]')).map(function (a) {
      return { a: a, el: a.hash ? document.getElementById(a.hash.slice(1)) : null };
    }).filter(function (m) { return m.el; });
    if (!map.length) return;

    function spy() {
      var active = map[0];
      // Active = the last section whose top has passed just below the sticky nav
      // (matches where an anchor click lands a section, ~scroll-padding-top).
      for (var i = 0; i < map.length; i++) {
        if (map[i].el.getBoundingClientRect().top - 100 <= 1) active = map[i];
      }
      map.forEach(function (m) { m.a.classList.toggle('is-active', m === active); });
    }
    window.addEventListener('scroll', spy, { passive: true });
    spy();
  }

  // ---- mobile nav (hamburger → dropdown) ----------------------------------
  function initNavToggle() {
    var btn = document.querySelector('.nav-toggle');
    var menu = document.getElementById('nav-menu');
    if (!btn || !menu || btn.__wired) return;
    btn.__wired = true;
    var openIcon = btn.querySelector('.nav-open-icon');
    var closeIcon = btn.querySelector('.nav-close-icon');

    function setOpen(open) {
      menu.classList.toggle('hidden', !open);
      btn.setAttribute('aria-expanded', open ? 'true' : 'false');
      btn.setAttribute('aria-label', open ? 'Close menu' : 'Open menu');
      if (openIcon) openIcon.classList.toggle('hidden', open);
      if (closeIcon) closeIcon.classList.toggle('hidden', !open);
    }

    btn.addEventListener('click', function (e) {
      e.stopPropagation();
      setOpen(menu.classList.contains('hidden'));
    });
    menu.addEventListener('click', function (e) { if (e.target.closest('a')) setOpen(false); });
    document.addEventListener('click', function (e) {
      if (!menu.classList.contains('hidden') && !menu.contains(e.target) && !btn.contains(e.target)) setOpen(false);
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') setOpen(false); });
    // Grew past the breakpoint while open (hamburger vanished) — reset to closed.
    window.addEventListener('resize', function () {
      if (window.innerWidth >= 768 && !menu.classList.contains('hidden')) setOpen(false);
    });
  }

  // ---- boot ---------------------------------------------------------------
  function boot() {
    document.querySelectorAll('.theme-toggle').forEach(function (b) {
      if (b.__wired) return; b.__wired = true;
      b.addEventListener('click', toggleTheme);
    });
    initNavToggle();
    wireCopy();
    // Sync any <beck-diagram> elements to the current theme (now and once Beck upgrades them).
    syncDiagramModes(currentTheme());
    whenBeck(function () { syncDiagramModes(currentTheme()); });
    initHero();
    initPlayground();
    initSyntaxFilter();
    initApiNav();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
