import { FilterSelect } from "@/components/FilterSelect"
import { SearchBox } from "@/components/SearchBox"
import { Button } from "@/components/ui/button"
import type { UserFilters } from "@/lib/user-filters"
import { ROLES } from "@/types/auth"

interface UserFilterBarProps {
  filters: UserFilters
  canClear: boolean
  onChange: (patch: Partial<UserFilters>) => void
  onClear: () => void
}

export function UserFilterBar({ filters, canClear, onChange, onClear }: UserFilterBarProps) {
  return (
    <div className="space-y-3">
      <SearchBox
        label="Search users"
        placeholder="Search name or email…"
        value={filters.search}
        onSearch={(search) => onChange({ search })}
      />

      <div className="grid gap-3 sm:grid-cols-3">
        <FilterSelect
          label="Role"
          allLabel="All roles"
          value={filters.role ?? ""}
          options={ROLES.map((role) => ({ value: role, label: role }))}
          onChange={(value) => onChange({ role: ROLES.find((role) => role === value) })}
        />

        <FilterSelect
          label="Status"
          allLabel="All users"
          value={filters.status === "all" ? "" : filters.status}
          options={[
            { value: "active", label: "Active" },
            { value: "inactive", label: "Inactive" },
          ]}
          onChange={(value) => onChange({ status: value === "active" || value === "inactive" ? value : "all" })}
        />

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