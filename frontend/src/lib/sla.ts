import type { SlaState } from "@/types/incident"

export function getOverallSla(response: SlaState, resolution: SlaState): SlaState {
  if (response === "Breached" || resolution === "Breached") return "Breached"
  if (response === "Met" && resolution === "Met") return "Met"
  return "Pending"
}