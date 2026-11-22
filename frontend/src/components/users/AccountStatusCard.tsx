import { useState } from "react"
import { getErrorMessage } from "@/api/errors"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { UserStatusBadge } from "@/components/users/UserBadges"
import { useUpdateUser } from "@/hooks/useUserMutations"
import type { User } from "@/types/auth"

interface AccountStatusCardProps {
  user: User
  isSelf: boolean
}

export function AccountStatusCard({ user, isSelf }: AccountStatusCardProps) {
  const update = useUpdateUser(user.id)
  const [confirming, setConfirming] = useState(false)

  function setActive(isActive: boolean) {
    update.mutate({ isActive }, { onSettled: () => setConfirming(false) })
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Account status</CardTitle>
      </CardHeader>

      <CardContent className="space-y-4">
        <UserStatusBadge isActive={user.isActive} />

        {user.isActive ? (
          <p className="text-sm text-muted-foreground">This user can sign in and work on incidents.</p>
        ) : (
          <p className="text-sm text-muted-foreground">
            This user cannot sign in. Reactivating restores access with the same password and role.
          </p>
        )}

        {user.isActive && confirming && (
          <div role="group" aria-label="Confirm deactivation" className="space-y-3 rounded-md border p-3">
            <p className="text-sm">
              {isSelf
                ? "Deactivate your own account? You will be signed out immediately and cannot sign back in."
                : "Deactivate this account? The user is signed out on their next request and cannot sign in until reactivated."}
            </p>
            <div className="flex gap-2">
              <Button size="sm" variant="destructive" disabled={update.isPending} onClick={() => setActive(false)}>
                {update.isPending ? "Saving…" : "Confirm"}
              </Button>
              <Button size="sm" variant="outline" disabled={update.isPending} onClick={() => setConfirming(false)}>
                Cancel
              </Button>
            </div>
          </div>
        )}

        {user.isActive && !confirming && (
          <Button variant="outline" size="sm" onClick={() => setConfirming(true)}>
            Deactivate account
          </Button>
        )}

        {/* Mengaktifkan kembali tidak merusak apa pun, jadi tanpa konfirmasi */}
        {!user.isActive && (
          <Button size="sm" disabled={update.isPending} onClick={() => setActive(true)}>
            {update.isPending ? "Saving…" : "Reactivate account"}
          </Button>
        )}

        {update.isError && (
          <Alert variant="destructive">
            <AlertDescription>{getErrorMessage(update.error)}</AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  )
}