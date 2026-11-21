import { useQuery } from "@tanstack/react-query"
import { listTeamMembers } from "@/api/team"
import { teamKeys } from "@/api/query-keys"

export function useTeamMembers() {
    return useQuery({
        queryKey: teamKeys.members,
        queryFn: ({ signal }) => listTeamMembers(signal),
        staleTime: 5 * 60_000,
    })
}