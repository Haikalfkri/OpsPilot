import { Link } from "react-router"
import { buttonVariants } from "@/components/ui/button"
import { usePageTitle } from "@/hooks/usePageTitle"

export default function ForbiddenPage() {
  usePageTitle("No access")
  
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">No access</h1>
      <p className="text-sm text-muted-foreground">You do not have access to this page.</p>
      <Link to="/" className={buttonVariants({ variant: "outline" })}>
        Back to dashboard
      </Link>
    </div>
  )
}