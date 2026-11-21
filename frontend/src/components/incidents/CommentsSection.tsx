import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useForm } from "react-hook-form"
import { z } from "zod"
import { getErrorMessage } from "@/api/errors"
import { Pagination } from "@/components/Pagination"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { Textarea } from "@/components/ui/textarea"
import { useAddComment } from "@/hooks/useIncidentMutations"
import { useIncidentComments } from "@/hooks/useIncidentDetail"
import { FEED_PAGE_SIZE } from "@/lib/constants"
import { applyServerErrors } from "@/lib/form-errors"
import { formatDateTime, formatRelative } from "@/lib/format"

const commentSchema = z.object({
  body: z.string().trim().min(1, "Comment is required.").max(2000, "Comment must be at most 2000 characters."),
})

type CommentForm = z.infer<typeof commentSchema>

interface CommentsSectionProps {
  incidentId: string
  canWrite: boolean
}

export function CommentsSection({ incidentId, canWrite }: CommentsSectionProps) {
  const [page, setPage] = useState(1)
  const [formError, setFormError] = useState<string | null>(null)

  const comments = useIncidentComments(incidentId, page)
  const addComment = useAddComment(incidentId)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CommentForm>({
    resolver: zodResolver(commentSchema),
    defaultValues: { body: "" },
  })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)

    try {
      await addComment.mutateAsync(values.body)
      reset()

      const total = (comments.data?.totalCount ?? 0) + 1
      setPage(Math.max(1, Math.ceil(total / FEED_PAGE_SIZE)))
    } catch (error) {
      if (applyServerErrors(error, setError, ["body"])) return
      setFormError(getErrorMessage(error))
    }
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>Comments</CardTitle>
      </CardHeader>

      <CardContent className="space-y-4">
        {comments.isPending && <Skeleton className="h-16 w-full" />}

        {comments.isError && (
          <Alert variant="destructive">
            <AlertDescription className="space-y-2">
              <p>{getErrorMessage(comments.error)}</p>
              <Button variant="outline" size="sm" onClick={() => comments.refetch()}>
                Try again
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {comments.data && comments.data.items.length === 0 && (
          <p className="text-sm text-muted-foreground">No comments yet.</p>
        )}

        {comments.data && comments.data.items.length > 0 && (
          <ul className="space-y-4">
            {comments.data.items.map((comment) => (
              <li key={comment.id} className="space-y-1">
                <p className="text-sm">
                  <span className="font-medium">{comment.authorName ?? "Unknown user"}</span>{" "}
                  <span className="text-muted-foreground" title={formatDateTime(comment.createdAtUtc)}>
                    · {formatRelative(comment.createdAtUtc)}
                  </span>
                </p>
                <p className="whitespace-pre-wrap break-words text-sm">{comment.body}</p>
              </li>
            ))}
          </ul>
        )}

        {comments.data && comments.data.totalPages > 1 && (
          <Pagination
            page={comments.data.page}
            totalPages={comments.data.totalPages}
            totalCount={comments.data.totalCount}
            pageSize={FEED_PAGE_SIZE}
            disabled={comments.isPlaceholderData}
            onPageChange={setPage}
          />
        )}

        {canWrite ? (
          <form onSubmit={onSubmit} noValidate className="space-y-2 border-t pt-4">
            {formError && (
              <Alert variant="destructive">
                <AlertDescription>{formError}</AlertDescription>
              </Alert>
            )}

            <Label htmlFor="new-comment">Add a comment</Label>
            <Textarea
              id="new-comment"
              rows={3}
              aria-invalid={errors.body ? true : undefined}
              {...register("body")}
            />
            {errors.body && (
              <p role="alert" className="text-sm text-destructive">
                {errors.body.message}
              </p>
            )}

            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Posting…" : "Post comment"}
            </Button>
          </form>
        ) : (
          <p className="border-t pt-4 text-sm text-muted-foreground">Viewers can read comments but not write them.</p>
        )}
      </CardContent>
    </Card>
  )
}