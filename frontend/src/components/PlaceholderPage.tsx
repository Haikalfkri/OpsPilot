import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"

interface PlaceholderPageProps {
  title: string
  description: string
  comingIn: string
}

// Halaman sementara untuk fitur yang belum dibangun
export function PlaceholderPage({ title, description, comingIn }: PlaceholderPageProps) {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">{title}</h1>

      <Card>
        <CardHeader>
          <CardTitle>Coming soon</CardTitle>
          <CardDescription>{description}</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">Built in {comingIn}.</p>
        </CardContent>
      </Card>
    </div>
  )
}