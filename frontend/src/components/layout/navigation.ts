import { canManageUsers } from "@/lib/permissions";
import type { UserRole } from "@/types/auth";

export interface NavItem {
    to: string,
    label: string,
    end?: boolean
    visibleTo?: (role: UserRole) => boolean // kosong = semua role melihatnya
}

const navItems: NavItem[] = [
    { to: "/", label: "Dashboard", end: true },
    { to: "/incidents", label: "Incidents" },
    { to: "/notifications", label: "Notifications" },
    { to: "/users", label: "Users", visibleTo: canManageUsers },
]

// menu yang boleh diliat role ini
export function getNavItems(role: UserRole): NavItem[] {
    return navItems.filter((item) => item.visibleTo?.(role) ?? true)
}