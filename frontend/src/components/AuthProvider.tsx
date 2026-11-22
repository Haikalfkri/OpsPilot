import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react"
import { login as loginRequest } from "@/api/auth"
import { AuthContext, type AuthContextValue } from "@/hooks/auth-context"
import {
  UNAUTHORIZED_EVENT,
  clearReturnTo,
  clearSession,
  clearSessionExpired,
  hasStoredSession,
  markSessionExpired,
  readSession,
  saveSession,
  type StoredSession,
} from "@/lib/auth-storage"
import { queryClient } from "@/lib/query-client"

const currentPath = () => window.location.pathname + window.location.search

export function AuthProvider({ children }: { children: ReactNode }) {
  // Nilai awal dibaca sekali dari sessionStorage, jadi refresh tidak memutus sesi.
  const [session, setSession] = useState<StoredSession | null>(() => readSession())

  // Mengakhiri sesi. expired = true berarti bukan keinginan pengguna (waktu habis atau 401).
  const endSession = useCallback((expired: boolean) => {
    // Hanya sesi yang benar-benar ada yang dianggap "habis". Tanpa pemeriksaan ini,
    // jawaban 401 yang terlambat tiba SESUDAH pengguna menekan Sign out akan
    // memunculkan pesan "session expired" yang keliru.
    const hadSession = hasStoredSession()

    clearSession()

    if (expired && hadSession) {
      markSessionExpired(currentPath()) // ingat halaman yang sedang dibuka
    } else if (!expired) {
      clearSessionExpired() // keluar dengan sengaja: mulai dari awal
      clearReturnTo()
    }

    setSession(null)

    // Buang SEMUA data cache. Tanpa ini, pengguna berikutnya di tab yang sama bisa
    // sekilas melihat data pengguna sebelumnya.
    queryClient.clear()
  }, [])

  const logout = useCallback(() => endSession(false), [endSession])
  const expire = useCallback(() => endSession(true), [endSession])

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

  // Backend menjawab 401 (token ditolak atau kedaluwarsa)
  useEffect(() => {
    window.addEventListener(UNAUTHORIZED_EVENT, expire)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, expire)
  }, [expire])

  // Keluar otomatis tepat saat token habis, walau pengguna tidak berbuat apa-apa
  useEffect(() => {
    if (!session) return

    const millisecondsLeft = Date.parse(session.expiresAtUtc) - Date.now()
    const timer = window.setTimeout(expire, Math.max(millisecondsLeft, 0))

    return () => window.clearTimeout(timer)
  }, [session, expire])

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