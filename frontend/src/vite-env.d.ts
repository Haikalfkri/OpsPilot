/// <reference types="vite/client" />

// Daftar variabel lingkungan yang kita pakai, supaya TypeScript tahu namanya
interface ImportMetaEnv {
  readonly VITE_API_URL: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}