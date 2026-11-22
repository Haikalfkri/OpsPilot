import { QueryClient, QueryClientProvider, useQuery } from "@tanstack/react-query"
import { fireEvent, render, screen } from "@testing-library/react"
import { describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/errors"
import { QueryState } from "@/components/QueryState"

// Komponen kecil yang menjalankan query sungguhan dengan fungsi palsu
function Demo({ fetcher }: { fetcher: () => Promise<string> }) {
  const query = useQuery({ queryKey: ["demo"], queryFn: fetcher })

  return (
    <QueryState query={query} errorTitle="Demo failed">
      {(value) => <p>value: {value}</p>}
    </QueryState>
  )
}

function renderDemo(fetcher: () => Promise<string>) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

  render(
    <QueryClientProvider client={client}>
      <Demo fetcher={fetcher} />
    </QueryClientProvider>,
  )
}

describe("QueryState", () => {
  it("shows a placeholder while loading", () => {
    renderDemo(() => new Promise(() => {})) // tidak pernah selesai

    expect(document.querySelector('[aria-busy="true"]')).not.toBeNull()
    expect(screen.queryByText(/value:/)).toBeNull()
  })

  it("shows the content once the data arrives", async () => {
    renderDemo(() => Promise.resolve("hello"))

    expect(await screen.findByText("value: hello")).toBeTruthy()
  })

  it("shows the error with a Try again button that loads it again", async () => {
    const fetcher = vi
      .fn<() => Promise<string>>()
      .mockRejectedValueOnce(new ApiError({ status: 500, title: "Server exploded" }))
      .mockResolvedValue("recovered")
    renderDemo(fetcher)

    expect(await screen.findByText("Demo failed")).toBeTruthy()
    expect(screen.getByText("Server exploded")).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Try again" }))

    expect(await screen.findByText("value: recovered")).toBeTruthy()
  })
})