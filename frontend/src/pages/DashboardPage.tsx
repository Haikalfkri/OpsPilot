import { PlaceholderPage } from "@/components/PlaceholderPage"
import { useAuth } from "@/hooks/useAuth"

export default function DashboardPage() {
  const { user } = useAuth()

  return (
    <PlaceholderPage
      title="Dashboard"
      description={`Welcome back, ${user?.displayName}. Key numbers and charts will appear here.`}
      comingIn="Stage 9"
    />
  )
}