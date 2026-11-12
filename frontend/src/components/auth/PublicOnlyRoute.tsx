import { Navigate, Outlet } from "react-router"
import { useAuth } from "@/hooks/useAuth"

export function PublicOnlyRoute() {
    const { isAuthenticated  } = useAuth();

    return isAuthenticated ? <Navigate to="/" replace /> : <Outlet />
}