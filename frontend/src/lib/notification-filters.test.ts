import { describe, expect, it } from "vitest"
import { NOTIFICATIONS_PAGE_SIZE } from "@/lib/constants"
import {
  parseNotificationFilters,
  toNotificationApiParams,
  toNotificationSearchParams,
} from "@/lib/notification-filters"

const parse = (query: string) => parseNotificationFilters(new URLSearchParams(query))

describe("parseNotificationFilters", () => {
  it("defaults to all notifications on page 1", () => {
    expect(parse("")).toEqual({ filter: "all", page: 1 })
  })

  it("reads the unread filter and the page", () => {
    expect(parse("filter=unread&page=3")).toEqual({ filter: "unread", page: 3 })
  })

  it.each(["filter=bogus", "filter=UNREAD", "filter="])("ignores the invalid value in %s", (query) => {
    expect(parse(query).filter).toBe("all")
  })

  it.each(["page=0", "page=-2", "page=abc"])("falls back to page 1 for %s", (query) => {
    expect(parse(query).page).toBe(1)
  })
})

describe("toNotificationSearchParams", () => {
  it("writes nothing for the defaults", () => {
    expect(toNotificationSearchParams({ filter: "all", page: 1 }).toString()).toBe("")
  })

  it("round-trips", () => {
    const original = { filter: "unread", page: 2 } as const

    expect(parseNotificationFilters(toNotificationSearchParams(original))).toEqual(original)
  })
})

describe("toNotificationApiParams", () => {
  it("sends no isRead for 'all'", () => {
    const params = toNotificationApiParams({ filter: "all", page: 1 })

    expect(params).toEqual({ page: 1, pageSize: NOTIFICATIONS_PAGE_SIZE, isRead: undefined })
  })

  it("asks only for unread notifications for 'unread'", () => {
    expect(toNotificationApiParams({ filter: "unread", page: 2 })).toMatchObject({ page: 2, isRead: false })
  })
})