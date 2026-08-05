import { useEffect, useRef, type ReactNode } from 'react'
import type { NavigationItem, NavigationRoute } from './NavigationModel'
import { NavigationIcon, Sidebar } from './Sidebar'
import { groupNavigationItems } from './NavigationModel'

function focusables(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>('button, a, input, select, textarea, [tabindex]:not([tabindex="-1"])')).filter(element => !element.hasAttribute('disabled'))
}

export function NavigationDrawer({ open, items, activeRoute, onNavigate, onClose, children }: {
  open: boolean
  items: readonly NavigationItem[]
  activeRoute: NavigationRoute
  onNavigate: (route: NavigationRoute) => void
  onClose: () => void
  children?: ReactNode
}) {
  const drawerRef = useRef<HTMLElement>(null)
  const openerRef = useRef<HTMLElement | null>(null)
  useEffect(() => {
    if (!open) return
    openerRef.current = document.activeElement as HTMLElement | null
    const drawer = drawerRef.current
    if (!drawer) return
    const drawerElement: HTMLElement = drawer
    const list = focusables(drawerElement)
    list[0]?.focus()
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') { event.preventDefault(); onClose(); return }
      if (event.key !== 'Tab') return
      const current = focusables(drawerElement)
      if (current.length === 0) return
      const first = current[0]
      const last = current[current.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => { document.removeEventListener('keydown', handleKeyDown); openerRef.current?.focus() }
  }, [open, onClose])
  if (!open) return null
  return <div className="drawer-scrim" role="presentation" onMouseDown={onClose}>
    <aside className="navigation-drawer" ref={drawerRef} aria-label="Điều hướng mở rộng" aria-modal="true" role="dialog" onMouseDown={event => event.stopPropagation()}>
      <div className="drawer-header"><strong>Điều hướng</strong><button className="icon-button" type="button" aria-label="Đóng điều hướng" title="Đóng điều hướng" onClick={onClose}>×</button></div>
      {groupNavigationItems(items).map(group => <section className="nav-group" key={group.group}>
        <h2 className="nav-group-label">{group.label}</h2>
        {group.items.map(item => <button className={`nav-link${activeRoute === item.route ? ' active' : ''}`} type="button" key={item.route}
          aria-current={activeRoute === item.route ? 'page' : undefined} onClick={() => { onNavigate(item.route); onClose() }}>
          <NavigationIcon item={item} /><span>{item.label}</span>
        </button>)}
      </section>)}
      {children}
    </aside>
  </div>
}

// Keep these imports part of the module's public navigation vocabulary for consumers that need a
// single file map without coupling to the shell implementation.
export { Sidebar }
