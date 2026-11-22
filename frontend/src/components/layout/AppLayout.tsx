import { useEffect, useRef } from "react"
import { Outlet, useLocation } from "react-router"
import { MainNav } from "@/components/layout/MainNav"
import { NotificationBell } from "@/components/layout/NotificationBell"
import { getNavItems } from "@/components/layout/navigation"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { useAuth } from "@/hooks/useAuth"

export function AppLayout() {
  const { user, logout } = useAuth()
  const { pathname } = useLocation()
  const mainRef = useRef<HTMLElement>(null)
  const previousPath = useRef(pathname)

  useEffect(() => {
    if (previousPath.current === pathname) return

    previousPath.current = pathname
    mainRef.current?.focus()
  }, [pathname])

  // Tidak mungkin terjadi di dalam ProtectedRoute; baris ini hanya menenangkan TypeScript
  if (!user) return null

  const items = getNavItems(user.role)

  return (
    <div className="flex min-h-screen bg-background">
      {/* Tautan lompat: tersembunyi sampai menerima fokus (tombol Tab pertama).
          Pengguna papan ketik bisa melewati menu dan langsung ke isi halaman. */}
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded-md focus:bg-background focus:px-3 focus:py-2 focus:text-sm focus:shadow focus:ring-2 focus:ring-ring"
      >
        Skip to content
      </a>

      {/* Menu samping: hanya di layar lebar (md ke atas, 768px) */}
      <aside className="hidden w-56 shrink-0 border-r md:flex md:flex-col">
        <div className="px-4 py-4 text-lg font-semibold">OpsPilot</div>
        <MainNav items={items} orientation="vertical" />
      </aside>

      {/* min-w-0 mencegah konten lebar (tabel) mendorong layout melewati layar */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between gap-4 border-b px-4 py-3">
          <span className="font-semibold md:hidden">OpsPilot</span>

          <div className="ml-auto flex items-center gap-3">
            <NotificationBell />
            <div className="hidden text-right text-sm leading-tight sm:block">
              <div className="font-medium">{user.displayName}</div>
              <div className="text-muted-foreground">{user.email}</div>
            </div>
            <Badge variant="secondary">{user.role}</Badge>
            <Button variant="outline" size="sm" onClick={logout}>
              Sign out
            </Button>
          </div>
        </header>

        {/* Menu untuk layar sempit: baris mendatar di bawah bar atas */}
        <div className="border-b md:hidden">
          <MainNav items={items} orientation="horizontal" />
        </div>

        {/* tabIndex={-1}: bisa menerima fokus lewat kode (tidak lewat Tab). outline-none: tanpa garis
            fokus di seluruh area, karena yang difokuskan adalah wadah, bukan kontrol. */}
        <main id="main-content" ref={mainRef} tabIndex={-1} className="flex-1 p-4 outline-none md:p-6">
          {/* Di sinilah halaman anak digambar */}
          <Outlet />
        </main>
      </div>
    </div>
  )
}