/* Beck docs — theme toggle, copy buttons, and the live playground.
   The Beck engine (beck.global.js) watches <html data-theme> and re-themes any
   hydrated ```beck fence automatically; we only drive the imperative playground
   diagram's theme explicitly. */
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

  // ---- playground ---------------------------------------------------------
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

  // ---- Monaco (the VS Code editor), loaded from CDN with a worker shim ----
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
      editor = window.monaco.editor.create(mount, {
        value: initial,
        language: 'yaml',
        theme: currentTheme() === 'dark' ? 'vs-dark' : 'vs',
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
      if (window.monaco && window.monaco.editor) window.monaco.editor.setTheme(e.detail.theme === 'dark' ? 'vs-dark' : 'vs');
      if (handle && handle.setTheme) handle.setTheme(e.detail.theme);
    });
  }

  // ---- boot ---------------------------------------------------------------
  function boot() {
    document.querySelectorAll('.theme-toggle').forEach(function (b) {
      if (b.__wired) return; b.__wired = true;
      b.addEventListener('click', toggleTheme);
    });
    wireCopy();
    // Sync any <beck-diagram> elements to the current theme (now and once Beck upgrades them).
    syncDiagramModes(currentTheme());
    whenBeck(function () { syncDiagramModes(currentTheme()); });
    initPlayground();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
