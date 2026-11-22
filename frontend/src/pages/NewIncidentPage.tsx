import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useForm, useWatch } from "react-hook-form"
import { Link, useNavigate } from "react-router"
import { z } from "zod"
import { getErrorMessage } from "@/api/errors"
import { FilterSelect } from "@/components/FilterSelect"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { useCreateIncident } from "@/hooks/useIncidentMutations"
import { useSlaPolicy } from "@/hooks/useSlaPolicy"
import { useTeamMembers } from "@/hooks/useTeamMembers"
import { formatDuration } from "@/lib/format"
import { applyServerErrors } from "@/lib/form-errors"
import { noticeState } from "@/lib/navigation-state"
import { SEVERITIES, type IncidentSeverity } from "@/types/incident"
import { usePageTitle } from "@/hooks/usePageTitle"

const createSchema = z.object({
  title: z
    .string()
    .trim()
    .min(5, "Title must be at least 5 characters.")
    .max(160, "Title must be at most 160 characters."),
  description: z
    .string()
    .trim()
    .min(1, "Description is required.")
    .max(5000, "Description must be at most 5000 characters."),
  // Nilai dari <select> berupa teks; "" berarti belum memilih
  severity: z.string().refine((value) => SEVERITIES.some((item) => item === value), "Select a severity."),
  assigneeId: z.string(), // "" = tanpa assignee
})

type CreateForm = z.infer<typeof createSchema>

// Teks di bawah pilihan severity: apa konsekuensinya bagi SLA
function SlaHint({ severity }: { severity: IncidentSeverity | undefined }) {
  const policy = useSlaPolicy()
  const target = policy.data?.targets.find((item) => item.severity === severity)

  if (policy.isError) {
    return <p className="text-xs text-muted-foreground">SLA targets are unavailable right now.</p>
  }

  if (!severity || !target) {
    return <p className="text-xs text-muted-foreground">Pick a severity to see its SLA targets.</p>
  }

  return (
    <p className="text-xs text-muted-foreground">
      {severity}: first response within {formatDuration(target.responseMinutes)}, resolution within{" "}
      {formatDuration(target.resolutionMinutes)}. These are configurable demo values.
    </p>
  )
}

export default function NewIncidentPage() {
  const navigate = useNavigate()
  const members = useTeamMembers()
  const createIncident = useCreateIncident()
  usePageTitle("New incident")

  // Pesan umum (bukan milik satu kolom), mis. "Cannot reach the server."
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setError,
    setValue,
    control,
    formState: { errors, isSubmitting },
  } = useForm<CreateForm>({
    resolver: zodResolver(createSchema),
    defaultValues: { title: "", description: "", severity: "", assigneeId: "" },
  })

  // Dua dropdown memakai komponen biasa, jadi nilainya dipantau dengan watch
  const severity = useWatch({ control, name: "severity" })
  const assigneeId = useWatch({ control, name: "assigneeId" })
  const selectedSeverity = SEVERITIES.find((item) => item === severity)

  // Backend menolak Viewer sebagai assignee, jadi tidak ditawarkan
  const assigneeOptions = (members.data?.items ?? [])
    .filter((member) => member.role !== "Viewer")
    .map((member) => ({ value: member.id, label: member.displayName }))

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)

    // Sudah dijamin oleh validasi; baris ini memastikan tipenya benar untuk TypeScript
    const chosenSeverity = SEVERITIES.find((item) => item === values.severity)
    if (!chosenSeverity) return

    try {
      const created = await createIncident.mutateAsync({
        title: values.title,
        description: values.description,
        severity: chosenSeverity,
        assigneeId: values.assigneeId || undefined,
      })

      // Berhasil: ke halaman detail dengan pesan sukses
      navigate(`/incidents/${created.id}`, { state: noticeState("Incident created.") })
    } catch (error) {
      // Error validasi per kolom dari backend -> tampil di bawah kolomnya
      if (applyServerErrors(error, setError, ["title", "description", "severity", "assigneeId"])) return

      // Selain itu (server mati, 403, dan lain-lain) -> pesan umum di atas form
      setFormError(getErrorMessage(error))
    }
  })

  return (
    <div className="max-w-2xl space-y-4">
      <Link to="/incidents" className="text-sm text-muted-foreground hover:underline">
        ← Back to incidents
      </Link>

      <Card>
        <CardHeader>
          <CardTitle className="text-2xl" role="heading" aria-level={1}>New incident</CardTitle>
          <CardDescription>Describe what is happening. You can assign it now or later.</CardDescription>
        </CardHeader>

        <CardContent>
          <form onSubmit={onSubmit} noValidate className="space-y-5">
            {formError && (
              <Alert variant="destructive">
                <AlertDescription>{formError}</AlertDescription>
              </Alert>
            )}

            <div className="space-y-2">
              <Label htmlFor="title">Title</Label>
              <Input id="title" maxLength={200} aria-invalid={errors.title ? true : undefined} {...register("title")} />
              {errors.title && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.title.message}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea
                id="description"
                rows={6}
                aria-invalid={errors.description ? true : undefined}
                {...register("description")}
              />
              {errors.description && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.description.message}
                </p>
              )}
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-1.5">
                <FilterSelect
                  label="Severity"
                  allLabel="Select severity…"
                  value={severity}
                  options={SEVERITIES.map((item) => ({ value: item, label: item }))}
                  // shouldValidate: pesan "Select a severity." hilang begitu pengguna memilih
                  onChange={(value) => setValue("severity", value, { shouldValidate: Boolean(errors.severity) })}
                />

                {errors.severity && (
                  <p role="alert" className="text-sm text-destructive">
                    {errors.severity.message}
                  </p>
                )}
                <SlaHint severity={selectedSeverity} />
              </div>

              <div className="space-y-1.5">
                <FilterSelect
                  label="Assignee"
                  allLabel="Unassigned"
                  value={assigneeId}
                  options={assigneeOptions}
                  onChange={(value) => setValue("assigneeId", value)}
                />

                {errors.assigneeId && (
                  <p role="alert" className="text-sm text-destructive">
                    {errors.assigneeId.message}
                  </p>
                )}

                {members.isError && (
                  <p role="alert" className="text-xs text-destructive">
                    Could not load team members. You can still create the incident without an assignee.
                  </p>
                )}

                <p className="text-xs text-muted-foreground">Optional. The assignee is notified.</p>
              </div>
            </div>

            <div className="flex gap-2">
              {/* Nonaktif selama mengirim: klik ganda tidak membuat dua incident */}
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? "Creating…" : "Create incident"}
              </Button>
              <Link to="/incidents" className={buttonVariants({ variant: "outline" })}>
                Cancel
              </Link>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}