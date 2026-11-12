import HomePage from "@/pages/HomePage"
import LoginPage from "@/pages/LoginPage"
import { useAuth } from "@/hooks/useAuth"

export default function App() {
  const { isAuthenticated } = useAuth()

  return isAuthenticated ? <HomePage /> : <LoginPage />
}