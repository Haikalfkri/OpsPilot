import { formatDuration } from "@/lib/format"
import {
  DASHBOARD_PERIODS,
  DEFAULT_PERIOD,
  type SeverityCount,
  type StatusCount,
  type TrendPoint,
} from "@/types/dashboard"

// ?days=14 -> 14. Nilai di luar daftar izin (mis. "5", "abc", "7abc") jatuh ke bawaan.
export function parseDashboardDays(params: URLSearchParams): number {
  const raw = params.get("days")

  return DASHBOARD_PERIODS.find((period) => String(period) === raw) ?? DEFAULT_PERIOD
}

// Periode -> URL. Nilai bawaan tidak ditulis, supaya URL tetap pendek.
export function periodSearchParams(days: number): URLSearchParams {
  const params = new URLSearchParams()

  if (days !== DEFAULT_PERIOD) params.set("days", String(days))

  return params
}

export function formatAverageResolution(minutes: number | null): string {
  if (minutes === null) return "—"
  if (minutes < 1) return "<1m"

  return formatDuration(Math.round(minutes))
}

// Backend mengelompokkan per hari UTC, jadi tanggalnya ditampilkan apa adanya sebagai tanggal UTC.
// timeZone: "UTC" mencegah browser menggesernya ke zona waktu lokal (yang bisa mundur satu hari).
export function formatTrendDate(date: string, locale?: string): string {
  return new Intl.DateTimeFormat(locale, { month: "short", day: "numeric", timeZone: "UTC" }).format(
    new Date(`${date}T00:00:00Z`),
  )
}

// Satu batang di grafik
export interface CountDatum {
  label: string
  count: number
}

export const severityData = (items: SeverityCount[]): CountDatum[] =>
  items.map((item) => ({ label: item.severity, count: item.count }))

export const statusData = (items: StatusCount[]): CountDatum[] =>
  items.map((item) => ({ label: item.status, count: item.count }))

export const totalOf = (data: CountDatum[]): number => data.reduce((sum, item) => sum + item.count, 0)

// "Critical 3, High 5, Medium 2, Low 1": dipakai sebagai teks pengganti grafik
export const describeCounts = (data: CountDatum[]): string =>
  data.map((item) => `${item.label} ${item.count}`).join(", ")

export function sumTrend(points: TrendPoint[]): { created: number; resolved: number } {
  return {
    created: points.reduce((sum, point) => sum + point.created, 0),
    resolved: points.reduce((sum, point) => sum + point.resolved, 0),
  }
}

// Waktu hitung TERTUA di antara beberapa angka: umur "terburuk" yang perlu diketahui pengguna
export function oldestGeneratedAt(timestamps: string[]): string | null {
  if (timestamps.length === 0) return null

  return new Date(Math.min(...timestamps.map((value) => Date.parse(value)))).toISOString()
}