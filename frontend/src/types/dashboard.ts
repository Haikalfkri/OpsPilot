import type { IncidentSeverity, IncidentStatus } from "@/types/incident"

export const DASHBOARD_PERIODS = [7, 14, 30, 90] as const
export const DEFAULT_PERIOD = 7

export interface DashboardPeriodInfo {
    days: number
    fromUtc: string
    toUtc: string
}

export interface DashboardSummary {
  period: DashboardPeriodInfo
  openIncidents: number
  criticalHighOpenIncidents: number
  slaBreachedIncidents: number 
  resolvedInPeriod: number
  averageResolutionMinutes: number | null
  generatedAtUtc: string 
}

export interface SeverityCount {
    severity: IncidentSeverity
    count: number
}

export interface StatusCount {
    status: IncidentStatus
    count: number
}

export interface DashboardBreakdown {
  period: DashboardPeriodInfo
  bySeverity: SeverityCount[]
  byStatus: StatusCount[]
  generatedAtUtc: string
}

export interface TrendPoint {
  date: string
  created: number
  resolved: number
}

export interface DashboardTrends {
  period: DashboardPeriodInfo
  points: TrendPoint[]
  generatedAtUtc: string
}