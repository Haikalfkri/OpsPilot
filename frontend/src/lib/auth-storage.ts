import type { User } from "@/types/auth"

export interface StoredSession {
  accessToken: string
  expiresAtUtc: string
  user: User
}

const STORAGE_KEY = "opspilot.session"

export const UNAUTHORIZED_EVENT = "opspilot:unauthorized"

export function saveSession(session: StoredSession): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
  } catch {
  }
}

export function clearSession(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // diabaikan
  }
}

export function readSession(): StoredSession | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null

    const session = JSON.parse(raw) as StoredSession
    const expired = Date.parse(session.expiresAtUtc) <= Date.now()

    if (!session.accessToken || expired) {
      clearSession()
      return null
    }

    return session
  } catch {
    clearSession()
    return null
  }
}