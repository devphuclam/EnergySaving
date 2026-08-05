import { useEffect, useRef, type ReactNode } from 'react'

function focusables(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>('button, a, input, textarea, select, [tabindex]:not([tabindex="-1"])'))
    .filter(element => !element.hasAttribute('disabled'))
}

export function ConfirmDialog({ open, title, description, confirmLabel = 'Xác nhận', cancelLabel = 'Hủy', onConfirm, onCancel, children }: {
  open: boolean; title: string; description: string; confirmLabel?: string; cancelLabel?: string; onConfirm: () => void; onCancel: () => void; children?: ReactNode
}) {
  const ref = useRef<HTMLDivElement>(null)
  const previous = useRef<HTMLElement | null>(null)
  useEffect(() => {
    if (!open) return
    previous.current = document.activeElement as HTMLElement | null
    const dialog = ref.current
    if (!dialog) return
    const first = focusables(dialog)[0]
    first?.focus()
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { event.preventDefault(); onCancel(); return }
      if (event.key !== 'Tab') return
      const current = focusables(dialog)
      if (current.length === 0) return
      const firstFocusable = current[0]
      const lastFocusable = current[current.length - 1]
      if (event.shiftKey && document.activeElement === firstFocusable) { event.preventDefault(); lastFocusable.focus() }
      else if (!event.shiftKey && document.activeElement === lastFocusable) { event.preventDefault(); firstFocusable.focus() }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => { document.removeEventListener('keydown', handleKeyDown); previous.current?.focus() }
  }, [onCancel, open])
  if (!open) return null
  return <div className="dialog-scrim"><div className="dialog" ref={ref} role="dialog" aria-modal="true" aria-labelledby="dialog-title" aria-describedby="dialog-description">
    <h2 id="dialog-title">{title}</h2><p id="dialog-description">{description}</p>{children}<div className="dialog-actions"><button className="button button-secondary" type="button" onClick={onCancel}>{cancelLabel}</button><button className="button button-danger" type="button" onClick={onConfirm}>{confirmLabel}</button></div>
  </div></div>
}
