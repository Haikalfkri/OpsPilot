// Semua pengaturan aplikasi dibaca dari sini, bukan dari import.meta.env di mana-mana.
const apiUrl = import.meta.env.VITE_API_URL

// Gagal dengan pesan jelas kalau lupa mengisi .env (lebih baik error di awal
// daripada request aneh yang gagal tanpa alasan yang jelas)
if (!apiUrl) {
  throw new Error("VITE_API_URL belum diisi. Salin .env.example menjadi .env dan isi alamat API.")
}

export const config = {
  // Garis miring di ujung dibuang supaya "http://x:5080/" dan "http://x:5080" sama saja
  apiUrl: apiUrl.replace(/\/+$/, ""),
} as const