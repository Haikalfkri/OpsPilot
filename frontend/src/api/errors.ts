export class ApiError extends Error {
    readonly status: number
    readonly title: string
    readonly detail?: string
    readonly fieldErrors: Record<string, string[]>
    readonly traceId?: string
    readonly body: unknown

    constructor(init: {
        status: number
        title: string
        detail?: string
        fieldErrors?: Record<string, string[]>
        traceId?: string
        body?: unknown
    })  {
        super(init.detail ?? init.title)
        this.name = "ApiError"
        this.status = init.status
        this.title = init.title
        this.detail = init.detail
        this.fieldErrors = init.fieldErrors ?? {}
        this.traceId = init.traceId
        this.body = init.body
    }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}

export function getErrorMessage(error: unknown): string {
    if (isApiError(error)) return error.detail ?? error.title
    if (error instanceof Error) return error.message
    return "Something went wrong."
}