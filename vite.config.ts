import { defineConfig } from 'vite'
import tailwindcss from '@tailwindcss/vite'

// Playground dev/build config.
export default defineConfig({
  plugins: [tailwindcss()],
})
