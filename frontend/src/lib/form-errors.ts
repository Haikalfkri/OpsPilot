    import type { FieldValues, Path, UseFormSetError } from "react-hook-form"
import { isApiError } from "@/api/errors"

export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly Path<T>[],
): boolean {
  if (!isApiError(error)) return false

  let applied = false

  for (const [name, messages] of Object.entries(error.fieldErrors)) {
    const field = fields.find((candidate) => candidate === name)

    if (field && messages.length > 0) {
      setError(field, { type: "server", message: messages[0] })
      applied = true
    }
  }

  return applied
}