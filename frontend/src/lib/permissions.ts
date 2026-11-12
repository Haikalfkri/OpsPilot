import type { UserRole } from "@/types/auth";

// Backend: CanManageIncidents (Admin dan Responder boleh menulis, Viewer hanya membaca)
export function canManageIncidents(role: UserRole): boolean {
    return role === "Admin" || role === "Responder"
}

// Backend: AdminOnly
export function canManageUsers(role: UserRole): boolean {
    return role === "Admin"
}