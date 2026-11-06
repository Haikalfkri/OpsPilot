import { ApiError } from "@/api/errors"
import { config } from "@/lib/config"
import type { ProblemDetails } from "@/types/api"

let getToken: () => string | null = () => null
let onUnauthorized: () => void = () => {}

export function configureHttp(options: {
  getToken?: () => string | null
  onUnauthorized?: () => void
}) {
  if (options.getToken) getToken = options.getToken
  if (options.onUnauthorized) onUnauthorized = options.onUnauthorized
}

type QueryValue = string | number | boolean | null | undefined

export interface RequestOptions {
  query?: Record<string, QueryValue> // ?page=1&pageSize=20
  body?: unknown // dikirim sebagai JSON
  signal?: AbortSignal // untuk membatalkan permintaan
  // true = 401 dianggap hasil biasa (mis. password salah saat login), bukan "sesi habis"
  skipUnauthorizedHandler?: boolean
}

// Menyusun alamat lengkap. Nilai kosong dibuang supaya tidak jadi "?search=undefined".
function buildUrl(path: string, query?: Record<string, QueryValue>): string {
  const url = new URL(config.apiUrl + path)

  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== null && value !== "") {
      url.searchParams.set(key, String(value))
    }
  }

  return url.toString()
}

// Membaca isi response. Response kosong (mis. 204) menghasilkan undefined.
async function readBody(response: Response): Promise<unknown> {
  if (response.status === 204) return undefined

  const text = await response.text()
  if (!text) return undefined

  try {
    return JSON.parse(text)
  } catch {
    return text // bukan JSON
  }
}

// Mengubah response gagal menjadi ApiError, memakai bentuk Problem Details dari backend.
function toApiError(status: number, body: unknown): ApiError {
  const problem = typeof body === "object" && body !== null ? (body as ProblemDetails) : undefined

  return new ApiError({
    status,
    title: problem?.title ?? `Request failed (${status})`,
    detail: problem?.detail,
    fieldErrors: problem?.errors,
    traceId: problem?.traceId,
    body,
  })
}

async function request<T>(method: string, path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: "application/json" }

  // Token (kalau ada) dikirim di setiap permintaan sebagai "Bearer"
  const token = getToken()
  if (token) headers.Authorization = `Bearer ${token}`

  if (options.body !== undefined) headers["Content-Type"] = "application/json"

  let response: Response
  try {
    response = await fetch(buildUrl(path, options.query), {
      method,
      headers,
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      signal: options.signal,
    })
  } catch (error) {
    // Dibatalkan dengan sengaja: bukan error, teruskan apa adanya
    if (error instanceof DOMException && error.name === "AbortError") throw error

    // fetch hanya melempar error kalau permintaan TIDAK SAMPAI ke server:
    // API mati, alamat salah, atau diblokir CORS. (Status 4xx/5xx tidak melempar error.)
    throw new ApiError({
      status: 0,
      title: "Cannot reach the server",
      detail: "Check that the API is running and that its CORS settings allow this address.",
    })
  }

  const body = await readBody(response)

  if (!response.ok) {
    // 401 di sini berarti token hilang, salah, atau kedaluwarsa: beri tahu aplikasi
    // (Tahap 3: bersihkan sesi dan kembali ke halaman login)
    if (response.status === 401 && !options.skipUnauthorizedHandler) onUnauthorized()

    throw toApiError(response.status, body)
  }

  return body as T
}

export const http = {
  get: <T>(path: string, options?: Omit<RequestOptions, "body">) => request<T>("GET", path, options),

  post: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, "body">) =>
    request<T>("POST", path, { ...options, body }),

  put: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, "body">) =>
    request<T>("PUT", path, { ...options, body }),

  patch: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, "body">) =>
    request<T>("PATCH", path, { ...options, body }),

  delete: <T>(path: string, options?: Omit<RequestOptions, "body">) => request<T>("DELETE", path, options),
}