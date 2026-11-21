import type { IncidentSeverity } from "@/types/incident"

export interface SlaTarget {
    severity: IncidentSeverity
    responseMinutes: number
    resolutionMinutes: number
}

export interface SlaPolicy {
    targets: SlaTarget[]
    note: string
}