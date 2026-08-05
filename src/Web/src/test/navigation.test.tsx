import {
  canAccessNavigationItem,
  firstPermittedNavigationRoute,
  groupNavigationItems,
  isNavigationRouteAvailable,
  navigationItems,
  visibleNavigationItems,
} from '../components/navigation/NavigationModel'

export function runNavigationChecks(): string[] {
  const failures: string[] = []
  if (groupNavigationItems(navigationItems).length < 3) failures.push('navigation must use operational groups')
  if (visibleNavigationItems({ capabilities: [] }).some(item => item.route === 'audit')) failures.push('AUDIT_READ must remain permission filtered')
  if (!visibleNavigationItems({}).some(item => item.route === 'dashboard')) failures.push('legacy session shape must retain server-enforced dashboard visibility')
  if (visibleNavigationItems({}).some(item => item.route === 'audit')) failures.push('capability-gated navigation must fail closed when capabilities are absent')
  if (canAccessNavigationItem(navigationItems.find(item => item.route === 'audit')!, {})) failures.push('audit must fail closed without a capability collection')
  if (!isNavigationRouteAvailable('dashboard', {}, false)) failures.push('dashboard must stay reachable without capability metadata')
  if (isNavigationRouteAvailable('audit', {}, false)) failures.push('audit must be unreachable without capabilities')
  if (isNavigationRouteAvailable('audit', { capabilities: [] }, false)) failures.push('audit must be unreachable with an empty capability set')
  if (isNavigationRouteAvailable('setup', { capabilities: [] }, false)) failures.push('setup must be unreachable when the workspace does not require it')
  if (!isNavigationRouteAvailable('setup', { capabilities: [] }, true)) failures.push('setup must be reachable when the workspace requires it')
  if (firstPermittedNavigationRoute({}, false) !== 'dashboard') failures.push('brand/home navigation must prefer the permitted dashboard')
  return failures
}
