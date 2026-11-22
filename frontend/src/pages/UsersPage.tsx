import type { ReactNode } from "react"
import { Link, useSearchParams } from "react-router"
import { getErrorMessage } from "@/api/errors"
import { FlashNotice } from "@/components/FlashNotice"
import { Pagination } from "@/components/Pagination"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { UserFilterBar } from "@/components/users/UserFilterBar"
import { UserTable } from "@/components/users/UserTable"
import { useAuth } from "@/hooks/useAuth"
import { useUsers } from "@/hooks/useUsers"
import { USERS_PAGE_SIZE } from "@/lib/constants"
import { usePageTitle } from "@/hooks/usePageTitle"
import {
  hasActiveUserFilters,
  parseUserFilters,
  toUserApiParams,
  toUserSearchParams,
  type UserFilters,
} from "@/lib/user-filters"

export default function UsersPage() {
  const { user: me } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  usePageTitle("Users")

  // URL -> filter -> parameter API. URL adalah satu-satunya sumber kebenaran.
  const filters = parseUserFilters(searchParams)
  const users = useUsers(toUserApiParams(filters))

  function updateFilters(patch: Partial<UserFilters>) {
    setSearchParams(toUserSearchParams({ ...filters, page: 1, ...patch }))
  }

  let content: ReactNode

  if (users.isPending) {
    content = (
      <div className="space-y-2" aria-busy="true" aria-label="Loading users">
        {Array.from({ length: 5 }, (_, index) => (
          <Skeleton key={index} className="h-10 w-full" />
        ))}
      </div>
    )
  } else if (users.isError) {
    content = (
      <Alert variant="destructive">
        <AlertTitle>Could not load users</AlertTitle>
        <AlertDescription className="space-y-3">
          <p>{getErrorMessage(users.error)}</p>
          <Button variant="outline" size="sm" onClick={() => users.refetch()}>
            Try again
          </Button>
        </AlertDescription>
      </Alert>
    )
  } else if (users.data.items.length === 0) {
    content = (
      <div className="rounded-md border p-8 text-center">
        {users.data.totalCount > 0 ? (
          <div className="space-y-3">
            <p className="font-medium">This page does not exist.</p>
            <Button variant="outline" onClick={() => updateFilters({ page: 1 })}>
              Go to the first page
            </Button>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            {hasActiveUserFilters(filters) ? "No users match your filters." : "No users yet."}
          </p>
        )}
      </div>
    )
  } else {
    content = (
      <>
        <UserTable items={users.data.items} currentUserId={me?.id} dimmed={users.isPlaceholderData} />
        <Pagination
          page={users.data.page}
          totalPages={users.data.totalPages}
          totalCount={users.data.totalCount}
          pageSize={USERS_PAGE_SIZE}
          disabled={users.isPlaceholderData}
          onPageChange={(page) => updateFilters({ page })}
        />
      </>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">Users</h1>
        <Link to="/users/new" className={buttonVariants()}>
          Add user
        </Link>
      </div>

      <FlashNotice />

      <UserFilterBar
        filters={filters}
        canClear={hasActiveUserFilters(filters)}
        onChange={updateFilters}
        onClear={() => setSearchParams(new URLSearchParams())}
      />

      {content}
    </div>
  )
}