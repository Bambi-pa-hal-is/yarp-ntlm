import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  // Served by ModernApp under /app, through the gateway.
  base: '/app/',
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  server: {
    // `npm run dev`: send API calls through the gateway, like the built app does.
    proxy: {
      '/api': 'http://localhost:5100',
    },
  },
});
