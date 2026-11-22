import { Link, useParams } from "react-router"
import { getErrorMessage, isApiError } from "@/api/errors"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { AccountStatusCard } from "@/components/users/AccountStatusCard"
import { EditUserForm } from "@/components/users/EditUserForm"
import { RoleBadge, UserStatusBadge } from "@/components/users/UserBadges"
import { useAuth } from "@/hooks/useAuth"
import { useUser } from "@/hooks/useUsers"

function BackLink() {
  return (
    <Link to="/users" className="text-sm text-muted-foreground hover:underline">
      ← Back to users
    </Link>
  )
}

export default function UserDetailPage() {
  const { id = "" } = useParams<{ id: string }>()
  const { user: me } = useAuth()
  const user = useUser(id)

  if (user.isPending) {
    return (
      <div className="space-y-4" aria-busy="true" aria-label="Loading user">
        <Skeleton className="h-6 w-40" />
        <Skeleton className="h-64 w-full max-w-xl" />
      </div>
    )
  }

  if (user.isError) {
    // 404 = tidak ada, atau milik tim lain (backend sengaja tidak membedakan keduanya)
    const notFound = isApiError(user.error) && user.error.status === 404

    return (
      <div className="space-y-4">
        <BackLink />
        {notFound ? (
          <div className="space-y-3">
            <h1 className="text-2xl font-semibold">User not found</h1>
            <p className="text-sm text-muted-foreground">It does not exist, or it belongs to another team.</p>
            <Link to="/users" className={buttonVariants({ variant: "outline" })}>
              Back to users
            </Link>
          </div>
        ) : (
          <Alert variant="destructive">
            <AlertTitle>Could not load the user</AlertTitle>
            <AlertDescription className="space-y-3">
              <p>{getErrorMessage(user.error)}</p>
              <Button variant="outline" size="sm" onClick={() => user.refetch()}>
                Try again
              </Button>
            </AlertDescription>
          </Alert>
        )}
      </div>
    )
  }

  const data = user.data
  const isSelf = me?.id === data.id

  return (
    <div className="max-w-xl space-y-6">
      <BackLink />

      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-semibold">{data.displayName}</h1>
        <RoleBadge role={data.role} />
        <UserStatusBadge isActive={data.isActive} />
        {isSelf && <span className="text-sm text-muted-foreground">(you)</span>}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Profile</CardTitle>
        </CardHeader>
        <CardContent>
          {/* key: kalau data dari server berganti (mis. diubah dari tab lain), form dimulai ulang dengan nilai terbaru */}
          <EditUserForm key={`${data.displayName}|${data.role}`} user={data} isSelf={isSelf} />
        </CardContent>
      </Card>

      <AccountStatusCard user={data} isSelf={isSelf} />
    </div>
  )
}