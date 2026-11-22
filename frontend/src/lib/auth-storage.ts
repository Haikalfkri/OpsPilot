import type { User } from "@/types/auth"

export interface StoredSession {
  accessToken: string
  expiresAtUtc: string
  user: User
}

const STORAGE_KEY = "opspilot.session"
const EXPIRED_KEY = "opspilot.sessionExpired" // penanda: pengguna dilempar karena sesi habis
const RETURN_KEY = "opspilot.returnTo" // halaman yang sedang dibuka saat sesi habis

// Nama kejadian yang dikirim saat backend menjawab 401 (sesi habis atau token ditolak)
export const UNAUTHORIZED_EVENT = "opspilot:unauthorized"

// sessionStorage bisa gagal (mode privat, penuh), jadi semua aksesnya lewat tiga fungsi ini
function get(key: string): string | null {
  try {
    return sessionStorage.getItem(key)
  } catch {
    return null
  }
}

function set(key: string, value: string): void {
  try {
    sessionStorage.setItem(key, value)
  } catch {
    // Sesi tetap hidup di memori, hanya tidak bertahan setelah refresh
  }
}

function remove(key: string): void {
  try {
    sessionStorage.removeItem(key)
  } catch {
    // diabaikan
  }
}

export function saveSession(session: StoredSession): void {
  set(STORAGE_KEY, JSON.stringify(session))
}

export function clearSession(): void {
  remove(STORAGE_KEY)
}

// Ada sesi tersimpan? (tanpa memeriksa kedaluwarsa dan tanpa menghapus apa pun)
export function hasStoredSession(): boolean {
  return get(STORAGE_KEY) !== null
}

// Membaca sesi. Mengembalikan null kalau tidak ada, rusak, atau token sudah kedaluwarsa.
export function readSession(): StoredSession | null {
  const raw = get(STORAGE_KEY)
  if (!raw) return null

  try {
    const session = JSON.parse(raw) as StoredSession
    const expiresAt = Date.parse(session.expiresAtUtc)

    // Data rusak: dibuang
    if (!session.accessToken || Number.isNaN(expiresAt)) {
      clearSession()
      return null
    }

    // Kedaluwarsa: tidak dipakai (tidak pernah ikut terkirim), tetapi sengaja TIDAK dihapus di sini.
    // Yang menghapusnya adalah proses "sesi habis" di AuthProvider, supaya pengguna diberi tahu.
    if (expiresAt <= Date.now()) return null

    return session
  } catch {
    clearSession()
    return null
  }
}

// ---------- Sesi habis: pesan di halaman login dan tujuan kembali ----------

export function markSessionExpired(returnTo: string): void {
  set(EXPIRED_KEY, "1")
  set(RETURN_KEY, returnTo)
}

export const wasSessionExpired = (): boolean => get(EXPIRED_KEY) !== null
export const clearSessionExpired = (): void => remove(EXPIRED_KEY)
export const readReturnTo = (): string | null => get(RETURN_KEY)
export const clearReturnTo = (): void => remove(RETURN_KEY)