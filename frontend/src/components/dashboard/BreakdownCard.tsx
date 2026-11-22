import { CountBarChart } from "@/components/dashboard/DashboardCharts"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { totalOf, type CountDatum } from "@/lib/dashboard"

interface BreakdownCardProps {
  title: string
  data: CountDatum[]
  color: string
  ariaLabel: string
  emptyMessage: string
}

export function BreakdownCard({ title, data, color, ariaLabel, emptyMessage }: BreakdownCardProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>

      <CardContent className="space-y-3">
        {totalOf(data) === 0 ? (
          // Grafik kosong tidak berguna: lebih jelas diberi tahu dengan kalimat
          <p className="py-10 text-center text-sm text-muted-foreground">{emptyMessage}</p>
        ) : (
          <>
            <CountBarChart data={data} color={color} ariaLabel={ariaLabel} />

            {/* Daftar angka: teks pengganti yang terlihat, berguna juga bagi yang tidak melihat grafik */}
            <ul className="flex flex-wrap gap-x-4 gap-y-1 text-sm">
              {data.map((item) => (
                <li key={item.label}>
                  <span className="text-muted-foreground">{item.label}</span>{" "}
                  <span className="font-medium tabular-nums">{item.count}</span>
                </li>
              ))}
            </ul>
          </>
        )}
      </CardContent>
    </Card>
  )
}