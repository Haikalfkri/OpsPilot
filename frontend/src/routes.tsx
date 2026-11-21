import type { RouteObject } from "react-router"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import { PublicOnlyRoute } from "@/components/auth/PublicOnlyRoute"
import { RequireRole } from "@/components/auth/RequireRole"
import { AppLayout } from "@/components/layout/AppLayout"
import { canManageIncidents, canManageUsers } from "@/lib/permissions"
import DashboardPage from "@/pages/DashboardPage"
import IncidentsPage from "@/pages/IncidentsPage"
import LoginPage from "@/pages/LoginPage"
import NotFoundPage from "@/pages/NotFoundPage"
import NotificationsPage from "@/pages/NotificationsPage"
import RouteErrorPage from "@/pages/RouteErrorPage"
import UsersPage from "@/pages/UsersPage"
import IncidentDetailPage from "@/pages/IncidentDetailPage"
import NewIncidentPage from "@/pages/NewIncidentPage"

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
              {
                element: <RequireRole allow={canManageIncidents} />,
                children: [{ path: "incidents/new", element: <NewIncidentPage /> }],
              },
              { path: "incidents/:id", element: <IncidentDetailPage /> },
              { path: "notifications", element: <NotificationsPage /> },
              {
                element: <RequireRole allow={canManageUsers} />,
                children: [{ path: "users", element: <UsersPage /> }],
              },
              { path: "*", element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
]