import { http } from "@/api/http"
import type { PagedResult } from "@/types/api"
import type { User } from "@/types/auth"
import type { CreateUserRequest, ListUsersParams, UpdateUserRequest } from "@/types/user"

const base = (id: string) => `/api/v1/users/${encodeURIComponent(id)}`

export function listUsers(params: ListUsersParams, signal?: AbortSignal) {
  return http.get<PagedResult<User>>("/api/v1/users", { query: params, signal })
}

export function getUser(id: string, signal?: AbortSignal) {
  return http.get<User>(base(id), { signal })
}

export function createUser(body: CreateUserRequest) {
  return http.post<User>("/api/v1/users", body)
}

export function updateUser(id: string, body: UpdateUserRequest) {
  return http.patch<User>(base(id), body)
}