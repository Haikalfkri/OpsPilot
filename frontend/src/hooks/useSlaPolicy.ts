import { useQuery } from "@tanstack/react-query"
import { getSlaPolicy } from "@/api/sla"
import { slaKeys } from "@/api/query-keys"

export function useSlaPolicy() {
    return useQuery({
        queryKey: slaKeys.policy,
        queryFn: ({ signal }) => getSlaPolicy(signal),
        staleTime: 60 * 60_000,
    })
}