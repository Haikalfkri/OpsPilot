import { http } from "@/api/http"
import type { SlaPolicy } from "@/types/sla"

export function getSlaPolicy(signal?: AbortSignal) {
    return http.get<SlaPolicy>("/api/v1/sla/policy", { signal })
}