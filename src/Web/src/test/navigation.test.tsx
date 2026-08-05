import {
  canAccessNavigationItem,
  deriveRouteAccess,
  firstPermittedNavigationRoute,
  groupNavigationItems,
  isNavigationRouteAvailable,
  navigationItems,
  visibleNavigationItems,
} from '../components/navigation/NavigationModel'

export function runNavigationChecks(): string[] {
  const failures: string[] = []
  const engineerAccess = deriveRouteAccess({ roleMode: 'Engineer', setupRequired: false })
  if (!engineerAccess.dashboard || !engineerAccess.configuration || !engineerAccess.simulator || !engineerAccess.telemetry)
    failures.push('an authorized scope must confirm the operational read routes')
  if (engineerAccess.audit) failures.push('scope presence must never imply the AUDIT_READ capability')
  if (engineerAccess.setup) failures.push('setup must require the workspace landing requirement')
  if (!deriveRouteAccess({ roleMode: 'Administrator', setupRequired: true }).setup)
    failures.push('setup must be available when the workspace requires it and scope is confirmed')
  const readOnlyAccess = deriveRouteAccess({ roleMode: 'ReadOnly', setupRequired: true })
  for (const route of ['dashboard', 'configuration', 'simulator', 'telemetry', 'setup'] as const) {
    if (readOnlyAccess[route]) failures.push(`${route} must fail closed without an authorized scope`)
  }
  const unconfirmed = deriveRouteAccess({ setupRequired: false })
  if (Object.values(unconfirmed).some(value => value))
    failures.push('route availability must fail closed before workspace status confirms scope')
  if (!deriveRouteAccess({ capabilities: ['AUDIT_READ'], roleMode: 'Engineer', setupRequired: false }).audit)
    failures.push('AUDIT_READ capability must authorize the audit route')
  if (deriveRouteAccess({ capabilities: [], roleMode: 'Engineer', setupRequired: false }).audit)
    failures.push('audit must fail closed with an empty capability set')
  if (deriveRouteAccess({ roleMode: 'Engineer', setupRequired: false }).audit)
    failures.push('audit must fail closed when capabilities are absent')
  if (isNavigationRouteAvailable('audit', unconfirmed))
    failures.push('audit must be unreachable before capabilities are confirmed')
  if (isNavigationRouteAvailable('dashboard', unconfirmed))
    failures.push('dashboard must be unreachable before workspace status confirms scope')
  if (!isNavigationRouteAvailable('dashboard', engineerAccess))
    failures.push('dashboard must be reachable for an authorized scope')
  if (firstPermittedNavigationRoute(engineerAccess) !== 'dashboard')
    failures.push('brand/home navigation must prefer the permitted dashboard')
  if (firstPermittedNavigationRoute(unconfirmed) !== undefined)
    failures.push('no route may be offered before any scope is confirmed')
  const auditAccess = deriveRouteAccess({ capabilities: ['AUDIT_READ'], roleMode: 'Engineer', setupRequired: false })
  if (!canAccessNavigationItem(navigationItems.find(item => item.route === 'audit')!, auditAccess))
    failures.push('audit navigation item must follow the capability-derived access')
  if (visibleNavigationItems(auditAccess).some(item => item.route !== 'audit' && !auditAccess[item.route]))
    failures.push('visible navigation must match route access exactly')
  if (visibleNavigationItems(readOnlyAccess).some(item => item.route !== 'audit'))
    failures.push('a scope-less session must not surface operational navigation')
  if (groupNavigationItems(navigationItems).length < 3) failures.push('navigation must use operational groups')
  return failures
}
