import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { createMemoryRouter, RouterProvider } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/errors"
import { AuthContext, type AuthContextValue } from "@/hooks/auth-context"
import { markSessionExpired, wasSessionExpired } from "@/lib/auth-storage"
import LoginPage from "@/pages/LoginPage"

const login = vi.fn<AuthContextValue["login"]>()

function renderPage() {
  const auth: AuthContextValue = { user: null, isAuthenticated: false, login, logout: vi.fn() }
  const router = createMemoryRouter([{ path: "/login", element: <LoginPage /> }], { initialEntries: ["/login"] })

  render(
    <AuthContext.Provider value={auth}>
      <RouterProvider router={router} />
    </AuthContext.Provider>,
  )
}

function fill(email: string, password: string) {
  fireEvent.change(screen.getByLabelText("Email"), { target: { value: email } })
  fireEvent.change(screen.getByLabelText("Password"), { target: { value: password } })
}

const submit = () => fireEvent.click(screen.getByRole("button", { name: "Sign in" }))

beforeEach(() => {
  vi.resetAllMocks()
  sessionStorage.clear()
})

describe("LoginPage: the form", () => {
  it("shows the rate-limit message from the backend", async () => {
    login.mockRejectedValue(
      new ApiError({
        status: 429,
        title: "Too many requests",
        detail: "Too many sign-in attempts. Please wait a moment and try again.",
      }),
    )
    renderPage()

    fill("admin@opspilot.local", "DemoPassword123!")
    submit()

    expect(await screen.findByText("Too many sign-in attempts. Please wait a moment and try again.")).toBeTruthy()
  })
  
  it("has the sign-in heading, and sets the tab title", () => {
    renderPage()

    expect(screen.getByRole("heading", { level: 1, name: "Sign in to OpsPilot" })).toBeTruthy()
    expect(document.title).toBe("Sign in · OpsPilot")
  })

  it("shows both problems for an empty form, and does not try to sign in", async () => {
    renderPage()

    submit()

    expect(await screen.findByText("Email is required.")).toBeTruthy()
    expect(screen.getByText("Password is required.")).toBeTruthy()
    expect(login).not.toHaveBeenCalled()
  })

  it("rejects an email that is not an email", async () => {
    renderPage()

    fill("abc", "DemoPassword123!")
    submit()

    expect(await screen.findByText("Enter a valid email address.")).toBeTruthy()
    expect(login).not.toHaveBeenCalled()
  })

  it("signs in with the trimmed email and the password exactly as typed", async () => {
    login.mockResolvedValue(undefined)
    renderPage()

    fill("  admin@opspilot.local  ", " Demo Password! ")
    submit()

    await waitFor(() => expect(login).toHaveBeenCalledWith("admin@opspilot.local", " Demo Password! "))
  })

  it("shows the backend's generic message when sign-in fails, without saying which part was wrong", async () => {
    login.mockRejectedValue(new ApiError({ status: 401, title: "Authentication failed", detail: "Invalid email or password." }))
    renderPage()

    fill("admin@opspilot.local", "wrong-password")
    submit()

    expect(await screen.findByText("Invalid email or password.")).toBeTruthy()
  })

  it("puts a validation error from the backend under its own field", async () => {
    login.mockRejectedValue(
      new ApiError({ status: 400, title: "Validation failed", fieldErrors: { email: ["Email must be a valid email address."] } }),
    )
    renderPage()

    fill("admin@opspilot.local", "DemoPassword123!")
    submit()

    expect(await screen.findByText("Email must be a valid email address.")).toBeTruthy()
  })

  it("disables the button while signing in", async () => {
    login.mockReturnValue(new Promise(() => {}))
    renderPage()

    fill("admin@opspilot.local", "DemoPassword123!")
    submit()

    const busy = (await screen.findByRole("button", { name: "Signing in…" })) as HTMLButtonElement
    expect(busy.disabled).toBe(true)
  })
})

describe("LoginPage: after the session ended", () => {
  it("says nothing special on a normal visit", () => {
    renderPage()

    expect(screen.queryByText(/session has expired/)).toBeNull()
  })

  it("explains that the session expired, and shows that only once", async () => {
    markSessionExpired("/incidents/abc")
    renderPage()

    expect(screen.getByText("Your session has expired. Please sign in again.")).toBeTruthy()

    // Penandanya dibuang setelah dibaca: kunjungan berikutnya tidak menampilkannya lagi
    await waitFor(() => expect(wasSessionExpired()).toBe(false))
  })
})