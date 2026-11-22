import { describe, expect, it } from "vitest"
import {
  describeCounts,
  formatAverageResolution,
  formatTrendDate,
  oldestGeneratedAt,
  parseDashboardDays,
  periodSearchParams,
  severityData,
  statusData,
  sumTrend,
  totalOf,
} from "@/lib/dashboard"

describe("parseDashboardDays", () => {
  it.each([
    ["", 7],
    ["days=7", 7],
    ["days=14", 14],
    ["days=30", 30],
    ["days=90", 90],
  ])("'%s' gives %s days", (query, expected) => {
    expect(parseDashboardDays(new URLSearchParams(query))).toBe(expected)
  })

  it.each(["days=5", "days=365", "days=-7", "days=abc", "days=7abc", "days=", "days=0"])(
    "falls back to 7 days for %s",
    (query) => {
      expect(parseDashboardDays(new URLSearchParams(query))).toBe(7)
    },
  )
})

describe("periodSearchParams", () => {
  it("writes nothing for the default period", () => {
    expect(periodSearchParams(7).toString()).toBe("")
  })

  it("writes the other periods", () => {
    expect(periodSearchParams(30).toString()).toBe("days=30")
  })

  it("round-trips", () => {
    for (const days of [7, 14, 30, 90]) {
      expect(parseDashboardDays(periodSearchParams(days))).toBe(days)
    }
  })
})

describe("formatAverageResolution", () => {
  it.each([
    [null, "—"],
    [0.4, "<1m"],
    [1, "1m"],
    [59.4, "59m"],
    [90, "1h 30m"],
    [89.6, "1h 30m"], // dibulatkan ke menit terdekat
    [1440, "1d"],
    [1500, "1d 1h"],
  ])("%s minutes reads '%s'", (minutes, expected) => {
    expect(formatAverageResolution(minutes)).toBe(expected)
  })
})

describe("formatTrendDate", () => {
  it("shows the UTC day, whatever the time zone of the browser is", () => {
    expect(formatTrendDate("2026-10-30", "en-US")).toBe("Oct 30")
    expect(formatTrendDate("2026-01-01", "en-US")).toBe("Jan 1")
    expect(formatTrendDate("2026-12-31", "en-US")).toBe("Dec 31")
  })
})

describe("chart data", () => {
  it("keeps the order and counts of severities and statuses", () => {
    expect(
      severityData([
        { severity: "Critical", count: 3 },
        { severity: "Low", count: 1 },
      ]),
    ).toEqual([
      { label: "Critical", count: 3 },
      { label: "Low", count: 1 },
    ])

    expect(statusData([{ status: "Open", count: 4 }])).toEqual([{ label: "Open", count: 4 }])
  })

  it("adds up and describes the counts", () => {
    const data = [
      { label: "Critical", count: 3 },
      { label: "High", count: 0 },
      { label: "Low", count: 2 },
    ]

    expect(totalOf(data)).toBe(5)
    expect(totalOf([])).toBe(0)
    expect(describeCounts(data)).toBe("Critical 3, High 0, Low 2")
  })

  it("adds up the trend", () => {
    expect(
      sumTrend([
        { date: "2026-10-29", created: 2, resolved: 1 },
        { date: "2026-10-30", created: 3, resolved: 6 },
      ]),
    ).toEqual({ created: 5, resolved: 7 })

    expect(sumTrend([])).toEqual({ created: 0, resolved: 0 })
  })
})

describe("oldestGeneratedAt", () => {
  it("is null without timestamps", () => {
    expect(oldestGeneratedAt([])).toBeNull()
  })

  it("picks the oldest one", () => {
    expect(oldestGeneratedAt(["2026-10-30T08:00:30Z", "2026-10-30T08:00:00Z", "2026-10-30T08:01:00Z"])).toBe(
      "2026-10-30T08:00:00.000Z",
    )
  })
})