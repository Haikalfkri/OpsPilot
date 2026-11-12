import { Link } from "react-router"
import { buttonVariants } from "@/components/ui/button"

export default function NotFoundPage() {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Page not found</h1>
      <p className="text-sm text-muted-foreground">The page you are looking for does not exist.</p>
      {/* Tautan yang tampil seperti tombol: memakai gaya tombol, bukan komponen Button */}
      <Link to="/" className={buttonVariants({ variant: "outline" })}>
        Back to dashboard
      </Link>
    </div>
  )
}