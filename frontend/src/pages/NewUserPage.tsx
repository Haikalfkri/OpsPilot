import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useForm, useWatch } from "react-hook-form"
import { Link, useNavigate } from "react-router"
import { getErrorMessage, isApiError } from "@/api/errors"
import { FilterSelect } from "@/components/FilterSelect"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { useCreateUser } from "@/hooks/useUserMutations"
import { applyServerErrors } from "@/lib/form-errors"
import { noticeState } from "@/lib/navigation-state"
import { PASSWORD_HINT, createUserSchema, type CreateUserValues } from "@/lib/user-schemas"
import { ROLES } from "@/types/auth"
import { usePageTitle } from "@/hooks/usePageTitle"

export default function NewUserPage() {
  const navigate = useNavigate()
  const createUser = useCreateUser()
  const [formError, setFormError] = useState<string | null>(null)
  usePageTitle("Add user")
  
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    control,
    formState: { errors, isSubmitting },
  } = useForm<CreateUserValues>({
    resolver: zodResolver(createUserSchema),
    // Role sengaja tidak punya nilai awal: hak akses tidak boleh terisi diam-diam
    defaultValues: { email: "", displayName: "", password: "", role: "" },
  })

  const role = useWatch({ control, name: "role" })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)

    const chosenRole = ROLES.find((item) => item === values.role)
    if (!chosenRole) return

    try {
      await createUser.mutateAsync({
        email: values.email,
        displayName: values.displayName,
        password: values.password,
        role: chosenRole,
      })

      navigate("/users", { state: noticeState("User created.") })
    } catch (error) {
      // 409 = email sudah dipakai: itu masalah kolom Email, bukan masalah umum
      if (isApiError(error) && error.status === 409) {
        setError("email", { type: "server", message: error.detail ?? error.title })
        return
      }

      if (applyServerErrors(error, setError, ["email", "displayName", "password", "role"])) return

      setFormError(getErrorMessage(error))
    }
  })

  return (
    <div className="max-w-xl space-y-4">
      <Link to="/users" className="text-sm text-muted-foreground hover:underline">
        ← Back to users
      </Link>

      <Card>
        <CardHeader>
          <CardTitle className="text-2xl" role="heading" aria-level={1}>Add user</CardTitle>
          <CardDescription>The new user joins your team and can sign in right away.</CardDescription>
        </CardHeader>

        <CardContent>
          <form onSubmit={onSubmit} noValidate className="space-y-5">
            {formError && (
              <Alert variant="destructive">
                <AlertDescription>{formError}</AlertDescription>
              </Alert>
            )}

            <div className="space-y-2">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="off"
                aria-invalid={errors.email ? true : undefined}
                {...register("email")}
              />
              {errors.email && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.email.message}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="displayName">Display name</Label>
              <Input
                id="displayName"
                aria-invalid={errors.displayName ? true : undefined}
                {...register("displayName")}
              />
              {errors.displayName && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.displayName.message}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="password">Initial password</Label>
              <Input
                id="password"
                type="password"
                autoComplete="new-password"
                aria-invalid={errors.password ? true : undefined}
                {...register("password")}
              />
              {errors.password && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.password.message}
                </p>
              )}
              <p className="text-xs text-muted-foreground">{PASSWORD_HINT}</p>
            </div>

            <div className="space-y-1.5">
              <FilterSelect
                label="Role"
                allLabel="Select role…"
                value={role}
                options={ROLES.map((item) => ({ value: item, label: item }))}
                onChange={(value) => setValue("role", value, { shouldValidate: Boolean(errors.role) })}
              />
              {errors.role && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.role.message}
                </p>
              )}
              <p className="text-xs text-muted-foreground">
                Admins manage users. Responders work on incidents. Viewers can only read.
              </p>
            </div>

            <div className="flex gap-2">
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? "Creating…" : "Create user"}
              </Button>
              <Link to="/users" className={buttonVariants({ variant: "outline" })}>
                Cancel
              </Link>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}