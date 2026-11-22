import { useState } from "react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

interface SearchBoxProps {
  value: string
  label: string
  placeholder: string
  onSearch: (text: string) => void
}

export function SearchBox({ value, label, placeholder, onSearch }: SearchBoxProps) {
  const [text, setText] = useState(value)
  const [lastValue, setLastValue] = useState(value)

  if (value !== lastValue) {
    setLastValue(value)
    setText(value)
  }

  return (
    <form
      role="search"
      className="flex gap-2"
      onSubmit={(event) => {
        event.preventDefault()
        onSearch(text.trim())
      }}
    >
      <Input
        type="search"
        aria-label={label}
        placeholder={placeholder}
        maxLength={100}
        value={text}
        onChange={(event) => setText(event.target.value)}
      />
      <Button type="submit" variant="secondary">
        Search
      </Button>
    </form>
  )
}