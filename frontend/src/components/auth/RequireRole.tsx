import { Outlet } from "react-router"
import { useAuth } from "@/hooks/useAuth"
import ForbiddenPage from "@/pages/ForbiddenPage"
import type { UserRole } from "@/types/auth"


export function RequireRole({ allow }: { allow: (role: UserRole) => boolean }) {
  const { user } = useAuth()

  if (!user || !allow(user.role)) return <ForbiddenPage />

  return <Outlet />
}