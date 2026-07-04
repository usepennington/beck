// The normalized diagram model. Authoring YAML is parsed and validated into
// these shapes with every default filled in, so downstream stages (layout,
// route, render, animate) never deal with optional/raw input.

/** What the diagram *is*. Every document declares one via a root `type:` key
 *  (`architecture` is the layered node/edge graph Beck launched with). The type
 *  picks the layout + routing strategy; measure/render/animate are shared. */
export type DiagramType = 'architecture' | 'sequence' | 'state' | 'class'
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
/** Structural form of a node. `card` is the classic architecture card; `pill`
 *  is a state-diagram state; `start`/`end` are the state machine entry/exit
 *  dots; `class` is a multi-compartment UML class card. */
export type NodeShape = 'card' | 'pill' | 'start' | 'end' | 'class'
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
/** End decoration on an edge. Beyond the classic filled `arrow`: `arrow-open`
 *  (a stroked chevron — async/reply messages, dependencies), `triangle`
 *  (hollow — UML inheritance), and `diamond`/`diamond-open` (UML composition/
 *  aggregation). When set on an edge these win over the `arrow` ends. */
export type MarkerShape = 'arrow' | 'arrow-open' | 'triangle' | 'diamond' | 'diamond-open'
export type AccentToken = 'primary' | 'success' | 'warn' | 'danger' | 'info' | 'neutral'

export interface Spacing {
  rank: number
  node: number
  cornerRadius: number
}

export interface DiagramMeta {
  type: DiagramType
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
  /** Structural form; `card` for architecture nodes and sequence participants. */
  shape: NodeShape
  /** UML stereotype rendered as «stereotype» above a class name. */
  stereotype?: string
  /** Class-card field compartment lines (empty for non-class shapes). */
  fields: string[]
  /** Class-card method compartment lines (empty for non-class shapes). */
  methods: string[]
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
  /** Explicit end decorations; when set they win over `arrow` at that end. */
  markerStart?: MarkerShape
  markerEnd?: MarkerShape
  /** Small annotation near the source end (UML multiplicity, e.g. "1"). */
  fromLabel?: string
  /** Small annotation near the target end (UML multiplicity, e.g. "*"). */
  toLabel?: string
  /** Sequence only: a dashed return message (rendered with an open arrowhead). */
  reply: boolean
  /** Sequence only: force-start (true) or suppress (false) an activation bar on
   *  the receiver; undefined lets the request/reply heuristic decide. */
  activate?: boolean
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
  /** `edge` pins the step to one specific edge id — used by sequence diagrams,
   *  where many messages share the same from/to pair. */
  | ({ type: 'packet'; from: string; to: string; via?: string[]; edge?: string; color?: string; label?: string } & PacketKnobs)
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

/** A labelled horizontal band in a sequence diagram, drawn before message `at`. */
export interface SectionMark {
  label: string
  at: number
}

export interface DiagramModel {
  meta: DiagramMeta
  nodes: NodeModel[]
  groups: GroupModel[]
  edges: EdgeModel[]
  flow: FlowModel
  /** Sequence section bands (empty for other diagram types). */
  sections: SectionMark[]
}
