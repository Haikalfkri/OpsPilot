import { useEffect, useState } from "react"
import { Navigate, Outlet } from "react-router"
import { useAuth } from "@/hooks/useAuth"
import { clearReturnTo, readReturnTo } from "@/lib/auth-storage"
import { safeReturnTo } from "@/lib/return-to"

// Sudah login -> pergi. Tujuannya: halaman tempat sesi habis tadi (kalau ada), selain itu "/".
export function PublicOnlyRoute() {
  const { isAuthenticated } = useAuth()

  // Dibaca sekali saat komponen muncul, yaitu sebelum pengguna login
  const [destination] = useState(() => safeReturnTo(readReturnTo()) ?? "/")

  // Setelah login berhasil, catatan tujuan dibuang supaya tidak terpakai lagi
  useEffect(() => {
    if (isAuthenticated) clearReturnTo()
  }, [isAuthenticated])

  return isAuthenticated ? <Navigate to={destination} replace /> : <Outlet />
}