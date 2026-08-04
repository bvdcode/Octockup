import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts",
  },
  server: {
    proxy: {
      "/api": {
        target: "https://octockup.belov.us",
        changeOrigin: true,
        secure: false,
        ws: true,
      },
    },
  },
});
