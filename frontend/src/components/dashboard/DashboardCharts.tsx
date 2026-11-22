import { Bar, BarChart, CartesianGrid, Legend, Line, LineChart, Tooltip, XAxis, YAxis } from "recharts"
import { formatTrendDate, type CountDatum } from "@/lib/dashboard"
import type { TrendPoint } from "@/types/dashboard"

// Warna dari tema (bukan kode warna tetap), supaya ikut berubah bersama tema
const tick = { fill: "var(--muted-foreground)", fontSize: 12 }

const tooltipStyle = {
  contentStyle: {
    background: "var(--popover)",
    color: "var(--popover-foreground)",
    border: "1px solid var(--border)",
    borderRadius: 8,
  },
  labelStyle: { color: "var(--popover-foreground)" },
}

interface CountBarChartProps {
  data: CountDatum[]
  color: string
  ariaLabel: string // teks pengganti untuk pembaca layar
}

export function CountBarChart({ data, color, ariaLabel }: CountBarChartProps) {
  return (
    // role="img" + aria-label: pembaca layar membaca ringkasannya, bukan potongan SVG
    <div role="img" aria-label={ariaLabel}>
      {/* "responsive": lebar mengikuti kartu, tinggi tetap 240px */}
      <BarChart data={data} responsive width="100%" height={240} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--border)" />
        <XAxis dataKey="label" tick={tick} tickLine={false} axisLine={false} />
        <YAxis allowDecimals={false} width="auto" tick={tick} tickLine={false} axisLine={false} />
        <Tooltip cursor={{ fill: "var(--muted)" }} {...tooltipStyle} />
        <Bar dataKey="count" name="Incidents" fill={color} radius={[4, 4, 0, 0]} />
      </BarChart>
    </div>
  )
}

interface TrendLineChartProps {
  points: TrendPoint[]
  ariaLabel: string
}

export function TrendLineChart({ points, ariaLabel }: TrendLineChartProps) {
  return (
    <div role="img" aria-label={ariaLabel}>
      <LineChart data={points} responsive width="100%" height={280} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--border)" />
        <XAxis
          dataKey="date"
          tick={tick}
          tickLine={false}
          axisLine={false}
          minTickGap={24} // Recharts menjarangkan label sendiri kalau titiknya banyak (30 atau 90 hari)
          tickFormatter={(date: string) => formatTrendDate(date)}
        />
        <YAxis allowDecimals={false} width="auto" tick={tick} tickLine={false} axisLine={false} />
        <Tooltip labelFormatter={(date) => formatTrendDate(String(date))} {...tooltipStyle} />
        <Legend />
        <Line
          type="monotone"
          dataKey="created"
          name="Created"
          stroke="var(--chart-1)"
          strokeWidth={2}
          dot={points.length <= 14} // titik hanya untuk periode pendek, supaya grafik 90 hari tidak ramai
        />
        <Line
          type="monotone"
          dataKey="resolved"
          name="Resolved"
          stroke="var(--chart-2)"
          strokeWidth={2}
          dot={points.length <= 14}
        />
      </LineChart>
    </div>
  )
}