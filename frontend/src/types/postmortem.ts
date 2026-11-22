// GET /api/v1/incidents/{id}/postmortem
export interface Postmortem {
  id: string
  incidentId: string
  authorId: string // penulis ASLI: tidak berubah walau orang lain memperbarui
  authorName: string | null
  summary: string
  rootCause: string
  impact: string
  correctiveActions: string
  createdAtUtc: string
  updatedAtUtc: string
}

// PUT: keempat field wajib. PUT mengganti seluruh isinya.
export interface SavePostmortemRequest {
  summary: string
  rootCause: string
  impact: string
  correctiveActions: string
}