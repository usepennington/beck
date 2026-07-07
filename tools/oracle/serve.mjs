// Minimal static file server rooted at the repo root, for the browser measurement
// oracle. Serves the oracle HTML and the committed font TTFs so a font-pinned page
// can measure text/cards the same way the C# engine does.
//
//   node tools/oracle/serve.mjs [port]   (run from the repo root)
import { createServer } from 'node:http'
import { readFile } from 'node:fs/promises'
import { extname, join, normalize } from 'node:path'

const root = process.cwd()
const port = Number(process.argv[2] || 5599)
const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript',
  '.mjs': 'text/javascript',
  '.css': 'text/css',
  '.ttf': 'font/ttf',
  '.otf': 'font/otf',
  '.json': 'application/json',
  '.yaml': 'text/plain',
  '.svg': 'image/svg+xml',
}

createServer(async (req, res) => {
  try {
    const url = decodeURIComponent(req.url.split('?')[0])
    const path = join(root, normalize(url))
    if (!path.startsWith(root)) { res.statusCode = 403; return res.end('forbidden') }
    const data = await readFile(path)
    res.setHeader('Content-Type', types[extname(path)] || 'application/octet-stream')
    res.setHeader('Access-Control-Allow-Origin', '*')
    res.end(data)
  } catch {
    res.statusCode = 404
    res.end('not found')
  }
}).listen(port, () => console.log(`oracle server on http://localhost:${port}`))
