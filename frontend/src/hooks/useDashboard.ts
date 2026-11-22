import { keepPreviousData, useQuery } from "@tanstack/react-query"
import { getDashboardBreakdown, getDashboardSummary, getDashboardTrends } from "@/api/dashboard"
import { dashboardKeys } from "@/api/query-keys"
import { DASHBOARD_POLL_MS } from "@/lib/constants"

const shared = {
  placeholderData: keepPreviousData,
  refetchInterval: DASHBOARD_POLL_MS,
} as const

export function useDashboardSummary(days: number) {
  return useQuery({
    queryKey: dashboardKeys.summary(days),
    queryFn: ({ signal }) => getDashboardSummary(days, signal),
    ...shared,
  })
}

export function useDashboardBreakdown(days: number) {
  return useQuery({
    queryKey: dashboardKeys.breakdown(days),
    queryFn: ({ signal }) => getDashboardBreakdown(days, signal),
    ...shared,
  })
}

export function useDashboardTrends(days: number) {
  return useQuery({
    queryKey: dashboardKeys.trends(days),
    queryFn: ({ signal }) => getDashboardTrends(days, signal),
    ...shared,
  })
}