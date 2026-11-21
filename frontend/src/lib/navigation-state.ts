export interface NoticeState {
    notice: string  
}

export function noticeState(notice: string): NoticeState {
    return { notice }
}

export function readNotice(state: unknown): string | undefined {
  if (typeof state === "object" && state !== null && "notice" in state && typeof state.notice === "string") {
    return state.notice
  }

  return undefined
}