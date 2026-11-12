import type { RouteObject } from "react-router"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import { PublicOnlyRoute } from "@/components/auth/PublicOnlyRoute"
import { RequireRole } from "@/components/auth/RequireRole"
import { AppLayout } from "@/components/layout/AppLayout"
import { canManageUsers } from "@/lib/permissions"
import DashboardPage from "@/pages/DashboardPage"
import IncidentsPage from "@/pages/IncidentsPage"
import LoginPage from "@/pages/LoginPage"
import NotFoundPage from "@/pages/NotFoundPage"
import NotificationsPage from "@/pages/NotificationsPage"
import RouteErrorPage from "@/pages/RouteErrorPage"
import UsersPage from "@/pages/UsersPage"

export const routes: RouteObject[] = [
  {
    // Route induk tanpa alamat: hanya untuk memasang halaman error bagi semua yang di bawahnya
    errorElement: <RouteErrorPage />,
    children: [
      {
        element: <PublicOnlyRoute />,
        children: [{ path: "/login", element: <LoginPage /> }],
      },
      {
        element: <ProtectedRoute />,
        children: [
          {
            element: <AppLayout />,
            children: [
              { index: true, element: <DashboardPage /> }, // alamat "/"
              { path: "incidents", element: <IncidentsPage /> },
              { path: "notifications", element: <NotificationsPage /> },
              {
                element: <RequireRole allow={canManageUsers} />,
                children: [{ path: "users", element: <UsersPage /> }],
              },
              // "*" cocok dengan alamat apa pun yang belum ada. Di dalam layout,
              // supaya pengguna yang tersasar tetap punya menu.
              { path: "*", element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
]