import { useMutation, useQueryClient, type QueryClient } from "@tanstack/react-query"
import { createUser, updateUser } from "@/api/users"
import { teamKeys, userKeys } from "@/api/query-keys"
import type { CreateUserRequest, UpdateUserRequest } from "@/types/user"

// Setelah user berubah: daftar user usang, dan dropdown assignee juga
// (ia berisi user aktif dan role mereka, dan di-cache 5 menit).
function refreshUsers(queryClient: QueryClient) {
  void queryClient.invalidateQueries({ queryKey: userKeys.lists() })
  void queryClient.invalidateQueries({ queryKey: teamKeys.members })
}

export function useCreateUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: CreateUserRequest) => createUser(body),
    onSuccess: (user) => {
      queryClient.setQueryData(userKeys.detail(user.id), user)
      refreshUsers(queryClient)
    },
  })
}

export function useUpdateUser(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: UpdateUserRequest) => updateUser(id, body),
    onSuccess: (user) => {
      queryClient.setQueryData(userKeys.detail(id), user)
      refreshUsers(queryClient)
    },
    onError: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.detail(id), exact: true })
    },
  })
}