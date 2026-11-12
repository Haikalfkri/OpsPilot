import { getErrorMessage } from "@/api/errors"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { useAuth } from "@/hooks/useAuth"
import { useMe } from "@/hooks/useMe"

export default function HomePage() {
  const { user, logout } = useAuth()

  // Memanggil GET /auth/me DENGAN token. Kalau berhasil, berarti token terkirim dan diterima.
  const me = useMe()

  return (
    <main className="flex min-h-screen items-center justify-center bg-background p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Welcome, {user?.displayName}</CardTitle>
          <CardDescription>You are signed in. This page is temporary.</CardDescription>
        </CardHeader>

        <CardContent className="space-y-4">
          {me.isPending && <p className="text-sm">Asking the server who you are…</p>}

          {me.isError && <p className="text-sm text-destructive">{getErrorMessage(me.error)}</p>}

          {me.data && (
            <dl className="space-y-2 text-sm">
              <div className="flex items-center justify-between">
                <dt className="text-muted-foreground">Email</dt>
                <dd>{me.data.email}</dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-muted-foreground">Role</dt>
                <dd>
                  <Badge variant="secondary">{me.data.role}</Badge>
                </dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-muted-foreground">Team</dt>
                <dd className="font-mono text-xs">{me.data.teamId}</dd>
              </div>
            </dl>
          )}

          <Button variant="outline" onClick={logout}>
            Sign out
          </Button>
        </CardContent>
      </Card>
    </main>
  )
}