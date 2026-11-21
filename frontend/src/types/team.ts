import type { UserRole } from "@/types/auth"

export interface TeamMember {
    id: string
    displayName: string
    role: UserRole
}