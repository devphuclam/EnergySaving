import { useEffect, useRef, type ReactNode } from 'react'

function focusables(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>('button, a, input, textarea, select, [tabindex]:not([tabindex="-1"])'))
    .filter(element => !element.hasAttribute('disabled'))
}

export function Drawer({ open, title, onClose, children, labelledBy = 'drawer-title' }: { open: boolean; title: string; onClose: () => void; children: ReactNode; labelledBy?: string }) {
  const ref = useRef<HTMLElement>(null)
  const previous = useRef<HTMLElement | null>(null)
  useEffect(() => {
    if (!open) return
    previous.current = document.activeElement as HTMLElement | null
    const drawer = ref.current
    if (!drawer) return
    focusables(drawer)[0]?.focus()
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { event.preventDefault(); onClose(); return }
      if (event.key !== 'Tab') return
      const current = focusables(drawer)
      if (current.length === 0) return
      const first = current[0]
      const last = current[current.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => { document.removeEventListener('keydown', handleKeyDown); previous.current?.focus() }
  }, [onClose, open])
  if (!open) return null
  return <div className="drawer-scrim" role="presentation" onMouseDown={onClose}><aside className="detail-drawer" ref={ref} role="dialog" aria-modal="true" aria-labelledby={labelledBy} onMouseDown={event => event.stopPropagation()}>
    <div className="drawer-header"><h2 id={labelledBy}>{title}</h2><button className="icon-button" type="button" aria-label="Đóng bảng chi tiết" title="Đóng bảng chi tiết" onClick={onClose}>×</button></div>{children}
  </aside></div>
}
