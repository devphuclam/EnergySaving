import { useEffect, useId, type ReactNode } from 'react'

type GuardEntry = { message: string }
const guards = new Map<string, GuardEntry>()
const listeners = new Set<() => void>()

function emit() {
  for (const listener of listeners) listener()
}

/** Module-level registry so the shell can block navigation while any form is dirty. */
export function subscribeUnsavedChanges(listener: () => void): () => void {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}

export function hasUnsavedChanges(): boolean {
  return guards.size > 0
}

export function unsavedChangesMessage(): string | undefined {
  return guards.values().next().value?.message
}

export function registerUnsavedChange(id: string, message: string): void {
  guards.set(id, { message })
  emit()
}

export function clearUnsavedChange(id: string): void {
  guards.delete(id)
  emit()
}

export function UnsavedChangesGuard({ when, message = 'Bạn có thay đổi chưa lưu. Hãy lưu hoặc hủy trước khi rời trang.', children }: { when: boolean; message?: string; children?: ReactNode }) {
  const id = useId()
  useEffect(() => {
    if (when) registerUnsavedChange(id, message)
    else clearUnsavedChange(id)
    return () => clearUnsavedChange(id)
  }, [id, message, when])
  return <>{when && <p className="unsaved-warning" role="status"><span aria-hidden="true">!</span>{message}</p>}{children}</>
}
