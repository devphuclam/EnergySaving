import type { NavigationItem, NavigationRoute } from './NavigationModel'
import { NavigationIcon } from './Sidebar'

export function Rail({ items, activeRoute, onNavigate, onExpand }: {
  items: readonly NavigationItem[]
  activeRoute: NavigationRoute
  onNavigate: (route: NavigationRoute) => void
  onExpand: () => void
}) {
  return <aside className="sidebar sidebar-rail" aria-label="Điều hướng rút gọn">
    <button className="icon-button rail-toggle" type="button" aria-label="Mở điều hướng" title="Mở điều hướng" onClick={onExpand}>›</button>
    <div className="rail-items">{items.map(item => <button className={`rail-link${activeRoute === item.route ? ' active' : ''}`} type="button" key={item.route}
      aria-label={item.label} title={`${item.label}: ${item.description}`} aria-current={activeRoute === item.route ? 'page' : undefined} onClick={() => onNavigate(item.route)}>
      <NavigationIcon item={item} /><span className="sr-only">{item.label}</span>
    </button>)}</div>
  </aside>
}
