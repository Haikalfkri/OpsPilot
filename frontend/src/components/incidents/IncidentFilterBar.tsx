import { FilterSelect } from "@/components/FilterSelect"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { SORT_OPTIONS, type IncidentFilters } from "@/lib/incident-filters"
import { SEVERITIES, STATUSES } from "@/types/incident"
import type { TeamMember } from "@/types/team"
import { SearchBox } from "@/components/SearchBox"

interface IncidentFilterBarProps {
  filters: IncidentFilters
  members: TeamMember[]
  canClear: boolean
  onChange: (patch: Partial<IncidentFilters>) => void
  onClear: () => void
}

export function IncidentFilterBar({ filters, members, canClear, onChange, onClear }: IncidentFilterBarProps) {
  const memberOptions = members.map((member) => ({ value: member.id, label: member.displayName }))

  if (filters.assigneeId && !memberOptions.some((option) => option.value === filters.assigneeId)) {
    memberOptions.push({ value: filters.assigneeId, label: "Unknown user" })
  }

  return (
    <div className="space-y-3">
      <SearchBox
        label="Search incidents"
        placeholder="Search title or description…"
        value={filters.search}
        onSearch={(search) => onChange({ search })}
      />

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <FilterSelect
          label="Status"
          allLabel="All statuses"
          value={filters.status ?? ""}
          options={STATUSES.map((status) => ({ value: status, label: status }))}
          onChange={(value) => onChange({ status: STATUSES.find((status) => status === value) })}
        />

        <FilterSelect
          label="Severity"
          allLabel="All severities"
          value={filters.severity ?? ""}
          options={SEVERITIES.map((severity) => ({ value: severity, label: severity }))}
          onChange={(value) => onChange({ severity: SEVERITIES.find((severity) => severity === value) })}
        />

        <FilterSelect
          label="Assignee"
          allLabel="All assignees"
          value={filters.assigneeId ?? ""}
          options={memberOptions}
          onChange={(value) => onChange({ assigneeId: value || undefined })}
        />

        <FilterSelect
          label="Sort by"
          value={filters.sort}
          options={SORT_OPTIONS.map((option) => ({ value: option.key, label: option.label }))}
          onChange={(value) => onChange({ sort: SORT_OPTIONS.find((option) => option.key === value)?.key ?? "newest" })}
        />

        <div className="space-y-1.5">
          <Label htmlFor="filter-from">Created from</Label>
          <Input
            id="filter-from"
            type="date"
            value={filters.from ?? ""}
            max={filters.to}
            onChange={(event) => onChange({ from: event.target.value || undefined })}
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="filter-to">Created to</Label>
          <Input
            id="filter-to"
            type="date"
            value={filters.to ?? ""}
            min={filters.from}
            onChange={(event) => onChange({ to: event.target.value || undefined })}
          />
        </div>

        {canClear && (
          <div className="flex items-end">
            <Button type="button" variant="ghost" onClick={onClear}>
              Clear filters
            </Button>
          </div>
        )}
      </div>
    </div>
  )
}