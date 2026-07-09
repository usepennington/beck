/* Beck docs — theme toggle, copy buttons, and page chrome (nav, syntax filter, API scroll-spy).
   Every diagram on the site is rendered to static SVG by the pure-C# engine (```beck fences at
   build time, the playground in the browser via WebAssembly), so there is no client Beck engine
   to drive here. */
(function () {
  'use strict';

  function currentTheme() {
    return document.documentElement.classList.contains('dark') ? 'dark' : 'light';
  }

  function setTheme(theme) {
    var root = document.documentElement;
    // MonorailCSS keys dark mode off the `.dark` class; the C# diagrams key their dark tokens
    // off `[data-theme]`. Drive both from one toggle. The sun/moon glyphs swap purely in CSS.
    root.classList.toggle('dark', theme === 'dark');
    root.setAttribute('data-theme', theme);
    try { localStorage.setItem('theme', theme); } catch (e) {}
  }

  function toggleTheme() { setTheme(currentTheme() === 'dark' ? 'light' : 'dark'); }
  window.beckToggleTheme = toggleTheme;

  // The persisted theme (stored value, else system pref) — mirrors the head script.
  function desiredTheme() {
    var stored;
    try { stored = localStorage.getItem('theme'); } catch (e) {}
    return stored ? stored : (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
  }

  // Keep <html> in sync with the persisted theme. The head script applies it before
  // first paint, but Blazor's interactive Router re-renders the root component on every
  // in-app navigation and resets <html>'s attributes to the server markup (no `.dark`/
  // `data-theme` — those are runtime-only), wiping the theme. A MutationObserver reapplies
  // it whenever that happens. The mismatch guard makes our own writes a no-op, so the
  // observer never loops. This is more robust than any one navigation event (`enhancedload`
  // fires for enhanced nav but NOT the interactive Router used here).
  function enforceTheme() {
    var root = document.documentElement, t = desiredTheme();
    if (root.classList.contains('dark') !== (t === 'dark') || root.getAttribute('data-theme') !== t) {
      root.classList.toggle('dark', t === 'dark');
      root.setAttribute('data-theme', t);
    }
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

  // ---- docs sidebar (collapsible on narrow screens) ----------------------
  // The left rail is a sticky sidebar at lg+, but below that the grid drops to one
  // column and the rail would bury the article, so it collapses behind a disclosure.
  // The nav defaults to `hidden` (shown via `lg:block` at lg+ regardless); this just
  // flips that on tap below lg. The chevron rotates purely off aria-expanded (CSS).
  function initDocsNav() {
    var btn = document.querySelector('.docs-nav-toggle');
    var nav = document.getElementById('docs-nav');
    if (!btn || !nav || btn.__wired) return;
    btn.__wired = true;

    function setOpen(open) {
      nav.classList.toggle('hidden', !open);
      btn.setAttribute('aria-expanded', open ? 'true' : 'false');
    }

    btn.addEventListener('click', function () { setOpen(nav.classList.contains('hidden')); });
    // Navigating collapses the menu so the article isn't pushed down after a tap.
    nav.addEventListener('click', function (e) { if (e.target.closest('a')) setOpen(false); });
    // Grew past the breakpoint (lg:block takes over): reset to closed so shrinking
    // back lands on the collapsed default rather than a stuck-open list.
    window.addEventListener('resize', function () {
      if (window.innerWidth >= 1024) setOpen(false);
    });
  }

  // ---- diagram fullscreen zoom ---------------------------------------------
  // BeckSvgPreprocessor emits a `.beck-zoom` button into each rendered ```beck embed.
  // Clicking it opens a native <dialog> lightbox with a CLONE of the diagram SVG —
  // cloning is safe because all its animation/theming is hash-scoped CSS classes that
  // apply to the copy too, and its url(#id) refs still resolve to the original's defs,
  // which stay in the document. The dialog is built per-open and removed on close, so
  // nothing here can be wiped by Blazor's Router re-rendering the page.
  function openBeckLightbox(embed) {
    var svg = embed.querySelector('.beck-svg');
    if (!svg || document.querySelector('.beck-lightbox')) return;

    var dialog = document.createElement('dialog');
    dialog.className = 'beck-lightbox';
    dialog.setAttribute('aria-label', 'Diagram, full screen');

    var clone = svg.cloneNode(true);
    // Drop the engine's inline sizing (max-width cap + height:auto) so the lightbox
    // CSS controls the box: natural size from the width/height attributes, shrunk
    // proportionally only when it exceeds the viewport.
    clone.removeAttribute('style');
    dialog.appendChild(clone);

    var close = document.createElement('button');
    close.type = 'button';
    close.className = 'beck-lightbox-close';
    close.setAttribute('aria-label', 'Close full screen view');
    close.innerHTML = '<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12"/></svg>';
    dialog.appendChild(close);

    // Any click closes: the backdrop, the diagram itself, or the ✕ (standard
    // lightbox behavior); Esc is handled natively by <dialog>.
    dialog.addEventListener('click', function () { dialog.close(); });
    dialog.addEventListener('close', function () { dialog.remove(); });
    document.body.appendChild(dialog);
    dialog.showModal();
  }

  // ---- boot ---------------------------------------------------------------
  function boot() {
    initNavToggle();
    initDocsNav();
    wireCopy();
    initSyntaxFilter();
    initApiNav();
  }

  // Theme toggle is delegated on `document` (which survives Blazor navigation) rather
  // than wired per-button in boot(), since boot() doesn't re-run on in-app navigation
  // and the buttons are re-rendered fresh (unwired) each time.
  document.addEventListener('click', function (e) {
    if (e.target.closest('.theme-toggle')) toggleTheme();
    var zoom = e.target.closest('.beck-zoom');
    if (zoom) {
      var embed = zoom.closest('.beck-embed');
      if (embed) openBeckLightbox(embed);
    }
  });

  // Reassert the theme whenever Blazor's Router re-renders <html> and clears it.
  enforceTheme();
  new MutationObserver(enforceTheme).observe(document.documentElement, {
    attributes: true, attributeFilter: ['class', 'data-theme']
  });

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
