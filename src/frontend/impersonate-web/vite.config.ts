import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5001',
      '/health': 'http://localhost:5001',
      '/openapi': 'http://localhost:5001'
    }
  },
  test: { environment: 'jsdom', setupFiles: './src/test/setup.ts', restoreMocks: true }
});
