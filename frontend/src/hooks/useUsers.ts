import { keepPreviousData, useQuery } from "@tanstack/react-query"
import { getUser, listUsers } from "@/api/users"
import { userKeys } from "@/api/query-keys"
import type { ListUsersParams } from "@/types/user"

export function useUsers(params: ListUsersParams) {
  return useQuery({
    queryKey: userKeys.list(params),
    queryFn: ({ signal }) => listUsers(params, signal),
    placeholderData: keepPreviousData,
  })
}

export function useUser(id: string) {
  return useQuery({
    queryKey: userKeys.detail(id),
    queryFn: ({ signal }) => getUser(id, signal),
  })
}