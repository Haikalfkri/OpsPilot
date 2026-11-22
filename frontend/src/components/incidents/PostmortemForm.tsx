import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useForm, type UseFormRegisterReturn } from "react-hook-form"
import { getErrorMessage } from "@/api/errors"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { useSavePostmortem } from "@/hooks/usePostmortem"
import { applyServerErrors } from "@/lib/form-errors"
import { postmortemSchema, type PostmortemValues } from "@/lib/postmortem-schema"
import type { Postmortem } from "@/types/postmortem"

interface FieldProps {
  id: string
  label: string
  help: string
  rows: number
  error?: string
  registration: UseFormRegisterReturn
}

// Satu kolom teks panjang: label, petunjuk, dan pesan error
function Field({ id, label, help, rows, error, registration }: FieldProps) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      <p id={`${id}-help`} className="text-xs text-muted-foreground">
        {help}
      </p>
      <Textarea
        id={id}
        rows={rows}
        aria-describedby={`${id}-help`}
        aria-invalid={error ? true : undefined}
        {...registration}
      />
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}

interface PostmortemFormProps {
  incidentId: string
  initial: Postmortem | null // null = menulis yang pertama
  onDone: () => void // dipanggil setelah berhasil disimpan atau dibatalkan
}

export function PostmortemForm({ incidentId, initial, onDone }: PostmortemFormProps) {
  const save = useSavePostmortem(incidentId)
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isDirty },
  } = useForm<PostmortemValues>({
    resolver: zodResolver(postmortemSchema),
    defaultValues: {
      summary: initial?.summary ?? "",
      rootCause: initial?.rootCause ?? "",
      impact: initial?.impact ?? "",
      correctiveActions: initial?.correctiveActions ?? "",
    },
  })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)

    try {
      await save.mutateAsync(values)
      onDone()
    } catch (error) {
      if (applyServerErrors(error, setError, ["summary", "rootCause", "impact", "correctiveActions"])) return
      setFormError(getErrorMessage(error)) // mis. 409: incident tidak lagi Resolved atau Closed
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {formError && (
        <Alert variant="destructive">
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      <Field
        id="pm-summary"
        label="Summary"
        help="What happened, in a few sentences."
        rows={3}
        error={errors.summary?.message}
        registration={register("summary")}
      />
      <Field
        id="pm-root-cause"
        label="Root cause"
        help="Why it happened."
        rows={4}
        error={errors.rootCause?.message}
        registration={register("rootCause")}
      />
      <Field
        id="pm-impact"
        label="Impact"
        help="Who or what was affected, and for how long."
        rows={3}
        error={errors.impact?.message}
        registration={register("impact")}
      />
      <Field
        id="pm-actions"
        label="Corrective actions"
        help="What will be done so this does not happen again."
        rows={4}
        error={errors.correctiveActions?.message}
        registration={register("correctiveActions")}
      />

      <div className="flex gap-2">
        {/* Mengubah yang sudah ada: nonaktif selama belum ada yang berubah */}
        <Button type="submit" disabled={save.isPending || (initial !== null && !isDirty)}>
          {save.isPending ? "Saving…" : "Save postmortem"}
        </Button>
        <Button type="button" variant="outline" onClick={onDone} disabled={save.isPending}>
          Cancel
        </Button>
      </div>
    </form>
  )
}