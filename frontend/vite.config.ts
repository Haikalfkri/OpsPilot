import path from "path"
import tailwindcss from "@tailwindcss/vite"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

export default defineConfig({
  plugins: [react(), tailwindcss()],

  resolve: {
    alias: {
      // import.meta.dirname = folder tempat file ini berada (pengganti __dirname)
      "@": path.resolve(import.meta.dirname, "./src"),
    },
  },

  server: {
    port: 5173,
    strictPort: true,
  },
})