// Oracle extractor for the M3 layered-layout gate. layeredLayout is pure math
// (no DOM), so it runs under vite-node with the browser-measured SizeMap
// (Goldens/measure/cards.json) as input — isolating the layout algorithm from
// measurement. The C# LayeredLayout, fed the same sizes, must match.
//
//   npx vite-node tools/oracle-layout.ts
import { readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import { loadDiagram } from '../src/model'
import { layeredLayout } from '../src/layout/layered'

const here = dirname(fileURLToPath(import.meta.url))
const corpusDir = join(here, '../dotnet/Beck.Rendering.Tests/Corpus')
const goldenDir = join(here, '../dotnet/Beck.Rendering.Tests/Goldens/layout')
const cards = JSON.parse(
  readFileSync(join(here, '../dotnet/Beck.Rendering.Tests/Goldens/measure/cards.json'), 'utf8'),
) as Record<string, Record<string, { w: number; h: number }>>

// Layered types only (sequence uses its own grid engine, gated in M6).
const FILES = [
  'arch-simple', 'arch-grouped', 'arch-flow', 'arch-kitchen',
  'state', 'class', 'sample-architecture', 'sample-class', 'sample-state',
]

const round = (n: number) => Math.round(n * 1000) / 1000
const rectMap = (m: Map<string, { x: number; y: number; w: number; h: number }>) => {
  const out: Record<string, unknown> = {}
  for (const k of [...m.keys()].sort())
    out[k] = { x: round(m.get(k)!.x), y: round(m.get(k)!.y), w: round(m.get(k)!.w), h: round(m.get(k)!.h) }
  return out
}

for (const file of FILES) {
  const yaml = readFileSync(join(corpusDir, `${file}.yaml`), 'utf8')
  const model = loadDiagram(yaml)
  const sizes = new Map(Object.entries(cards[file]).map(([id, wh]) => [id, wh]))
  const layout = layeredLayout(model, sizes)
  const out = {
    nodes: rectMap(layout.nodes),
    groups: rectMap(layout.groups),
    width: round(layout.width),
    height: round(layout.height),
  }
  writeFileSync(join(goldenDir, `${file}.layout.json`), JSON.stringify(out) + '\n', 'utf8')
  console.log(`  ${file}: ${layout.nodes.size} nodes, ${layout.groups.size} groups, ${out.width}x${out.height}`)
}
console.log(`Wrote ${FILES.length} layout goldens.`)
