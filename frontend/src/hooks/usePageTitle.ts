import { useEffect } from "react"

const APP_NAME = "OpsPilot"

// Mengatur judul tab browser: "Incidents · OpsPilot".
// Tanpa judul (mis. data belum tiba), tab hanya menampilkan nama aplikasi.
export function usePageTitle(title: string | undefined) {
  useEffect(() => {
    document.title = title ? `${title} · ${APP_NAME}` : APP_NAME
  }, [title])
}