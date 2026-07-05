import type { Direction } from '../model/schema'

export interface Point {
  x: number
  y: number
}

export interface Rect {
  x: number
  y: number
  w: number
  h: number
}

/**
 * True when `to` sits entirely behind `from` on the primary axis for `dir` — a
 * feedback / "back" edge that runs against the flow. Shared by layout (which
 * reserves an outer gutter for such edges) and routing (which diverts them onto
 * a secondary face), so both stages agree on which edges are feedback loops.
 */
export function againstFlow(from: Rect, to: Rect, dir: Direction): boolean {
  switch (dir) {
    case 'TB':
      return to.y + to.h <= from.y
    case 'BT':
      return to.y >= from.y + from.h
    case 'LR':
      return to.x + to.w <= from.x
    case 'RL':
      return to.x >= from.x + from.w
  }
}

/** Measured intrinsic size of each node, keyed by node id. */
export type SizeMap = Map<string, { w: number; h: number }>

export interface LayoutResult {
  /** Node id → placed rect (top-left, in canvas coordinates). */
  nodes: Map<string, Rect>
  /** Group id → bounding rect (members + padding, room for the label). */
  groups: Map<string, Rect>
  /** Total canvas size. */
  width: number
  height: number
}
