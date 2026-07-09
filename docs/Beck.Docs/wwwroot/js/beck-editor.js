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
  return {
    current: current,
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
