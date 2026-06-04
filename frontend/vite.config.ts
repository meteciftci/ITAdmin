import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            {
              name: 'react-vendor',
              test: /node_modules\/(react|react-dom|react-router|react-router-dom)\//,
              priority: 30,
            },
            {
              name: 'query-vendor',
              test: /node_modules\/@tanstack\//,
              priority: 25,
            },
            {
              name: 'i18n-vendor',
              test: /node_modules\/(i18next|react-i18next)\//,
              priority: 20,
            },
            {
              name: 'ui-vendor',
              test: /node_modules\/(lucide-react|date-fns|sonner|zustand|clsx|tailwind-merge|class-variance-authority)\//,
              priority: 15,
            },
          ],
        },
      },
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5263',
        changeOrigin: true,
      },
      '/uploads': {
        target: 'http://localhost:5263',
        changeOrigin: true,
      },
    },
  },
})
