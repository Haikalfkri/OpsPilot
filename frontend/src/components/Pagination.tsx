import { Button } from "@/components/ui/button"

interface PaginationProps {
  page: number
  totalPages: number
  totalCount: number
  pageSize: number
  disabled?: boolean
  onPageChange: (page: number) => void
}

export function Pagination({ page, totalPages, totalCount, pageSize, disabled, onPageChange }: PaginationProps) {
  const first = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const last = Math.min(page * pageSize, totalCount)

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
      <p className="text-muted-foreground">
        {first}–{last} of {totalCount}
      </p>

      <div className="flex items-center gap-2">
        <Button variant="outline" size="sm" disabled={disabled || page <= 1} onClick={() => onPageChange(page - 1)}>
          Previous
        </Button>

        <span aria-live="polite">
          Page {page} of {Math.max(totalPages, 1)}
        </span>

        <Button
          variant="outline"
          size="sm"
          disabled={disabled || page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          Next
        </Button>
      </div>
    </div>
  )
}