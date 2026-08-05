import type { NavigationItem, NavigationRoute } from './NavigationModel'
import { groupNavigationItems, iconPath } from './NavigationModel'

export function NavigationIcon({ item }: { item: NavigationItem }) {
  return <svg className="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d={iconPath(item.icon)} fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.8" /></svg>
}

export function Sidebar({ items, activeRoute, onNavigate, onCollapse }: {
  items: readonly NavigationItem[]
  activeRoute: NavigationRoute
  onNavigate: (route: NavigationRoute) => void
  onCollapse: () => void
}) {
  return <aside className="sidebar sidebar-expanded" aria-label="Điều hướng chính">
    <div className="sidebar-header"><span className="nav-heading">Không gian làm việc</span><button className="icon-button sidebar-toggle" type="button" aria-label="Thu gọn điều hướng" title="Thu gọn điều hướng" onClick={onCollapse}>‹</button></div>
    {groupNavigationItems(items).map(group => <section className="nav-group" key={group.group} aria-labelledby={`nav-group-${group.group}`}>
      <h2 className="nav-group-label" id={`nav-group-${group.group}`}>{group.label}</h2>
      {group.items.map(item => <button className={`nav-link${activeRoute === item.route ? ' active' : ''}`} type="button" key={item.route}
        aria-current={activeRoute === item.route ? 'page' : undefined} onClick={() => onNavigate(item.route)}>
        <NavigationIcon item={item} /><span>{item.label}</span>
      </button>)}
    </section>)}
    <div className="sidebar-note"><span className="status-mark status-mark-neutral" aria-hidden="true">•</span><span>Chế độ đọc độc lập nhà cung cấp</span></div>
  </aside>
}
