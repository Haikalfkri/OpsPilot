import { useLocation, useNavigate } from "react-router"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { readNotice } from "@/lib/navigation-state"

export function FlashNotice() {
  const location = useLocation()
  const navigate = useNavigate()
  const notice = readNotice(location.state)

  if (!notice) return null

  return (
    <Alert role="status">
      <AlertDescription className="flex items-center justify-between gap-4">
        <span>{notice}</span>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => navigate(location.pathname + location.search, { replace: true, state: null })}
        >
          Dismiss
        </Button>
      </AlertDescription>
    </Alert>
  )
}