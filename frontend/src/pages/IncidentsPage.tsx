import type { ReactNode } from "react"
import { Link, useSearchParams } from "react-router"
import { getErrorMessage } from "@/api/errors"
import { IncidentFilterBar } from "@/components/incidents/IncidentFilterBar"
import { IncidentTable, IncidentTableSkeleton } from "@/components/incidents/IncidentTable"
import { Pagination } from "@/components/Pagination"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
import { useAuth } from "@/hooks/useAuth"
import { useIncidents } from "@/hooks/useIncidents"
import { useTeamMembers } from "@/hooks/useTeamMembers"
import { usePageTitle } from "@/hooks/usePageTitle"
import {
  PAGE_SIZE,
  hasActiveFilters,
  parseFilters,
  toApiParams,
  toSearchParams,
  type IncidentFilters,
} from "@/lib/incident-filters"
import { canManageIncidents } from "@/lib/permissions"

export default function IncidentsPage() {
  const { user } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  usePageTitle("Incidents")

  // URL -> filter -> parameter API. URL adalah satu-satunya sumber kebenaran.
  const filters = parseFilters(searchParams)
  const incidents = useIncidents(toApiParams(filters))
  const members = useTeamMembers()

  // Mengubah filter = menulis URL baru. Halaman kembali ke 1, kecuali yang diubah memang halaman.
  function updateFilters(patch: Partial<IncidentFilters>) {
    setSearchParams(toSearchParams({ ...filters, page: 1, ...patch }))
  }

  function clearFilters() {
    setSearchParams(new URLSearchParams())
  }

  let content: ReactNode

  if (incidents.isPending) {
    content = <IncidentTableSkeleton />
  } else if (incidents.isError) {
    content = (
      <Alert variant="destructive">
        <AlertTitle>Could not load incidents</AlertTitle>
        <AlertDescription className="space-y-3">
          <p>{getErrorMessage(incidents.error)}</p>
          <Button variant="outline" size="sm" onClick={() => incidents.refetch()}>
            Try again
          </Button>
        </AlertDescription>
      </Alert>
    )
  } else if (incidents.data.items.length === 0) {
    content = (
      <div className="rounded-md border p-8 text-center">
        {incidents.data.totalCount > 0 ? (
          // Ada data, tetapi nomor halaman di URL melewati halaman terakhir
          <div className="space-y-3">
            <p className="font-medium">This page does not exist.</p>
            <Button variant="outline" onClick={() => updateFilters({ page: 1 })}>
              Go to the first page
            </Button>
          </div>
        ) : hasActiveFilters(filters) ? (
          <p className="text-sm text-muted-foreground">
            No incidents match your filters. Try changing or clearing them.
          </p>
        ) : (
          <p className="text-sm text-muted-foreground">No incidents yet.</p>
        )}
      </div>
    )
  } else {
    content = (
      <>
        <IncidentTable items={incidents.data.items} dimmed={incidents.isPlaceholderData} />
        <Pagination
          page={incidents.data.page}
          totalPages={incidents.data.totalPages}
          totalCount={incidents.data.totalCount}
          pageSize={PAGE_SIZE}
          disabled={incidents.isPlaceholderData}
          onPageChange={(page) => updateFilters({ page })}
        />
      </>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">Incidents</h1>

        {/* Hanya Admin dan Responder. Viewer tidak melihat tombol ini (backend juga menolaknya). */}
        {user && canManageIncidents(user.role) && (
          <Link to="/incidents/new" className={buttonVariants()}>
            New incident
          </Link>
        )}
      </div>

      <IncidentFilterBar
        filters={filters}
        members={members.data?.items ?? []}
        canClear={hasActiveFilters(filters)}
        onChange={updateFilters}
        onClear={clearFilters}
      />

      {content}
    </div>
  )
}