/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Served by the API at /admin (same origin, strict CSP); `npm run dev` proxies /v1 to a local API.
export default defineConfig({
  base: '/admin/',
  plugins: [react()],
  server: {
    proxy: { '/v1': process.env.NIRENGI_API ?? 'http://localhost:8080' },
  },
  build: {
    chunkSizeWarningLimit: 2000,
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
})
