import type { ReactNode } from "react"
import type { UseQueryResult } from "@tanstack/react-query"
import { getErrorMessage } from "@/api/errors"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"

interface QueryStateProps<T> {
  query: UseQueryResult<T>
  skeletonClassName?: string
  errorTitle?: string
  children: (data: T) => ReactNode // dipanggil hanya kalau datanya sudah ada
}

// Satu tempat untuk tiga keadaan: memuat -> kerangka, gagal -> pesan + Try again, berhasil -> isi
export function QueryState<T>({
  query,
  skeletonClassName = "h-24",
  errorTitle = "Could not load this section",
  children,
}: QueryStateProps<T>) {
  if (query.isPending) {
    return <Skeleton className={skeletonClassName} aria-busy="true" />
  }

  if (query.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>{errorTitle}</AlertTitle>
        <AlertDescription className="space-y-3">
          <p>{getErrorMessage(query.error)}</p>
          <Button variant="outline" size="sm" onClick={() => void query.refetch()}>
            Try again
          </Button>
        </AlertDescription>
      </Alert>
    )
  }

  return <>{children(query.data)}</>
}