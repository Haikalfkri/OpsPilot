import { useId } from "react"
import { Label } from "@/components/ui/label"

interface FilterSelectProps {
  label: string
  value: string
  options: { value: string; label: string }[]
  onChange: (value: string) => void
  allLabel?: string // kalau diisi, ada pilihan "semua" dengan nilai kosong
}

export function FilterSelect({ label, value, options, onChange, allLabel }: FilterSelectProps) {
  // useId menghasilkan id unik, supaya label terhubung dengan kolomnya (penting untuk pembaca layar)
  const id = useId()

  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <select
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
      >
        {allLabel !== undefined && <option value="">{allLabel}</option>}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  )
}