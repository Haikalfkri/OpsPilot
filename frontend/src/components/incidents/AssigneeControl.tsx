import { getErrorMessage, getFieldError } from "@/api/errors"
import { FilterSelect } from "@/components/FilterSelect"
import { useAssignIncident } from "@/hooks/useIncidentMutations"
import { useTeamMembers } from "@/hooks/useTeamMembers"
import type { IncidentDetail } from "@/types/incident"

interface AssigneeControlProps {
  incident: IncidentDetail
  canEdit: boolean
}

export function AssigneeControl({ incident, canEdit }: AssigneeControlProps) {
  const members = useTeamMembers()
  const assign = useAssignIncident(incident.id)

  if (!canEdit) {
    return (
      <div className="space-y-1">
        <p className="text-sm text-muted-foreground">Assignee</p>
        <p className="text-sm">{incident.assigneeName ?? "Unassigned"}</p>
      </div>
    )
  }

  const options = (members.data?.items ?? [])
    .filter((member) => member.role !== "Viewer")
    .map((member) => ({ value: member.id, label: member.displayName }))

  if (incident.assigneeId && !options.some((option) => option.value === incident.assigneeId)) {
    options.push({ value: incident.assigneeId, label: incident.assigneeName ?? "Unknown user" })
  }

  return (
    <div className="space-y-1.5">
      <FilterSelect
        label="Assignee"
        allLabel="Unassigned"
        value={incident.assigneeId ?? ""}
        options={options}
        disabled={assign.isPending}
        onChange={(value) => assign.mutate(value || null)}
      />

      {assign.isPending && <p className="text-xs text-muted-foreground">Saving…</p>}

      {assign.isError && (
        <p role="alert" className="text-sm text-destructive">
          {getFieldError(assign.error, "assigneeId") ?? getErrorMessage(assign.error)}
        </p>
      )}
    </div>
  )
}