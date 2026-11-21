import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { createMemoryRouter, RouterProvider } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/errors"
import { createIncident } from "@/api/incidents"
import { getSlaPolicy } from "@/api/sla"
import { listTeamMembers } from "@/api/team"
import NewIncidentPage from "@/pages/NewIncidentPage"
import type { IncidentDetail } from "@/types/incident"

// Semua pemanggil API diganti fungsi palsu: tes tidak menyentuh jaringan
vi.mock("@/api/incidents", () => ({
  createIncident: vi.fn(),
  updateIncident: vi.fn(),
  changeIncidentStatus: vi.fn(),
  assignIncident: vi.fn(),
  addComment: vi.fn(),
}))
vi.mock("@/api/sla", () => ({ getSlaPolicy: vi.fn() }))
vi.mock("@/api/team", () => ({ listTeamMembers: vi.fn() }))

const CREATED_ID = "22222222-2222-2222-2222-222222222222"
const RESPONDER_ID = "a0000000-0000-0000-0000-000000000002"

const created = { id: CREATED_ID } as IncidentDetail

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [
      { path: "/incidents/new", element: <NewIncidentPage /> },
      { path: "/incidents/:id", element: <p>detail page</p> },
    ],
    { initialEntries: ["/incidents/new"] },
  )

  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )

  return router
}

function fillValidForm() {
  fireEvent.change(screen.getByLabelText("Title"), { target: { value: "  Payment page is slow  " } })
  fireEvent.change(screen.getByLabelText("Description"), { target: { value: "Checkout takes 30 seconds." } })
  fireEvent.change(screen.getByLabelText("Severity"), { target: { value: "High" } })
}

const submit = () => fireEvent.click(screen.getByRole("button", { name: "Create incident" }))

beforeEach(() => {
  vi.resetAllMocks()

  vi.mocked(listTeamMembers).mockResolvedValue({
    items: [
      { id: "a0000000-0000-0000-0000-000000000001", displayName: "Demo Admin", role: "Admin" },
      { id: RESPONDER_ID, displayName: "Demo Responder", role: "Responder" },
      { id: "a0000000-0000-0000-0000-000000000003", displayName: "Demo Viewer", role: "Viewer" },
    ],
    page: 1,
    pageSize: 100,
    totalCount: 3,
    totalPages: 1,
  })

  vi.mocked(getSlaPolicy).mockResolvedValue({
    targets: [
      { severity: "Critical", responseMinutes: 15, resolutionMinutes: 240 },
      { severity: "High", responseMinutes: 60, resolutionMinutes: 480 },
      { severity: "Medium", responseMinutes: 240, resolutionMinutes: 1440 },
      { severity: "Low", responseMinutes: 480, resolutionMinutes: 4320 },
    ],
    note: "demo",
  })
})

describe("NewIncidentPage", () => {
  it("does not offer a Viewer as an assignee", async () => {
    renderPage()

    expect(await screen.findByRole("option", { name: "Demo Responder" })).toBeTruthy()
    expect(screen.queryByRole("option", { name: "Demo Viewer" })).toBeNull()
  })

  it("starts without a severity chosen", () => {
    renderPage()

    expect((screen.getByLabelText("Severity") as HTMLSelectElement).value).toBe("")
  })

  it("shows every problem at once, and sends nothing", async () => {
    renderPage()

    submit()

    expect(await screen.findByText("Title must be at least 5 characters.")).toBeTruthy()
    expect(screen.getByText("Description is required.")).toBeTruthy()
    expect(screen.getByText("Select a severity.")).toBeTruthy()
    expect(createIncident).not.toHaveBeenCalled()
  })

  it("explains the SLA targets of the chosen severity", async () => {
    renderPage()

    expect(screen.getByText("Pick a severity to see its SLA targets.")).toBeTruthy()

    fireEvent.change(screen.getByLabelText("Severity"), { target: { value: "Critical" } })

    expect(await screen.findByText(/first response within 15m, resolution within 4h/)).toBeTruthy()
  })

  it("sends the trimmed values, then goes to the new incident with a success notice", async () => {
    vi.mocked(createIncident).mockResolvedValue(created)
    const router = renderPage()

    fillValidForm()
    submit()

    await waitFor(() =>
      expect(createIncident).toHaveBeenCalledWith({
        title: "Payment page is slow",
        description: "Checkout takes 30 seconds.",
        severity: "High",
        assigneeId: undefined,
      }),
    )
    await waitFor(() => expect(router.state.location.pathname).toBe(`/incidents/${CREATED_ID}`))
    expect(router.state.location.state).toEqual({ notice: "Incident created." })
  })

  it("sends the chosen assignee", async () => {
    vi.mocked(createIncident).mockResolvedValue(created)
    renderPage()

    fillValidForm()
    await screen.findByRole("option", { name: "Demo Responder" })
    fireEvent.change(screen.getByLabelText("Assignee"), { target: { value: RESPONDER_ID } })
    submit()

    await waitFor(() => expect(createIncident).toHaveBeenCalledWith(expect.objectContaining({ assigneeId: RESPONDER_ID })))
  })

  it("disables the button while sending, so a double click cannot create two incidents", async () => {
    vi.mocked(createIncident).mockReturnValue(new Promise(() => {})) // tidak pernah selesai
    renderPage()

    fillValidForm()
    submit()

    const busy = (await screen.findByRole("button", { name: "Creating…" })) as HTMLButtonElement
    expect(busy.disabled).toBe(true)
  })

  it("puts a validation error from the backend under its own field, and stays on the page", async () => {
    vi.mocked(createIncident).mockRejectedValue(
      new ApiError({
        status: 400,
        title: "Validation failed",
        fieldErrors: { assigneeId: ["Assignee must be an active Admin or Responder of your team."] },
      }),
    )
    const router = renderPage()

    fillValidForm()
    submit()

    expect(await screen.findByText("Assignee must be an active Admin or Responder of your team.")).toBeTruthy()
    expect(router.state.location.pathname).toBe("/incidents/new")
  })

  it("shows other errors above the form, and keeps what was typed", async () => {
    vi.mocked(createIncident).mockRejectedValue(
      new ApiError({ status: 0, title: "Cannot reach the server", detail: "Check that the API is running." }),
    )
    renderPage()

    fillValidForm()
    submit()

    expect(await screen.findByText("Check that the API is running.")).toBeTruthy()
    expect((screen.getByLabelText("Description") as HTMLTextAreaElement).value).toBe("Checkout takes 30 seconds.")
  })
})