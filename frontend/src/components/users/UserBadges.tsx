import { Badge } from "@/components/ui/badge"
import type { UserRole } from "@/types/auth"

export function RoleBadge({ role }: { role: UserRole }) {
  return <Badge variant={role === "Admin" ? "default" : "secondary"}>{role}</Badge>
}

export function UserStatusBadge({ isActive }: { isActive: boolean }) {
  return isActive ? (
    <Badge variant="outline">Active</Badge>
  ) : (
    <Badge variant="secondary" className="text-muted-foreground">
      Inactive
    </Badge>
  )
}