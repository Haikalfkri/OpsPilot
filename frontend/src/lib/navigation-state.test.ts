import { describe, expect, it } from "vitest"
import { noticeState, readNotice } from "@/lib/navigation-state"

describe("navigation state", () => {
  it("round-trips a notice", () => {
    expect(readNotice(noticeState("Incident created."))).toBe("Incident created.")
  })

  it.each([undefined, null, "text", 42, {}, { notice: 5 }, { other: "x" }])("reads no notice from %j", (state) => {
    expect(readNotice(state)).toBeUndefined()
  })
})