const dateTimeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" })

export function formatDateTime(iso: string): string {
  return dateTimeFormat.format(new Date(iso))
}

export function isPast(iso: string, now: Date = new Date()): boolean {
  return new Date(iso).getTime() < now.getTime()
}

function formatDuration(totalMinutes: number): string {
  const days = Math.floor(totalMinutes / 1440)
  const hours = Math.floor((totalMinutes % 1440) / 60)
  const minutes = totalMinutes % 60

  if (days > 0) return hours > 0 ? `${days}d ${hours}h` : `${days}d`
  if (hours > 0) return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`
  return `${minutes}m`
}

// "in 3h 20m" untuk waktu yang akan datang, "2h 5m ago" untuk yang sudah lewat
export function formatRelative(iso: string, now: Date = new Date()): string {
  const diffMs = new Date(iso).getTime() - now.getTime()
  const minutes = Math.round(Math.abs(diffMs) / 60_000)

  if (minutes < 1) return "now"

  return diffMs >= 0 ? `in ${formatDuration(minutes)}` : `${formatDuration(minutes)} ago`
}