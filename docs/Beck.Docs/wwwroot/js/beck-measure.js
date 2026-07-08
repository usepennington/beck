/* Text metrics for the WASM playground.

   The pure-C# Beck engine does everything (layout, routing, SVG, animation) except one thing
   it can't do in the browser without native code: measure glyphs. The browser is the ground
   truth here — it's what will actually draw the SVG <text> — so the WASM CanvasTextMeasurer
   calls this to measure with a 2D canvas, exactly like the old JS engine used getBoundingClientRect.
   This is measurement only; nothing here renders a diagram. */
window.beckMeasure = (function () {
  var canvas = document.createElement('canvas');
  var ctx = canvas.getContext('2d');

  function font(mono, weight, px) {
    var family = mono ? "'IBM Plex Mono', ui-monospace, monospace"
                      : "'IBM Plex Sans', ui-sans-serif, sans-serif";
    return weight + ' ' + px + 'px ' + family;
  }

  return {
    // Returns [advanceWidth, ascent, descent] in CSS px for one text run. Ascent/descent come
    // from the font's bounding box (constant per font+size), matching Skia's SKFontMetrics.
    text: function (t, mono, weight, px) {
      ctx.font = font(mono, weight, px);
      var m = ctx.measureText(t || '');
      var asc = m.fontBoundingBoxAscent;
      var desc = m.fontBoundingBoxDescent;
      if (asc === undefined || asc === null) { asc = px * 0.8; desc = px * 0.2; } // pre-2022 fallback
      return [m.width, asc, desc];
    },

    // Resolve once the site's IBM Plex faces are actually loaded, so the first measurement
    // isn't taken against a fallback font (which would mis-size the cards until a re-render).
    fontsReady: async function () {
      try {
        var faces = [
          "300 16px 'IBM Plex Sans'", "400 16px 'IBM Plex Sans'", "500 16px 'IBM Plex Sans'",
          "600 16px 'IBM Plex Sans'", "700 16px 'IBM Plex Sans'",
          "400 16px 'IBM Plex Mono'", "500 16px 'IBM Plex Mono'", "700 16px 'IBM Plex Mono'"
        ];
        await Promise.all(faces.map(function (f) { return document.fonts.load(f); }));
        await document.fonts.ready;
      } catch (e) { /* fall back to whatever is loaded */ }
    }
  };
})();
