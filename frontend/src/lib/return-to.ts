export function safeReturnTo(path: string | null | undefined): string | null {
  if (!path) return null

  // Harus diawali satu "/" (alamat relatif). "//" dan "/\" dibaca browser sebagai situs lain.
  if (!path.startsWith("/") || path.startsWith("//") || path.startsWith("/\\")) return null

  // Kembali ke halaman login tidak ada gunanya
  if (path === "/login" || path.startsWith("/login?") || path.startsWith("/login/")) return null

  return path
}