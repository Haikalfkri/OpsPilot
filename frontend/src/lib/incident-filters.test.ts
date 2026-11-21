import { describe, expect, it } from "vitest"
import {
  PAGE_SIZE,
  endOfDayIso,
  hasActiveFilters,
  parseFilters,
  startOfDayIso,
  toApiParams,
  toSearchParams,
} from "@/lib/incident-filters"

const parse = (query: string) => parseFilters(new URLSearchParams(query))

describe("parseFilters", () => {
  it("uses the defaults for an empty URL", () => {
    expect(parse("")).toEqual({
      page: 1,
      status: undefined,
      severity: undefined,
      assigneeId: undefined,
      from: undefined,
      to: undefined,
      search: "",
      sort: "newest",
    })
  })

  it("reads every filter from the URL", () => {
    const filters = parse(
      "status=Open&severity=High&assignee=a0000000-0000-0000-0000-000000000002&from=2026-10-01&to=2026-10-31&q=disk&sort=due&page=3",
    )

    expect(filters).toMatchObject({
      status: "Open",
      severity: "High",
      assigneeId: "a0000000-0000-0000-0000-000000000002",
      from: "2026-10-01",
      to: "2026-10-31",
      search: "disk",
      sort: "due",
      page: 3,
    })
  })

  it.each(["status=Bogus", "severity=urgent", "assignee=not-a-guid", "from=yesterday", "to=2026-99-99", "sort=hack"])(
    "ignores the invalid value in %s",
    (query) => {
      expect(parse(query)).toEqual(parse(""))
    },
  )

  it.each(["page=0", "page=-3", "page=abc", "page=1.5x"])("falls back to page 1 for %s", (query) => {
    expect(parse(query).page).toBe(1)
  })

  it("trims and limits the search text to 100 characters", () => {
    expect(parse("q=%20%20disk%20%20").search).toBe("disk")
    expect(parse(`q=${"a".repeat(150)}`).search).toHaveLength(100)
  })
})

describe("toSearchParams", () => {
  it("writes nothing for the default filters", () => {
    expect(toSearchParams(parse("")).toString()).toBe("")
  })

  it("round-trips: parse(toSearchParams(x)) equals x", () => {
    const original = parse("status=Investigating&severity=Critical&q=disk&sort=severity&page=2&from=2026-10-01")

    expect(parseFilters(toSearchParams(original))).toEqual(original)
  })

  it("leaves out page 1 and the default sort", () => {
    const text = toSearchParams({ ...parse(""), status: "Open", page: 1, sort: "newest" }).toString()

    expect(text).toBe("status=Open")
  })
})

describe("toApiParams", () => {
  it("always sends the page size and a sort", () => {
    expect(toApiParams(parse(""))).toMatchObject({
      page: 1,
      pageSize: PAGE_SIZE,
      sortBy: "createdAt",
      sortDirection: "desc",
    })
  })

  it.each([
    ["oldest", "createdAt", "asc"],
    ["updated", "updatedAt", "desc"],
    ["severity", "severity", "desc"],
    ["due", "dueAt", "asc"],
  ])("maps the %s sort to %s %s", (sort, sortBy, direction) => {
    expect(toApiParams(parse(`sort=${sort}`))).toMatchObject({ sortBy, sortDirection: direction })
  })

  it("passes the filters through, and omits the empty ones", () => {
    const params = toApiParams(parse("status=Open&q=disk"))

    expect(params).toMatchObject({ status: "Open", search: "disk" })
    expect(params.severity).toBeUndefined()
    expect(params.createdFrom).toBeUndefined()
  })

  it("turns local dates into the start and end of that day, as ISO timestamps", () => {
    const params = toApiParams(parse("from=2026-10-01&to=2026-10-31"))

    expect(params.createdFrom).toBe(startOfDayIso("2026-10-01"))
    expect(params.createdTo).toBe(endOfDayIso("2026-10-31"))
    expect(params.createdFrom!.endsWith("Z")).toBe(true)
  })
})

describe("startOfDayIso and endOfDayIso", () => {
  it("are local midnight and the last millisecond of the same local day", () => {
    const start = new Date(startOfDayIso("2026-10-30"))
    const end = new Date(endOfDayIso("2026-10-30"))

    expect([start.getFullYear(), start.getMonth(), start.getDate(), start.getHours(), start.getMinutes()]).toEqual([
      2026, 9, 30, 0, 0,
    ])
    expect([end.getDate(), end.getHours(), end.getMinutes(), end.getSeconds(), end.getMilliseconds()]).toEqual([
      30, 23, 59, 59, 999,
    ])
  })
})

describe("hasActiveFilters", () => {
  it("is false for the defaults, even with a page or sort", () => {
    expect(hasActiveFilters(parse(""))).toBe(false)
    expect(hasActiveFilters(parse("page=3&sort=due"))).toBe(false)
  })

  it.each(["status=Open", "severity=Low", "q=disk", "from=2026-10-01", "to=2026-10-31"])("is true for %s", (query) => {
    expect(hasActiveFilters(parse(query))).toBe(true)
  })
})