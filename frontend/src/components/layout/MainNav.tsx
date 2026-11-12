import { NavLink } from "react-router"
import type { NavItem } from "@/components/layout/navigation"
import { cn } from "@/lib/utils"

interface MainNavProps {
  items: NavItem[]
  orientation: "vertical" | "horizontal"
}

export function MainNav({ items, orientation }: MainNavProps) {
  return (
    <nav
      aria-label="Main"
      className={cn("flex gap-1 px-2", orientation === "vertical" ? "flex-col" : "overflow-x-auto py-2")}
    >
      {items.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.end}
          // className boleh berupa fungsi: React Router memberi tahu apakah tautan ini aktif
          className={({ isActive }) =>
            cn(
              "whitespace-nowrap rounded-md px-3 py-2 text-sm transition-colors hover:bg-muted",
              isActive ? "bg-muted font-medium text-foreground" : "text-muted-foreground",
            )
          }
        >
          {item.label}
        </NavLink>
      ))}
    </nav>
  )
}