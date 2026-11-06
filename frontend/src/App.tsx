import { getErrorMessage } from "@/api/errors"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { useHealth } from "@/hooks/useHealth"
import { config } from "@/lib/config"

function StatusBadge({ status }: { status: string }) {
  return <Badge variant={status === "Healthy" ? "secondary" : "destructive"}>{status}</Badge>
}

export default function App() {
  const health = useHealth()

  return (
    <main className="flex min-h-screen items-center justify-center bg-background p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>OpsPilot</CardTitle>
          <CardDescription>API connection check</CardDescription>
        </CardHeader>

        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground">
            API: <code>{config.apiUrl}</code>
          </p>

          {health.isPending && <p className="text-sm">Checking…</p>}

          {health.isError && <p className="text-sm text-destructive">{getErrorMessage(health.error)}</p>}

          {health.data && (
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Overall</span>
                <StatusBadge status={health.data.status} />
              </div>

              <ul className="space-y-1">
                {health.data.checks.map((check) => (
                  <li key={check.name} className="flex items-center justify-between text-sm">
                    <span className="text-muted-foreground">{check.name}</span>
                    <StatusBadge status={check.status} />
                  </li>
                ))}
              </ul>
            </div>
          )}

          <Button onClick={() => health.refetch()} disabled={health.isFetching}>
            {health.isFetching ? "Checking…" : "Check again"}
          </Button>
        </CardContent>
      </Card>
    </main>
  )
}