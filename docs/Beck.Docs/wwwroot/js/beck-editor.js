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
  };
})();
