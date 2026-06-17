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

/** Measured intrinsic size of each node, keyed by node id. */
export type SizeMap = Map<string, { w: number; h: number }>

export interface LayoutResult {
  /** Node id → placed rect (top-left, in canvas coordinates). */
  nodes: Map<string, Rect>
  /** Group id → bounding rect (members + padding, room for the label). */
  groups: Map<string, Rect>
  /** Interior waypoints for long edges (edge id → points), for the router. */
  waypoints: Map<string, Point[]>
  /** Total canvas size. */
  width: number
  height: number
}

export function rectCenter(r: Rect): Point {
  return { x: r.x + r.w / 2, y: r.y + r.h / 2 }
}
