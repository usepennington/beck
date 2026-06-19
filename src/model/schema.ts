// The normalized diagram model. Authoring YAML is parsed and validated into
// these shapes with every default filled in, so downstream stages (layout,
// route, render, animate) never deal with optional/raw input.

export type Direction = 'TB' | 'BT' | 'LR' | 'RL'
export type ThemeMode = 'auto' | 'light' | 'dark'
/** How a diagram wider than its container behaves. `shrink` (default) scales the
 *  whole diagram down to fit the available width; `scroll` keeps it at natural
 *  size and lets the container scroll horizontally. Vertical size is unaffected. */
export type FitMode = 'shrink' | 'scroll'
export type NodeKind =
  | 'service'
  | 'db'
  | 'queue'
  | 'cache'
  | 'gateway'
  | 'external'
  | 'user'
  | 'ghost'
export type NodeVariant = 'solid' | 'subtle' | 'ghost'
export type EdgeStyle = 'solid' | 'dashed'
export type EdgeCurve = 'step-round' | 'straight' | 's'
export type EdgeKind = 'data' | 'control' | 'async' | 'dependency'
/** Easing token for a travelling packet (mapped to a concrete GSAP ease in the animator). */
export type PacketEase =
  | 'linear'
  | 'smooth'
  | 'accelerate'
  | 'decelerate'
  | 'expo'
  | 'sine'
  | 'steps'
  | 'bounce'
/** Visual form of a travelling packet. `dot` is the default small glowing dot;
 *  `circle` is a larger filled disc; `ring` is a hollow stroked circle. */
export type PacketShape = 'dot' | 'circle' | 'ring'
export type Side = 'top' | 'bottom' | 'left' | 'right'
/** Which ends of an edge carry an arrowhead. */
export type ArrowEnds = 'none' | 'end' | 'start' | 'both'
export type AccentToken = 'primary' | 'success' | 'warn' | 'danger' | 'info' | 'neutral'

export interface Spacing {
  rank: number
  node: number
  cornerRadius: number
}

export interface DiagramMeta {
  title?: string
  subtitle?: string
  direction: Direction
  theme: ThemeMode
  animate: boolean
  loop: boolean
  /** How the diagram behaves when wider than its container (`shrink`/`scroll`). */
  fit: FitMode
  spacing: Spacing
}

export interface NodeModel {
  id: string
  title: string
  subtitle?: string
  /** Named icon key OR raw inline `<svg>` markup. Resolved at render time. */
  icon?: string
  kind: NodeKind
  variant: NodeVariant
  status?: string
  /** CSS color value, e.g. `var(--beck-primary)` or a raw hex. */
  accent: string
  /** Optional link target; renders the card as an `<a href>`. */
  href?: string
  /** Anchor target (e.g. `_blank`); only meaningful with `href`. */
  target?: string
  /** Override the card background (CSS color); defaults to the theme surface. */
  surface?: string
  /** Override the card text color (CSS color); defaults to the theme text. */
  textColor?: string
  width?: number
  rank?: number
  order?: number
  group?: string
}

export interface GroupModel {
  id: string
  label: string
  members: string[]
  accent: string
}

export interface EdgeModel {
  id: string
  from: string
  to: string
  label?: string
  style: EdgeStyle
  curve: EdgeCurve
  kind: EdgeKind
  /** CSS color value for the stroke. */
  color: string
  /** Which ends carry an arrowhead (`true`/`false` authoring maps to end/none). */
  arrow: ArrowEnds
  fromSide?: Side
  toSide?: Side
}

/** Knobs shared by `packet` and `burst` that shape the travelling dot. All are
 *  optional; unset ones fall back to the edge-kind defaults, then to engine
 *  constants. */
export interface PacketKnobs {
  /** Visual form of the packet (`dot` | `circle` | `ring`); defaults to `dot`. */
  shape?: PacketShape
  /** Dot radius in px. */
  size?: number
  /** Travel speed in px/s. */
  speed?: number
  /** Soft glow on the dot. */
  glow?: boolean
  /** Emit an expanding ring (a "burst") at the destination on arrival. */
  impact?: boolean
  /** Easing of the dot's travel (and the trail draw, kept in lockstep). */
  ease?: PacketEase
}

export type FlowStep =
  | ({ type: 'packet'; from: string; to: string; via?: string[]; color?: string; label?: string } & PacketKnobs)
  /** Several dots down an edge (or fanned to many targets), staggered. */
  | ({
      type: 'burst'
      from: string
      to: string | string[]
      via?: string[]
      count: number
      stagger: number
      color?: string
      label?: string
    } & PacketKnobs)
  | { type: 'status'; node: string; text: string; color?: string }
  | { type: 'highlight'; node: string; color?: string }
  | { type: 'pulse'; node: string; color?: string }
  /** Persistently recolor an edge (and its arrowhead) until the next reset. */
  | { type: 'activate'; from: string; to: string; color?: string }
  /** Continuous flowing dashes along an edge (ongoing traffic), until reset. */
  | { type: 'stream'; from: string; to: string; color?: string }
  /** Leave a node visibly busy (breathing glow) until `idle`/reset. */
  | { type: 'working'; node: string; color?: string }
  /** Clear a node's `working` state. */
  | { type: 'idle'; node: string }
  /** A failure beat: red shake + flash, with an optional status text. */
  | { type: 'fail'; node: string; text?: string; color?: string }
  | { type: 'phase'; label: string }
  | { type: 'wait'; seconds: number }
  | { type: 'reset' }
  | { type: 'parallel'; steps: FlowStep[] }

export interface FlowModel {
  repeat: number
  repeatDelay: number
  steps: FlowStep[]
  /** True when the flow was auto-derived from the edges (no `flow:` authored). */
  derived: boolean
}

export interface DiagramModel {
  meta: DiagramMeta
  nodes: NodeModel[]
  groups: GroupModel[]
  edges: EdgeModel[]
  flow: FlowModel
}
