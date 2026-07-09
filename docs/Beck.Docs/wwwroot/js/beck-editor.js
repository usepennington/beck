// Theme bridge for the playground's Monaco editor.
//
// Monaco needs literal colours, so the editor can't consume the site's CSS colour variables
// the way a rendered diagram does. Instead the playground defines two static Monaco themes
// (beck / beck-dark) in C# and flips between them when the site theme changes. This shim just
// reports the current site theme and notifies .NET when it toggles — it renders nothing. (The
// site theme toggle lives in site.js and stamps `.dark` + data-theme on the <html> element.)
window.beckEditor = (function () {
  function current() {
    return document.documentElement.classList.contains('dark') ? 'dark' : 'light';
  }
  var observer = null;

  // ---- playground boot watchdog -------------------------------------------
  // The playground's editor + preview panes are painted by a WebAssembly island
  // (PlaygroundIsland). If the WASM runtime is blocked — an enterprise CSP without
  // wasm-unsafe-eval, say — the island never activates, its first render never runs,
  // and the prerendered chrome would sit blank forever with no word to the reader.
  // Rendering is now WASM, so a textarea can't restore live preview; the honest fix
  // is to say so. Arm a generous timer on the playground page; the island calls
  // beckEditor.ready() the instant it activates, disarming it. If that never comes,
  // replace the panes with a clear, styled message. A healthy boot disarms the timer
  // long before it fires, so the notice never flashes.
  var watchdog = null, booted = false;

  function armWatchdog() {
    var panes = document.getElementById('pg-panes');
    if (!panes || booted) return;
    if (watchdog) clearTimeout(watchdog);
    watchdog = setTimeout(function () {
      if (!booted) showPlaygroundFallback(panes);
    }, 20000);
  }

  function showPlaygroundFallback(panes) {
    if (!document.getElementById('pg-fallback-style')) {
      var s = document.createElement('style');
      s.id = 'pg-fallback-style';
      // Themed off the site's own --color-* / --font-mono tokens so it tracks light/dark.
      s.textContent =
        '.pg-fallback{max-width:30rem;text-align:center;padding:2rem;}' +
        '.pg-fallback-eyebrow{font-family:var(--font-mono,ui-monospace,monospace);font-size:11px;' +
        'letter-spacing:.15em;text-transform:uppercase;color:var(--color-primary-700);margin-bottom:.75rem;}' +
        '.pg-fallback h2{font-size:1.25rem;font-weight:600;color:var(--color-base-900);margin-bottom:.5rem;}' +
        '.pg-fallback p{font-size:.9rem;line-height:1.55;color:var(--color-base-500);}' +
        '.dark .pg-fallback-eyebrow{color:var(--color-primary-400);}' +
        '.dark .pg-fallback h2{color:var(--color-base-50);}' +
        '.dark .pg-fallback p{color:var(--color-base-400);}';
      document.head.appendChild(s);
    }
    panes.style.display = 'flex';
    panes.style.alignItems = 'center';
    panes.style.justifyContent = 'center';
    panes.innerHTML =
      '<div class="pg-fallback">' +
        '<div class="pg-fallback-eyebrow">Playground unavailable</div>' +
        '<h2>This browser can’t run the interactive playground</h2>' +
        '<p>The playground compiles and renders diagrams with WebAssembly, which appears to be ' +
        'blocked here — often an enterprise security policy or a strict content-security-policy. ' +
        'Everything else in the docs works, and every example on the site renders as static SVG.</p>' +
      '</div>';
  }

  // ---- playground dropdown keyboard focus ---------------------------------
  // Move roving focus between an open dropdown's options, or back to its trigger.
  // Kept in JS because Blazor can't address a dynamic, grouped option list by index
  // without a ref map. delta 0 (just opened) lands on the selected option, else first.
  function focusDdOption(rootId, delta) {
    var root = document.getElementById(rootId);
    if (!root) return;
    var items = Array.prototype.slice.call(root.querySelectorAll('.pg-dd-item'));
    if (!items.length) return;
    var i = items.indexOf(document.activeElement), next;
    if (i < 0) {
      next = 0;
      for (var k = 0; k < items.length; k++) {
        if (items[k].getAttribute('aria-selected') === 'true') { next = k; break; }
      }
    } else {
      next = (i + delta + items.length) % items.length;
    }
    items[next].focus();
  }

  function focusDdTrigger(rootId) {
    var root = document.getElementById(rootId);
    var t = root && root.querySelector('.pg-dd-trigger');
    if (t) t.focus();
  }

  return {
    current: current,
    armWatchdog: armWatchdog,
    // Called by PlaygroundIsland the instant the WASM island activates and first paints.
    ready: function () { booted = true; if (watchdog) { clearTimeout(watchdog); watchdog = null; } },
    focusDdOption: focusDdOption,
    focusDdTrigger: focusDdTrigger,
    // Watch <html> for theme flips; invoke the .NET OnHostThemeChanged for each. Returns the
    // theme at hook-up time so the editor can set its initial theme in the same round-trip.
    observeTheme: function (dotNetRef) {
      if (observer) observer.disconnect();
      var last = current();
      observer = new MutationObserver(function () {
        var now = current();
        if (now === last) return;
        last = now;
        dotNetRef.invokeMethodAsync('OnHostThemeChanged', now);
      });
      observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'data-theme'] });
      return last;
    },
    // Kick a stalled first paint. Arriving at /playground via Blazor enhanced navigation can
    // activate the WASM island while the navigation's DOM merge is still moving nodes, so
    // Monaco's initial render runs against a detached container and paints nothing — and no
    // later event re-triggers it (automaticLayout only reacts to size *changes*, and the size
    // was measured correctly). Re-run layout+render until a line actually paints; on a healthy
    // boot the first pass is a no-op.
    nudge: function () {
      var tries = 0;
      (function tick() {
        if (!window.monaco) return;
        monaco.editor.getEditors().forEach(function (e) { e.layout(); e.render(true); });
        var painted = document.querySelector('.monaco-editor .view-line');
        if (!painted && ++tries < 30) requestAnimationFrame(tick);
      })();
    },
  };
})();

// This script is `defer`, so the DOM (including the prerendered playground chrome)
// is parsed by the time it runs; full page loads re-run it per navigation. Arm the
// boot watchdog now — it no-ops on every page that isn't the playground.
window.beckEditor.armWatchdog();
