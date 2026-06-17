// Built-in icon set. Each icon uses `stroke="currentColor"` so it inherits the
// node's accent color via CSS (the icon chip sets `color: var(--beck-accent)`).
// A node's `icon` may also be a raw inline `<svg>…</svg>` string, used verbatim.

const svg = (paths: string): string =>
  `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">${paths}</svg>`

export const ICONS: Record<string, string> = {
  service: svg('<rect x="3" y="4" width="18" height="6" rx="1.5"/><rect x="3" y="14" width="18" height="6" rx="1.5"/><path d="M7 7h.01M7 17h.01"/>'),
  server: svg('<rect x="3" y="4" width="18" height="6" rx="1.5"/><rect x="3" y="14" width="18" height="6" rx="1.5"/><path d="M7 7h.01M7 17h.01"/>'),
  db: svg('<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.66 3.58 3 8 3s8-1.34 8-3V5"/><path d="M4 12c0 1.66 3.58 3 8 3s8-1.34 8-3"/>'),
  database: svg('<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.66 3.58 3 8 3s8-1.34 8-3V5"/><path d="M4 12c0 1.66 3.58 3 8 3s8-1.34 8-3"/>'),
  queue: svg('<path d="M3 8 5 5h14l2 3M3 8v9a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V8M3 8h18"/><path d="M9 12h6"/>'),
  // A memory chip — distinct from `bolt` (they used to share the lightning path).
  cache: svg('<rect x="6" y="6" width="12" height="12" rx="1.5"/><rect x="9.5" y="9.5" width="5" height="5" rx="0.5"/><path d="M9 3v3M15 3v3M9 18v3M15 18v3M3 9h3M3 15h3M18 9h3M18 15h3"/>'),
  redis: svg('<rect x="6" y="6" width="12" height="12" rx="1.5"/><rect x="9.5" y="9.5" width="5" height="5" rx="0.5"/><path d="M9 3v3M15 3v3M9 18v3M15 18v3M3 9h3M3 15h3M18 9h3M18 15h3"/>'),
  memory: svg('<rect x="6" y="6" width="12" height="12" rx="1.5"/><rect x="9.5" y="9.5" width="5" height="5" rx="0.5"/><path d="M9 3v3M15 3v3M9 18v3M15 18v3M3 9h3M3 15h3M18 9h3M18 15h3"/>'),
  bolt: svg('<path d="M13 2 4 14h7l-1 8 9-12h-7z"/>'),
  gateway: svg('<path d="M12 2 4 6v6c0 5 3.4 8.5 8 10 4.6-1.5 8-5 8-10V6z"/><path d="m9 12 2 2 4-4"/>'),
  shield: svg('<path d="M12 2 4 6v6c0 5 3.4 8.5 8 10 4.6-1.5 8-5 8-10V6z"/><path d="m9 12 2 2 4-4"/>'),
  external: svg('<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c2.5 2.7 2.5 15.3 0 18M12 3c-2.5 2.7-2.5 15.3 0 18"/>'),
  globe: svg('<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c2.5 2.7 2.5 15.3 0 18M12 3c-2.5 2.7-2.5 15.3 0 18"/>'),
  user: svg('<circle cx="12" cy="8" r="4"/><path d="M4 21c0-4 3.6-7 8-7s8 3 8 7"/>'),
  cloud: svg('<path d="M6 18a4 4 0 0 1 0-8 6 6 0 0 1 11.5-1.5A4.5 4.5 0 0 1 18 18z"/>'),
  lock: svg('<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>'),
  key: svg('<circle cx="8" cy="15" r="4"/><path d="m11 12 9-9M17 6l2 2M15 8l1.5 1.5"/>'),
  terminal: svg('<rect x="3" y="4" width="18" height="16" rx="2"/><path d="m7 9 3 3-3 3M13 15h4"/>'),
  code: svg('<path d="m8 8-4 4 4 4M16 8l4 4-4 4M13 5l-2 14"/>'),
  api: svg('<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M7 9h.01M7 12h.01M7 15h.01M11 9h6M11 12h6M11 15h4"/>'),
  function: svg('<path d="M9 4c-1.5 0-2 1-2 3v2H5m2 0v2c0 2-.5 3-2 3M15 4c1.5 0 2 1 2 3v2h2m-2 0v2c0 2 .5 3 2 3"/>'),
  mobile: svg('<rect x="7" y="3" width="10" height="18" rx="2"/><path d="M11 18h2"/>'),
  browser: svg('<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 9h18M7 6.5h.01M10 6.5h.01"/>'),

  // ---- infra / networking ----
  loadbalancer: svg('<circle cx="5" cy="12" r="2"/><circle cx="19" cy="6" r="2"/><circle cx="19" cy="12" r="2"/><circle cx="19" cy="18" r="2"/><path d="M7 12h2M11 12h6M11 12l6-6M11 12l6 6"/>'),
  lb: svg('<circle cx="5" cy="12" r="2"/><circle cx="19" cy="6" r="2"/><circle cx="19" cy="12" r="2"/><circle cx="19" cy="18" r="2"/><path d="M7 12h2M11 12h6M11 12l6-6M11 12l6 6"/>'),
  cdn: svg('<circle cx="12" cy="12" r="3"/><circle cx="5" cy="6" r="1.6"/><circle cx="19" cy="6" r="1.6"/><circle cx="5" cy="18" r="1.6"/><circle cx="19" cy="18" r="1.6"/><path d="M9.6 10.2 6.2 7.4M14.4 10.2l3.4-2.8M9.6 13.8l-3.4 2.8M14.4 13.8l3.4 2.8"/>'),
  ingress: svg('<path d="M14 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3"/><path d="M3 12h10M9 8l4 4-4 4"/>'),
  firewall: svg('<rect x="3" y="4" width="18" height="16" rx="1.5"/><path d="M3 9h18M3 15h18M9 4v5M15 9v6M9 15v5"/>'),
  vault: svg('<rect x="3" y="4" width="18" height="16" rx="2"/><circle cx="12" cy="12" r="4"/><path d="M12 8v1.5M12 12l2.6 2.6"/>'),
  secret: svg('<rect x="3" y="4" width="18" height="16" rx="2"/><circle cx="12" cy="12" r="4"/><path d="M12 8v1.5M12 12l2.6 2.6"/>'),

  // ---- compute / packaging ----
  container: svg('<path d="M12 3 21 8v8l-9 5-9-5V8z"/><path d="M3 8l9 5 9-5M12 13v8"/>'),
  pod: svg('<path d="M12 3 21 8v8l-9 5-9-5V8z"/><path d="M3 8l9 5 9-5M12 13v8"/>'),
  kubernetes: svg('<path d="M12 2 20 6.2v9.6L12 20 4 15.8V6.2z"/><circle cx="12" cy="11" r="2.4"/><path d="M12 6.2v2.4M16.4 13.6l-2.2-1M7.6 13.6l2.2-1"/>'),
  k8s: svg('<path d="M12 2 20 6.2v9.6L12 20 4 15.8V6.2z"/><circle cx="12" cy="11" r="2.4"/><path d="M12 6.2v2.4M16.4 13.6l-2.2-1M7.6 13.6l2.2-1"/>'),
  lambda: svg('<path d="M7 20c2.4-.2 3.2-1.8 4-4l1.5-5 3.5 9M9 4h1.8c.9 0 1.7.6 2 1.4"/>'),
  serverless: svg('<path d="M7 20c2.4-.2 3.2-1.8 4-4l1.5-5 3.5 9M9 4h1.8c.9 0 1.7.6 2 1.4"/>'),

  // ---- data / storage ----
  bucket: svg('<path d="M5 7h14l-1.2 11.3a2 2 0 0 1-2 1.7H8.2a2 2 0 0 1-2-1.7z"/><path d="M4 7c0-1.7 3.6-3 8-3s8 1.3 8 3-3.6 3-8 3-8-1.3-8-3"/>'),
  storage: svg('<path d="M5 7h14l-1.2 11.3a2 2 0 0 1-2 1.7H8.2a2 2 0 0 1-2-1.7z"/><path d="M4 7c0-1.7 3.6-3 8-3s8 1.3 8 3-3.6 3-8 3-8-1.3-8-3"/>'),
  warehouse: svg('<path d="M3 21V8l9-4 9 4v13"/><path d="M3 21h18M7 21v-7h10v7M7 14h10"/>'),
  file: svg('<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5M9 13h6M9 17h6"/>'),

  // ---- streaming / events ----
  stream: svg('<path d="M3 8c4 0 4.5 3 8 3s4-3 8-3M3 16c4 0 4.5 3 8 3s4-3 8-3"/>'),
  kafka: svg('<path d="M3 8c4 0 4.5 3 8 3s4-3 8-3M3 16c4 0 4.5 3 8 3s4-3 8-3"/>'),
  topic: svg('<path d="M3 8c4 0 4.5 3 8 3s4-3 8-3M3 16c4 0 4.5 3 8 3s4-3 8-3"/>'),
  event: svg('<circle cx="12" cy="12" r="3"/><path d="M12 3v3M12 18v3M3 12h3M18 12h3M5.6 5.6l2.1 2.1M16.3 16.3l2.1 2.1M16.3 7.7l2.1-2.1M5.6 18.4l2.1-2.1"/>'),
  webhook: svg('<path d="M9 9a3 3 0 1 1 4 2.8l-2.2 3.8"/><circle cx="6.5" cy="17" r="2.5"/><circle cx="17.5" cy="17" r="2.5"/><path d="M9 17h6"/>'),

  // ---- AI / ML ----
  brain: svg('<path d="M12 5l1.6 4.2L18 11l-4.4 1.8L12 17l-1.6-4.2L6 11l4.4-1.8z"/><path d="M18.5 15l.8 2 2 .8-2 .8-.8 2-.8-2-2-.8 2-.8z"/>'),
  model: svg('<path d="M12 5l1.6 4.2L18 11l-4.4 1.8L12 17l-1.6-4.2L6 11l4.4-1.8z"/><path d="M18.5 15l.8 2 2 .8-2 .8-.8 2-.8-2-2-.8 2-.8z"/>'),
  llm: svg('<path d="M12 5l1.6 4.2L18 11l-4.4 1.8L12 17l-1.6-4.2L6 11l4.4-1.8z"/><path d="M18.5 15l.8 2 2 .8-2 .8-.8 2-.8-2-2-.8 2-.8z"/>'),
  ai: svg('<path d="M12 5l1.6 4.2L18 11l-4.4 1.8L12 17l-1.6-4.2L6 11l4.4-1.8z"/><path d="M18.5 15l.8 2 2 .8-2 .8-.8 2-.8-2-2-.8 2-.8z"/>'),
  agent: svg('<rect x="5" y="8" width="14" height="11" rx="2.5"/><path d="M12 8V4M12 4h-.01"/><circle cx="9.5" cy="13" r="1"/><circle cx="14.5" cy="13" r="1"/><path d="M9.5 16h5"/>'),
  vector: svg('<circle cx="6" cy="7" r="1.4"/><circle cx="12" cy="5" r="1.4"/><circle cx="18" cy="8" r="1.4"/><circle cx="8" cy="14" r="1.4"/><circle cx="15" cy="13" r="1.4"/><circle cx="11" cy="19" r="1.4"/><path d="M7.4 7 10.6 5.4M13.4 5.2 16.7 7.2M9 13l4.6-.6"/>'),
  embeddings: svg('<circle cx="6" cy="7" r="1.4"/><circle cx="12" cy="5" r="1.4"/><circle cx="18" cy="8" r="1.4"/><circle cx="8" cy="14" r="1.4"/><circle cx="15" cy="13" r="1.4"/><circle cx="11" cy="19" r="1.4"/><path d="M7.4 7 10.6 5.4M13.4 5.2 16.7 7.2M9 13l4.6-.6"/>'),

  // ---- observability / ops ----
  chart: svg('<path d="M4 4v16h16"/><path d="M8 14v3M12 9v8M16 5v12"/>'),
  metrics: svg('<path d="M4 4v16h16"/><path d="M8 14v3M12 9v8M16 5v12"/>'),
  analytics: svg('<path d="M4 4v16h16"/><path d="M8 14v3M12 9v8M16 5v12"/>'),
  monitor: svg('<path d="M3 12h4l2-5 3 10 2-7 2 4h5"/>'),
  search: svg('<circle cx="11" cy="11" r="6"/><path d="m20 20-3.6-3.6"/>'),
  bell: svg('<path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>'),
  notification: svg('<path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>'),
  clock: svg('<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3.5 2"/>'),
  scheduler: svg('<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3.5 2"/>'),
  cron: svg('<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3.5 2"/>'),

  // ---- collaboration / messaging ----
  mail: svg('<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3.5 7 8.5 6 8.5-6"/>'),
  email: svg('<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3.5 7 8.5 6 8.5-6"/>'),
  git: svg('<circle cx="6" cy="6" r="2.2"/><circle cx="6" cy="18" r="2.2"/><circle cx="17" cy="9" r="2.2"/><path d="M6 8.2v7.6M6 12h6.5a2.5 2.5 0 0 0 2.5-2.5v-.1"/>'),
  repo: svg('<path d="M5 4h12a2 2 0 0 1 2 2v14H7a2 2 0 0 1-2-2z"/><path d="M5 16h14M9 4v12"/>'),
}

/** Resolve an icon key (or raw inline svg) to svg markup, or null if unknown. */
export function resolveIcon(key: string | undefined): string | null {
  if (!key) return null
  const trimmed = key.trim()
  if (trimmed.startsWith('<svg')) return trimmed
  return ICONS[trimmed] ?? null
}

/** True if `key` is renderable: a known icon name or raw inline `<svg>` markup. */
export function isKnownIcon(key: string): boolean {
  const trimmed = key.trim()
  return trimmed.startsWith('<svg') || trimmed in ICONS
}
