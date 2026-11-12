import { cleanup } from "@testing-library/react"
import { afterEach } from "vitest"

// Bersihkan komponen yang digambar sesudah tiap tes, supaya antar-tes tidak saling mengganggu
afterEach(() => {
  cleanup()
})