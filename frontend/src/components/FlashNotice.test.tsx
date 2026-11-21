import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { createMemoryRouter, RouterProvider } from "react-router"
import { describe, expect, it } from "vitest"
import { FlashNotice } from "@/components/FlashNotice"

function renderAt(state?: unknown) {
  const router = createMemoryRouter([{ path: "/incidents/1", element: <FlashNotice /> }], {
    initialEntries: [{ pathname: "/incidents/1", state }],
  })

  render(<RouterProvider router={router} />)
  return router
}

describe("FlashNotice", () => {
  it("shows nothing when there is no notice", () => {
    renderAt()

    expect(screen.queryByRole("status")).toBeNull()
  })

  it("shows the notice, and removes it when dismissed", async () => {
    const router = renderAt({ notice: "Incident created." })

    expect(await screen.findByText("Incident created.")).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Dismiss" }))

    await waitFor(() => expect(screen.queryByText("Incident created.")).toBeNull())
    expect(router.state.location.state).toBeNull() // tidak muncul lagi saat halaman dimuat ulang
    expect(router.state.location.pathname).toBe("/incidents/1")
  })
})