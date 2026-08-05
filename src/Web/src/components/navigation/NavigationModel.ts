export type NavigationRoute = 'setup' | 'dashboard' | 'configuration' | 'simulator' | 'telemetry' | 'audit'
export type NavigationGroup = 'monitoring' | 'configuration' | 'governance' | 'setup'

/**
 * Workspace mode returned by the server's operational-workspace status. It is the server's own
 * computed scope-presence signal: 'Administrator' and 'Engineer' confirm an authorized Site or
 * Area scope, 'ReadOnly' means no authorized scope. It is never treated as a role grant here;
 * only scope presence is consumed, mirroring the server's hasAuthorizedScope rule.
 */
export type WorkspaceRoleMode = 'Administrator' | 'Engineer' | 'ReadOnly'

/**
 * Effective per-route availability derived only from authoritative server data. The server remains
 * the authorization authority; this model never infers a permission from a role name and never
 * invents a capability code. Absent input fails closed.
 */
export type RouteAccess = Record<NavigationRoute, boolean>

export type RouteAccessInput = {
  capabilities?: string[]
  roleMode?: WorkspaceRoleMode
  setupRequired: boolean
}

/** Fail-closed derivation: no input confirms a scope or a capability, so no route opens. */
export function deriveRouteAccess(input: RouteAccessInput): RouteAccess {
  const hasAuthorizedScope = input.roleMode === 'Administrator' || input.roleMode === 'Engineer'
  return {
    dashboard: hasAuthorizedScope,
    configuration: hasAuthorizedScope,
    simulator: hasAuthorizedScope,
    telemetry: hasAuthorizedScope,
    audit: input.capabilities !== undefined && input.capabilities.includes('AUDIT_READ'),
    setup: hasAuthorizedScope && input.setupRequired,
  }
}

export type NavigationItem = {
  route: NavigationRoute
  label: string
  shortLabel: string
  group: NavigationGroup
  groupLabel: string
  description: string
  capability?: string
  icon: 'dashboard' | 'telemetry' | 'configuration' | 'simulator' | 'audit' | 'setup'
}

export const navigationItems: readonly NavigationItem[] = [
  { route: 'dashboard', label: 'Vận hành', shortLabel: 'VH', group: 'monitoring', groupLabel: 'Vận hành', description: 'Tóm tắt tình trạng và ngoại lệ', icon: 'dashboard' },
  { route: 'telemetry', label: 'Dữ liệu & tình trạng', shortLabel: 'DL', group: 'monitoring', groupLabel: 'Vận hành', description: 'Measurement, chất lượng và độ mới', icon: 'telemetry' },
  { route: 'configuration', label: 'Cấu hình', shortLabel: 'CH', group: 'configuration', groupLabel: 'Cấu hình', description: 'Phạm vi và cấu hình được cấp', icon: 'configuration' },
  { route: 'simulator', label: 'Mô phỏng', shortLabel: 'MP', group: 'configuration', groupLabel: 'Cấu hình', description: 'Workspace Simulator hiện có', icon: 'simulator' },
  { route: 'audit', label: 'Nhật ký', shortLabel: 'NK', group: 'governance', groupLabel: 'Quản trị / Hệ thống', description: 'Bằng chứng thay đổi được cấp quyền', capability: 'AUDIT_READ', icon: 'audit' },
  { route: 'setup', label: 'Thiết lập', shortLabel: 'TL', group: 'setup', groupLabel: 'Thiết lập', description: 'Hoàn tất chuỗi vận hành được cấp', icon: 'setup' },
]

const routePriority: readonly NavigationRoute[] = ['configuration', 'simulator', 'telemetry', 'audit', 'setup']

export type LandingResolution =
  | { kind: 'route'; route: NavigationRoute; reason: 'deep-link' | 'priority' | 'dashboard-fallback' }
  | { kind: 'safe-forbidden'; nextRoute?: NavigationRoute }
  | { kind: 'safe-no-authorized-capability' }
  | { kind: 'blocked' }

export type LandingResolutionInput = {
  deepLink?: string
  enabledRoutes: readonly NavigationRoute[]
  dashboardPermitted: boolean
  setupRequired?: boolean
}

export function iconPath(icon: NavigationItem['icon']): string {
  switch (icon) {
    case 'dashboard': return 'M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z'
    case 'telemetry': return 'M4 17l4-5 3 3 5-7 4 5M4 20h16'
    case 'configuration': return 'M5 5h14v14H5zM8 9h8M8 13h8M8 17h5'
    case 'simulator': return 'M6 4h12v16H6zM9 8h6M9 12h6M9 16h3'
    case 'audit': return 'M7 3h10v18H7zM10 7h4M10 11h4M10 15h4'
    case 'setup': return 'M12 3l2.2 4.5L19 8l-3.5 3.5.8 4.8-4.3-2.2-4.3 2.2.8-4.8L5 8l4.8-.5z'
  }
}

export function routeFromPath(pathname: string): NavigationRoute | undefined {
  const route = pathname.replace(/^\//, '').split('/')[0] as NavigationRoute
  return navigationItems.some(item => item.route === route) ? route : undefined
}

export function routeLabel(route: NavigationRoute): string {
  return navigationItems.find(item => item.route === route)?.label ?? 'Không gian làm việc'
}

export function canAccessNavigationItem(item: NavigationItem, access: RouteAccess): boolean {
  return access[item.route]
}

export function visibleNavigationItems(access: RouteAccess): NavigationItem[] {
  return navigationItems.filter(item => access[item.route])
}

/**
 * Canonical route availability predicate used by every navigation entry path (deep link, root
 * landing, brand, sidebar, rail, drawer, popstate, programmatic callbacks, session restoration).
 * Availability comes only from the server-derived RouteAccess: an unconfirmed or absent access
 * model fails every route closed.
 */
export function isNavigationRouteAvailable(route: NavigationRoute, access: RouteAccess): boolean {
  return access[route]
}

/**
 * First permitted destination for home/brand navigation and for the safe forbidden recovery
 * action. Prefers Dashboard, then the shared priority order, then Setup when it is required.
 */
export function firstPermittedNavigationRoute(access: RouteAccess): NavigationRoute | undefined {
  if (access.dashboard) return 'dashboard'
  for (const route of routePriority) {
    if (access[route]) return route
  }
  return undefined
}

export function resolveLanding(input: LandingResolutionInput): LandingResolution {
  const enabled = new Set(input.enabledRoutes)
  const deepLinkRoute = input.deepLink ? routeFromPath(input.deepLink) : undefined
  if (input.deepLink && (!deepLinkRoute || !enabled.has(deepLinkRoute))) {
    return { kind: 'safe-forbidden', nextRoute: routePriority.find(route => enabled.has(route)) ?? (input.dashboardPermitted ? 'dashboard' : undefined) }
  }
  if (deepLinkRoute && enabled.has(deepLinkRoute)) return { kind: 'route', route: deepLinkRoute, reason: 'deep-link' }
  const priorityRoute = routePriority.find(route => enabled.has(route) && (route !== 'setup' || input.setupRequired))
  if (priorityRoute) return { kind: 'route', route: priorityRoute, reason: 'priority' }
  if (input.dashboardPermitted && enabled.has('dashboard')) return { kind: 'route', route: 'dashboard', reason: 'dashboard-fallback' }
  return { kind: 'safe-no-authorized-capability' }
}

export function groupNavigationItems(items: readonly NavigationItem[]): Array<{ group: NavigationGroup; label: string; items: NavigationItem[] }> {
  const groups: NavigationGroup[] = ['monitoring', 'configuration', 'governance', 'setup']
  return groups.map(group => {
    const groupItems = items.filter(item => item.group === group)
    return { group, label: groupItems[0]?.groupLabel ?? '', items: groupItems }
  }).filter(group => group.items.length > 0)
}
