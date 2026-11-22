import type { UserRole } from "@/types/auth"

export type ListUsersParams = {
  page: number
  pageSize: number
  search?: string
  role?: UserRole
  isActive?: boolean
}

export interface CreateUserRequest {
  email: string
  displayName: string
  password: string
  role: UserRole
}

export interface UpdateUserRequest {
  displayName?: string
  role?: UserRole
  isActive?: boolean
}