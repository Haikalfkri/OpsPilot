import { z } from "zod"

function field(label: string, maxLength: number) {
  return z
    .string()
    .trim()
    .min(1, `${label} is required.`)
    .max(maxLength, `${label} must be at most ${maxLength} characters.`)
}

export const postmortemSchema = z.object({
  summary: field("Summary", 2000),
  rootCause: field("Root cause", 4000),
  impact: field("Impact", 2000),
  correctiveActions: field("Corrective actions", 4000),
})

export type PostmortemValues = z.infer<typeof postmortemSchema>