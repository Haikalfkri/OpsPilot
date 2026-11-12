import path from "path"
import tailwindcss from "@tailwindcss/vite"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vitest/config"

export default defineConfig({
  plugins: [react(), tailwindcss()],

  resolve: {
    alias: {
      // import.meta.dirname = folder tempat file ini berada (pengganti __dirname)
      "@": path.resolve(import.meta.dirname, "./src"),
    },
  },

  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    env: { VITE_API_URL: "http://localhost:5130" },
  },

  server: {
    port: 5173,
    strictPort: true,
  },
})