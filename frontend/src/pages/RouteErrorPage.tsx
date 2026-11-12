// Dipakai React Router kalau terjadi error tak terduga saat menggambar halaman
import { isRouteErrorResponse, useRouteError } from "react-router"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"

export default function RouteErrorPage() {
  const error = useRouteError()

  const message = isRouteErrorResponse(error)
    ? `${error.status} ${error.statusText}`
    : error instanceof Error
      ? error.message
      : "Unknown error"

  return (
    <main className="flex min-h-screen items-center justify-center bg-background p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Something went wrong</CardTitle>
          <CardDescription>The page could not be displayed.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-destructive">{message}</p>
          <Button onClick={() => window.location.assign("/")}>Reload</Button>
        </CardContent>
      </Card>
    </main>
  )
}