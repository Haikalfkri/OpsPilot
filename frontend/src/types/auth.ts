export const ROLES = ["Admin", "Responder", "Viewer"] as const
export type UserRole = (typeof ROLES)[number]

export interface User {
  id: string
  displayName: string
  email: string
  role: UserRole
  teamId: string
  isActive: boolean
}

// Jawaban POST /api/v1/auth/login
export interface LoginResponse {
  accessToken: string
  tokenType: string // selalu "Bearer"
  expiresAtUtc: string // tanggal ISO 8601 (UTC)
  user: User
}