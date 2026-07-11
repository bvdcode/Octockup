import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          const moduleId = id.replaceAll("\\", "/");

          if (!moduleId.includes("/node_modules/")) {
            return;
          }

          if (
            moduleId.includes("/@mui/x-data-grid/") ||
            moduleId.includes("/@mui/x-virtualizer/") ||
            moduleId.includes("/@mui/x-internals/")
          ) {
            return "mui-data-grid";
          }

          if (moduleId.includes("/@mui/icons-material/")) {
            return "mui-icons";
          }

          if (
            moduleId.includes("/@mui/") ||
            moduleId.includes("/@emotion/") ||
            moduleId.includes("/material-ui-confirm/") ||
            moduleId.includes("/react-transition-group/") ||
            moduleId.includes("/@popperjs/")
          ) {
            return "mui-core";
          }

          if (moduleId.includes("/@bvdcode/react-kit/")) {
            return "react-kit";
          }

          if (
            moduleId.includes("/react/") ||
            moduleId.includes("/react-dom/") ||
            moduleId.includes("/react-router/") ||
            moduleId.includes("/react-router-dom/") ||
            moduleId.includes("/scheduler/") ||
            moduleId.includes("/i18next/") ||
            moduleId.includes("/react-i18next/") ||
            moduleId.includes("/zustand/")
          ) {
            return "react-core";
          }
        },
      },
    },
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
