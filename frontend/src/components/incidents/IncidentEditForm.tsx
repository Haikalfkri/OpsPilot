import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useForm } from "react-hook-form"
import { z } from "zod"
import { getErrorMessage } from "@/api/errors"
import { FilterSelect } from "@/components/FilterSelect"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { useUpdateIncident } from "@/hooks/useIncidentMutations"
import { applyServerErrors } from "@/lib/form-errors"
import { SEVERITIES, type IncidentDetail } from "@/types/incident"

const editSchema = z.object({
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
  severity: z.enum(SEVERITIES),
})

type EditForm = z.infer<typeof editSchema>

interface IncidentEditFormProps {
  incident: IncidentDetail
  onDone: () => void
}

export function IncidentEditForm({ incident, onDone }: IncidentEditFormProps) {
  const update = useUpdateIncident(incident.id)
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setError,
    setValue,
    watch,
    formState: { errors, isDirty },
  } = useForm<EditForm>({
    resolver: zodResolver(editSchema),
    defaultValues: {
      title: incident.title,
      description: incident.description,
      severity: incident.severity,
    },
  })

  const severity = watch("severity")

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)

    try {
      await update.mutateAsync(values)
      onDone()
    } catch (error) {
      if (applyServerErrors(error, setError, ["title", "description", "severity"])) return
      setFormError(getErrorMessage(error)) // mis. 409: incident sudah Closed
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {formError && (
        <Alert variant="destructive">
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      <div className="space-y-2">
        <Label htmlFor="edit-title">Title</Label>
        <Input id="edit-title" aria-invalid={errors.title ? true : undefined} {...register("title")} />
        {errors.title && (
          <p role="alert" className="text-sm text-destructive">
            {errors.title.message}
          </p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="edit-description">Description</Label>
        <Textarea
          id="edit-description"
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

      <div className="space-y-1.5">
        <FilterSelect
          label="Severity"
          value={severity}
          options={SEVERITIES.map((item) => ({ value: item, label: item }))}
          onChange={(value) =>
            setValue("severity", SEVERITIES.find((item) => item === value) ?? incident.severity, {
              shouldDirty: true,
            })
          }
        />
        <p className="text-xs text-muted-foreground">
          Changing the severity recalculates the SLA targets from the creation time.
        </p>
        {errors.severity && (
          <p role="alert" className="text-sm text-destructive">
            {errors.severity.message}
          </p>
        )}
      </div>

      <div className="flex gap-2">
        <Button type="submit" disabled={!isDirty || update.isPending}>
          {update.isPending ? "Saving…" : "Save changes"}
        </Button>
        <Button type="button" variant="outline" onClick={onDone} disabled={update.isPending}>
          Cancel
        </Button>
      </div>
    </form>
  )
}