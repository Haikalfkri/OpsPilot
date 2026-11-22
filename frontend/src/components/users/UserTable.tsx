import { Link } from "react-router"
import { RoleBadge, UserStatusBadge } from "@/components/users/UserBadges"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { cn } from "@/lib/utils"
import type { User } from "@/types/auth"

interface UserTableProps {
  items: User[]
  currentUserId?: string
  dimmed: boolean // halaman berikutnya sedang dimuat: tabel lama dipudarkan
}

export function UserTable({ items, currentUserId, dimmed }: UserTableProps) {
  return (
    <div className={cn("rounded-md border transition-opacity", dimmed && "opacity-60")}>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Email</TableHead>
            <TableHead>Role</TableHead>
            <TableHead>Status</TableHead>
          </TableRow>
        </TableHeader>

        <TableBody>
          {items.map((user) => (
            <TableRow key={user.id}>
              <TableCell>
                <Link to={`/users/${user.id}`} className="font-medium hover:underline">
                  {user.displayName}
                </Link>
                {user.id === currentUserId && <span className="ml-2 text-xs text-muted-foreground">(you)</span>}
              </TableCell>
              <TableCell>{user.email}</TableCell>
              <TableCell>
                <RoleBadge role={user.role} />
              </TableCell>
              <TableCell>
                <UserStatusBadge isActive={user.isActive} />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}