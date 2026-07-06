import './main.css'
import { renderDiagram, type DiagramHandle } from '../src'
import simple from './samples/simple.yaml?raw'
import grouped from './samples/grouped.yaml?raw'
import flow from './samples/flow.yaml?raw'
import narrated from './samples/narrated.yaml?raw'
import sequence from './samples/sequence.yaml?raw'
import state from './samples/state.yaml?raw'
import classes from './samples/class.yaml?raw'

type ThemeMode = 'light' | 'dark'

const samples: Record<string, string> = {
  Simple: simple,
  Grouped: grouped,
  Flow: flow,
  Narrated: narrated,
  Sequence: sequence,
  State: state,
  Class: classes,
}

let theme: ThemeMode = 'light'
let handle: DiagramHandle | null = null

const app = document.getElementById('app')!
app.innerHTML = `
  <div class="pg">
    <aside class="pg-side">
      <h1 class="pg-title">Beck</h1>
      <p class="pg-sub">Declarative, animated architecture diagrams from YAML.</p>
      <div class="pg-row" id="samples"></div>
      <div class="pg-row">
        <button class="pg-btn" id="theme">Dark mode</button>
        <button class="pg-btn" id="replay">Replay</button>
      </div>
      <textarea class="pg-editor" id="editor" spellcheck="false"></textarea>
    </aside>
    <main class="pg-main"><div class="pg-stage" id="stage"></div></main>
  </div>`

const stage = document.getElementById('stage') as HTMLElement
const editor = document.getElementById('editor') as HTMLTextAreaElement
const samplesRow = document.getElementById('samples') as HTMLElement
const themeBtn = document.getElementById('theme') as HTMLButtonElement
const replayBtn = document.getElementById('replay') as HTMLButtonElement

function render(yaml: string): void {
  try {
    handle?.destroy()
    handle = renderDiagram(stage, yaml, { theme })
  } catch (err) {
    handle = null
    const pre = document.createElement('pre')
    pre.className = 'pg-error'
    pre.textContent = err instanceof Error ? err.message : String(err)
    stage.replaceChildren(pre)
  }
}

const sampleButtons: HTMLButtonElement[] = []
for (const name of Object.keys(samples)) {
  const btn = document.createElement('button')
  btn.className = 'pg-btn'
  btn.textContent = name
  btn.onclick = () => {
    sampleButtons.forEach((b) => b.classList.toggle('active', b === btn))
    editor.value = samples[name]
    render(editor.value)
  }
  samplesRow.appendChild(btn)
  sampleButtons.push(btn)
}

let debounce: ReturnType<typeof setTimeout> | null = null
editor.addEventListener('input', () => {
  if (debounce) clearTimeout(debounce)
  debounce = setTimeout(() => render(editor.value), 350)
})

themeBtn.onclick = () => {
  theme = theme === 'light' ? 'dark' : 'light'
  document.body.dataset.theme = theme === 'dark' ? 'dark' : ''
  themeBtn.textContent = theme === 'light' ? 'Dark mode' : 'Light mode'
  handle?.setTheme(theme)
}

replayBtn.onclick = () => {
  handle?.reset()
  handle?.play()
}

// initial
sampleButtons[0].classList.add('active')
editor.value = samples.Simple
render(editor.value)
