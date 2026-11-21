import { http } from "@/api/http"
import type { PagedResult } from "@/types/api"
import type { TeamMember } from "@/types/team"

export function listTeamMembers(signal?: AbortSignal) {
    return http.get<PagedResult<TeamMember>>("/api/v1/teams/current/users", {
        query: { pageSize: 100 },
        signal,
    })
}