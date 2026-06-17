import type { RenderedNode } from '../render/node'
import type { SizeMap } from './types'

/**
 * Phase 1 of the two-phase measure: read each card's intrinsic size. The caller
 * must have appended the node wraps to a `visibility:hidden` off-flow host so
 * `getBoundingClientRect()` reflects real content size without a visible reflow.
 */
export function measureNodes(rendered: Iterable<RenderedNode>): SizeMap {
  const sizes: SizeMap = new Map()
  for (const r of rendered) {
    const id = r.wrap.dataset.node
    if (!id) continue
    const rect = r.card.getBoundingClientRect()
    sizes.set(id, { w: Math.round(rect.width), h: Math.round(rect.height) })
  }
  return sizes
}
