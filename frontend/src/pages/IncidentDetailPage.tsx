import { useParams } from "react-router"
import { PlaceholderPage } from "@/components/PlaceholderPage"

export default function IncidentDetailPage() {
  const { id } = useParams<{ id: string }>()

  return (
    <PlaceholderPage
      title="Incident details"
      description={`Details, timeline, and comments for incident ${id}.`}
      comingIn="Stage 6"
    />
  )
}