import { USERS_PAGE_SIZE } from "@/lib/constants"
import { ROLES, type UserRole } from "@/types/auth"
import type { ListUsersParams } from "@/types/user"

export type UserStatusFilter = "all" | "active" | "inactive"

export interface UserFilters {
  page: number
  role?: UserRole
  status: UserStatusFilter
  search: string
}

const STATUSES: readonly UserStatusFilter[] = ["active", "inactive"]

export function parseUserFilters(params: URLSearchParams): UserFilters {
  const page = Number.parseInt(params.get("page") ?? "", 10)

  return {
    page: Number.isInteger(page) && page >= 1 ? page : 1,
    role: ROLES.find((role) => role === params.get("role")),
    status: STATUSES.find((status) => status === params.get("status")) ?? "all",
    search: (params.get("q") ?? "").trim().slice(0, 100),
  }
}

export function toUserSearchParams(filters: UserFilters): URLSearchParams {
  const params = new URLSearchParams()

  if (filters.role) params.set("role", filters.role)
  if (filters.status !== "all") params.set("status", filters.status)
  if (filters.search) params.set("q", filters.search)
  if (filters.page > 1) params.set("page", String(filters.page))

  return params
}

export function toUserApiParams(filters: UserFilters): ListUsersParams {
  return {
    page: filters.page,
    pageSize: USERS_PAGE_SIZE,
    search: filters.search || undefined,
    role: filters.role,
    isActive: filters.status === "all" ? undefined : filters.status === "active",
  }
}

export function hasActiveUserFilters(filters: UserFilters): boolean {
  return Boolean(filters.role || filters.status !== "all" || filters.search)
}