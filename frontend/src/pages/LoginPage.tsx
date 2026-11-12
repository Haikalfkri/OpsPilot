import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useForm } from "react-hook-form"
import { z } from "zod"
import { getErrorMessage } from "@/api/errors"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { useAuth } from "@/hooks/useAuth"
import { applyServerErrors } from "@/lib/form-errors"

// Aturan input. Batasnya SAMA dengan LoginRequestValidator di backend (Stage 4).
// Ini hanya untuk umpan balik cepat; yang memutuskan tetap backend.
const loginSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, "Email is required.")
    .max(256, "Email is too long.")
    .regex(/^\S+@\S+\.\S+$/, "Enter a valid email address."),
  password: z.string().min(1, "Password is required.").max(128, "Password is too long."),
})

// Tipe form diturunkan dari aturan di atas: tidak perlu menulis dua kali
type LoginForm = z.infer<typeof loginSchema>

export default function LoginPage() {
  const { login } = useAuth()

  // Pesan umum (bukan milik satu kolom), mis. "Invalid email or password."
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  })

  // handleSubmit hanya memanggil fungsi ini kalau semua aturan Zod lolos
  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)

    try {
      await login(values.email, values.password)
      // Berhasil: AuthProvider memperbarui state, dan App otomatis pindah halaman.
    } catch (error) {
      // 1) Error validasi per kolom dari backend -> tampil di bawah kolomnya
      if (applyServerErrors(error, setError, ["email", "password"])) return

      // 2) Selain itu (password salah, server mati, dan lain-lain) -> pesan umum di atas form
      setFormError(getErrorMessage(error))
    }
  })

  return (
    <main className="flex min-h-screen items-center justify-center bg-background p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Sign in to OpsPilot</CardTitle>
          <CardDescription>Use your work email and password.</CardDescription>
        </CardHeader>

        <CardContent>
          {/* noValidate: matikan validasi bawaan browser supaya pesan kita yang tampil */}
          <form onSubmit={onSubmit} noValidate className="space-y-4">
            {formError && (
              <Alert variant="destructive">
                <AlertDescription>{formError}</AlertDescription>
              </Alert>
            )}

            <div className="space-y-2">
              <Label htmlFor="email">Email</Label>
              {/* {...register("email")} menghubungkan kolom ini ke React Hook Form */}
              <Input
                id="email"
                type="email"
                autoComplete="username"
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
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                aria-invalid={errors.password ? true : undefined}
                {...register("password")}
              />
              {errors.password && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.password.message}
                </p>
              )}
            </div>

            <Button type="submit" className="w-full" disabled={isSubmitting}>
              {isSubmitting ? "Signing in…" : "Sign in"}
            </Button>

            {/* import.meta.env.DEV hanya true saat npm run dev. Di build produksi, blok ini hilang. */}
            {import.meta.env.DEV && (
              <p className="text-xs text-muted-foreground">
                Demo (local only): admin@opspilot.local / DemoPassword123!
              </p>
            )}
          </form>
        </CardContent>
      </Card>
    </main>
  )
}