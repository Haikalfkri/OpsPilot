import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react"
import { login as loginRequest } from "@/api/auth"
import { AuthContext, type AuthContextValue } from "@/hooks/auth-context"
import {
  UNAUTHORIZED_EVENT,
  clearSession,
  readSession,
  saveSession,
  type StoredSession,
} from "@/lib/auth-storage"
import { queryClient } from "@/lib/query-client"

export function AuthProvider({ children }: { children: ReactNode }) {
  // Nilai awal dibaca sekali dari sessionStorage, jadi refresh tidak memutus sesi.
  const [session, setSession] = useState<StoredSession | null>(() => readSession())

  const logout = useCallback(() => {
    clearSession()
    setSession(null)

    // Buang SEMUA data cache. Tanpa ini, pengguna berikutnya di tab yang sama bisa
    // sekilas melihat data pengguna sebelumnya.
    queryClient.clear()
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const response = await loginRequest(email, password)

    const newSession: StoredSession = {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      user: response.user,
    }

    saveSession(newSession)
    setSession(newSession)
  }, [])

  // Backend menjawab 401 (token ditolak atau kedaluwarsa) -> keluar otomatis
  useEffect(() => {
    window.addEventListener(UNAUTHORIZED_EVENT, logout)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, logout)
  }, [logout])

  // Keluar otomatis tepat saat token habis, walau pengguna tidak berbuat apa-apa
  useEffect(() => {
    if (!session) return

    const millisecondsLeft = Date.parse(session.expiresAtUtc) - Date.now()
    const timer = window.setTimeout(logout, Math.max(millisecondsLeft, 0))

    return () => window.clearTimeout(timer)
  }, [session, logout])

  // useMemo/useCallback menjaga agar objek ini tidak berganti setiap render,
  // sehingga komponen yang memakainya tidak ikut digambar ulang tanpa alasan.
  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ?? null,
      isAuthenticated: session !== null,
      login,
      logout,
    }),
    [session, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}