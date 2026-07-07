// Oracle extractor for the M1 model-parity gate. Runs the *reference* TS engine's
// `loadDiagram` (DOM-free) over the frozen corpus + every parseable ```beck fence
// in the docs, and writes a canonical JSON golden per file. The C# port's
// ModelJson.Canonical output is compared to these.
//
//   npx vite-node tools/oracle-model.ts
//
// The TS engine is the oracle and is never modified by the C# port work.
import { readFileSync, writeFileSync, readdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join, relative } from 'node:path'
import { loadDiagram } from '../src/model'

const here = dirname(fileURLToPath(import.meta.url))
const corpusDir = join(here, '../dotnet/Beck.Rendering.Tests/Corpus')
const goldenDir = join(here, '../dotnet/Beck.Rendering.Tests/Goldens/model')
const docsDir = join(here, '../docs/Beck.Docs/Content')

/** Recursively sort object keys and drop `undefined` so output is canonical. */
function canon(v: unknown): unknown {
  if (Array.isArray(v)) return v.map(canon)
  if (v && typeof v === 'object') {
    const out: Record<string, unknown> = {}
    for (const k of Object.keys(v as Record<string, unknown>).sort()) {
      const val = (v as Record<string, unknown>)[k]
      if (val === undefined) continue
      out[k] = canon(val)
    }
    return out
  }
  return v
}

function emit(name: string, yaml: string): boolean {
  try {
    const model = loadDiagram(yaml)
    writeFileSync(join(goldenDir, `${name}.model.json`), JSON.stringify(canon(model)) + '\n', 'utf8')
    return true
  } catch (err) {
    console.warn(`  SKIP ${name}: ${(err as Error).message}`)
    return false
  }
}

// 1. Hand-authored corpus fixtures.
let ok = 0
for (const file of readdirSync(corpusDir).sort()) {
  if (!file.endsWith('.yaml')) continue
  const name = file.replace(/\.yaml$/, '')
  if (emit(name, readFileSync(join(corpusDir, file), 'utf8'))) {
    console.log(`  ${name}`)
    ok++
  }
}

// 2. Every parseable ```beck fence in the docs — real-world coverage. Parseable
//    fences are written into the corpus (frozen) so the C# test picks them up.
const fence = /```beck\s*\n([\s\S]*?)```/g
for (const file of readdirSync(docsDir, { recursive: true }) as string[]) {
  if (!file.endsWith('.md')) continue
  const full = join(docsDir, file)
  const slug = relative(docsDir, full).replace(/[\\/]/g, '-').replace(/\.md$/, '')
  const text = readFileSync(full, 'utf8')
  let m: RegExpExecArray | null
  let i = 0
  while ((m = fence.exec(text)) !== null) {
    const yaml = m[1]
    const name = `docs-${slug}-${i}`
    if (emit(name, yaml)) {
      writeFileSync(join(corpusDir, `${name}.yaml`), yaml, 'utf8')
      console.log(`  ${name}`)
      ok++
    }
    i++
  }
}

console.log(`Wrote ${ok} model goldens.`)
