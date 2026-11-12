import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import { QueryClientProvider } from "@tanstack/react-query"
import { ReactQueryDevtools } from "@tanstack/react-query-devtools"
import { createBrowserRouter } from "react-router"
import { RouterProvider } from "react-router/dom"
import "./index.css"
import { configureHttp } from "@/api/http"
import { AuthProvider } from "@/components/AuthProvider"
import { UNAUTHORIZED_EVENT, readSession } from "@/lib/auth-storage"
import { queryClient } from "@/lib/query-client"
import { routes } from "@/routes"

configureHttp({
  getToken: () => readSession()?.accessToken ?? null,
  onUnauthorized: () => window.dispatchEvent(new Event(UNAUTHORIZED_EVENT)),
})

const router = createBrowserRouter(routes)

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      {/* AuthProvider harus DI LUAR router, karena penjaga rute memakai useAuth() */}
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  </StrictMode>
)