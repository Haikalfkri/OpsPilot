import { Card, CardContent, CardDescription, CardHeader } from "@/components/ui/card"
import { cn } from "@/lib/utils"

interface StatCardProps {
  title: string
  value: string
  hint: string
  tone?: "default" | "danger" // danger = angka merah (mis. pelanggaran SLA)
}

export function StatCard({ title, value, hint, tone = "default" }: StatCardProps) {
  return (
    // role="group" + aria-label: pembaca layar (dan tes) menemukan kartu dari judulnya
    <Card role="group" aria-label={title}>
      <CardHeader>
        <CardDescription>{title}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-1">
        <p className={cn("text-3xl font-semibold tabular-nums", tone === "danger" && "text-destructive")}>{value}</p>
        <p className="text-xs text-muted-foreground">{hint}</p>
      </CardContent>
    </Card>
  )
}