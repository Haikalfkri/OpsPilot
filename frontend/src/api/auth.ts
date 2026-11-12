import { http } from "@/api/http"
import type { LoginResponse, User } from "@/types/auth"

export function login(email: string, password: string): Promise<LoginResponse> {
  return http.post<LoginResponse>("/api/v1/auth/login", { email, password }, { skipUnauthorizedHandler: true })
}

export function getMe(signal?: AbortSignal): Promise<User> {
  return http.get<User>("/api/v1/auth/me", { signal })
}