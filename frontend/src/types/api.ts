export interface ProblemDetails {
    type?: string
    title?: string
    status?: number
    detail?: string
    traceId?: string
    // Hanya ada di error validasi (400): nama field -> daftar pesan.
    // Contoh: { "email": ["Email is required."] }
    errors?: Record<string, string[]>
}

export interface HealthCheckEntry {
    name: string
    status: string
}

export interface HealthResponse {
  status: string
  checks: HealthCheckEntry[]
}