import { Link } from "react-router"
import { buttonVariants } from "@/components/ui/button"
import { useUnreadCount } from "@/hooks/useNotifications"
import { formatUnreadBadge } from "@/lib/notifications"
import { cn } from "@/lib/utils"

// Ikon lonceng sebagai SVG biasa (tanpa pustaka ikon)
function BellIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="size-5"
    >
      <path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" />
      <path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" />
    </svg>
  )
}

export function NotificationBell() {
  const unread = useUnreadCount()

  // Selagi memuat atau gagal: tidak ada angka. Bar atas tidak pernah menampilkan error polling.
  const count = unread.data ?? 0
  const badge = formatUnreadBadge(count)

  return (
    <Link
      to="/notifications"
      // Pembaca layar tidak melihat angka di lencana, jadi labelnya memuat jumlahnya
      aria-label={count > 0 ? `Notifications, ${count} unread` : "Notifications"}
      className={cn(buttonVariants({ variant: "ghost", size: "icon" }), "relative")}
    >
      <BellIcon />

      {badge && (
        <span
          aria-hidden="true"
          className="absolute -right-1 -top-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-destructive px-1 text-[11px] font-medium leading-none text-white"
        >
          {badge}
        </span>
      )}
    </Link>
  )
}