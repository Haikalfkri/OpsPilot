import { isApiError } from "@/api/errors"
import { http } from "@/api/http"
import type { HealthResponse } from "@/types/api"

function looksLikeHealth(body: unknown): body is HealthResponse {
  return typeof body === "object" && body !== null && "status" in body && "checks" in body
}

export async function getHealth(signal?: AbortSignal): Promise<HealthResponse> {
  try {
    return await http.get<HealthResponse>("/health", { signal })
  } catch (error) {
    // Kalau PostgreSQL atau Redis bermasalah, backend menjawab 503, tetapi isinya tetap
    // JSON yang menjelaskan layanan mana yang bermasalah. Itu informasi berguna,
    // jadi kita tampilkan sebagai hasil biasa, bukan sebagai error.
    if (isApiError(error) && error.status === 503 && looksLikeHealth(error.body)) {
      return error.body
    }
    throw error
  }
}