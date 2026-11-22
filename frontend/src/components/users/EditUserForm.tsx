import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useForm } from "react-hook-form"
import { getErrorMessage } from "@/api/errors"
import { FilterSelect } from "@/components/FilterSelect"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { useUpdateUser } from "@/hooks/useUserMutations"
import { applyServerErrors } from "@/lib/form-errors"
import { editUserSchema, type EditUserValues } from "@/lib/user-schemas"
import { ROLES, type User } from "@/types/auth"
import type { UpdateUserRequest } from "@/types/user"

interface EditUserFormProps {
  user: User
  isSelf: boolean // true = Admin sedang mengedit akunnya sendiri
}

export function EditUserForm({ user, isSelf }: EditUserFormProps) {
  const update = useUpdateUser(user.id)
  const [formError, setFormError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const {
    register,
    handleSubmit,
    setError,
    setValue,
    watch,
    reset,
    formState: { errors, isDirty },
  } = useForm<EditUserValues>({
    resolver: zodResolver(editUserSchema),
    defaultValues: { displayName: user.displayName, role: user.role },
  })

  const role = watch("role")

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    setSaved(false)

    const chosenRole = ROLES.find((item) => item === values.role)
    if (!chosenRole) return

    // Hanya yang benar-benar berubah yang dikirim
    const body: UpdateUserRequest = {}
    if (values.displayName !== user.displayName) body.displayName = values.displayName
    if (chosenRole !== user.role) body.role = chosenRole

    // Perbedaan hanya spasi di ujung (sudah dipangkas): tidak ada yang perlu dikirim
    if (Object.keys(body).length === 0) {
      reset({ displayName: user.displayName, role: user.role })
      return
    }

    try {
      const updated = await update.mutateAsync(body)
      reset({ displayName: updated.displayName, role: updated.role }) // formulir kembali "bersih"
      setSaved(true)
    } catch (error) {
      if (applyServerErrors(error, setError, ["displayName", "role"])) return
      setFormError(getErrorMessage(error)) // mis. 409: Admin aktif terakhir
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {formError && (
        <Alert variant="destructive">
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      {saved && !isDirty && (
        <Alert role="status">
          <AlertDescription>Changes saved.</AlertDescription>
        </Alert>
      )}

      <div className="space-y-2">
        <Label htmlFor="user-email">Email</Label>
        <Input id="user-email" value={user.email} disabled readOnly />
        <p className="text-xs text-muted-foreground">The email address cannot be changed.</p>
      </div>

      <div className="space-y-2">
        <Label htmlFor="user-display-name">Display name</Label>
        <Input
          id="user-display-name"
          aria-invalid={errors.displayName ? true : undefined}
          {...register("displayName")}
        />
        {errors.displayName && (
          <p role="alert" className="text-sm text-destructive">
            {errors.displayName.message}
          </p>
        )}
      </div>

      <div className="space-y-1.5">
        <FilterSelect
          label="Role"
          value={role}
          options={ROLES.map((item) => ({ value: item, label: item }))}
          onChange={(value) =>
            setValue("role", ROLES.find((item) => item === value) ?? user.role, { shouldDirty: true })
          }
        />

        {errors.role && (
          <p role="alert" className="text-sm text-destructive">
            {errors.role.message}
          </p>
        )}

        {isSelf ? (
          <p className="text-xs text-muted-foreground">
            This is your own account. If you change your role you will be signed out and must sign in again.
          </p>
        ) : (
          <p className="text-xs text-muted-foreground">
            If the role changes, this user is signed out on their next request.
          </p>
        )}
      </div>

      <Button type="submit" disabled={!isDirty || update.isPending}>
        {update.isPending ? "Saving…" : "Save changes"}
      </Button>
    </form>
  )
}