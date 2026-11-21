import type { IncidentStatus } from "@/types/incident"

interface StatusAction {
  label: string 
  confirm?: string 
}

export const STATUS_ACTIONS: Record<IncidentStatus, StatusAction> = {
  Open: { label: "Open" }, // tidak pernah jadi tujuan perpindahan
  Investigating: { label: "Start investigating" },
  Resolved: {
    label: "Mark resolved",
    confirm: "Mark this incident as resolved? After this it can only be closed. It cannot go back to investigating.",
  },
  Closed: {
    label: "Close incident",
    confirm: "Close this incident? A closed incident can no longer be changed.",
  },
}