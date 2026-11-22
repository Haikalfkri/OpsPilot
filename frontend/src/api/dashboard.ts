import { http } from "@/api/http"
import type { DashboardBreakdown, DashboardSummary, DashboardTrends } from "@/types/dashboard"

export function getDashboardSummary(days: number, signal?: AbortSignal) {
  return http.get<DashboardSummary>("/api/v1/dashboard/summary", { query: { days }, signal })
}

export function getDashboardBreakdown(days: number, signal?: AbortSignal) {
  return http.get<DashboardBreakdown>("/api/v1/dashboard/incident-breakdown", { query: { days }, signal })
}

export function getDashboardTrends(days: number, signal?: AbortSignal) {
  return http.get<DashboardTrends>("/api/v1/dashboard/trends", { query: { days }, signal })
}