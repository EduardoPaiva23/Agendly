import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.VITE_API_PROXY ?? 'http://localhost:5133'

  return {
    plugins: [react(), tailwindcss()],
    // Main chunk is React + router + query + forms (~160 kB gzip); admin screens are lazy-loaded.
    build: { chunkSizeWarningLimit: 600 },
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        '/api': { target: apiTarget, changeOrigin: true },
        '/health': { target: apiTarget, changeOrigin: true },
      },
    },
  }
})
