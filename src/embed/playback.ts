import { STYLES } from '../styles'
import type { DiagramHandle } from '../core'

function button(label: string): HTMLButtonElement {
  const b = document.createElement('button')
  b.className = 'beck-playback-btn'
  b.type = 'button'
  b.textContent = label
  return b
}

/**
 * Optional playback controls: `<beck-playback for="diagram-id">`. Finds the
 * referenced `<beck-diagram>` and drives its handle.
 */
export class BeckPlaybackElement extends HTMLElement {
  connectedCallback(): void {
    const shadow = this.shadowRoot ?? this.attachShadow({ mode: 'open' })
    shadow.replaceChildren()
    const style = document.createElement('style')
    style.textContent = STYLES
    shadow.appendChild(style)

    const bar = document.createElement('div')
    bar.className = 'beck-playback'
    const playBtn = button('Pause')
    const resetBtn = button('Restart')
    bar.append(playBtn, resetBtn)
    shadow.appendChild(bar)

    const target = (): DiagramHandle | null => {
      const id = this.getAttribute('for')
      if (!id) return null
      const el = (this.getRootNode() as Document | ShadowRoot).getElementById?.(id) ?? document.getElementById(id)
      return (el as unknown as { diagram?: DiagramHandle })?.diagram ?? null
    }

    let playing = true
    playBtn.onclick = () => {
      const d = target()
      if (!d) return
      if (playing) {
        d.pause()
        playBtn.textContent = 'Play'
      } else {
        d.play()
        playBtn.textContent = 'Pause'
      }
      playing = !playing
    }
    resetBtn.onclick = () => {
      const d = target()
      if (!d) return
      d.reset()
      d.play()
      playing = true
      playBtn.textContent = 'Pause'
    }
  }
}
