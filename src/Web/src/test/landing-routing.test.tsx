import { resolveLanding, routeFromPath } from '../components/navigation/NavigationModel'
import { allIncludedRoutes, permittedOperatorRoutes, routeFixture } from './route-fixtures'

export function runLandingRoutingChecks(): string[] {
  const failures: string[] = []
  if (resolveLanding({ deepLink: '/telemetry', enabledRoutes: permittedOperatorRoutes, dashboardPermitted: true }).kind !== 'route') failures.push('permitted deep link must win')
  if (resolveLanding({ deepLink: '/audit', enabledRoutes: permittedOperatorRoutes, dashboardPermitted: true }).kind !== 'safe-forbidden') failures.push('unauthorized deep link must be safe')
  if (resolveLanding({ enabledRoutes: permittedOperatorRoutes, dashboardPermitted: true }).kind !== 'route') failures.push('first permitted capability must resolve')
  if (resolveLanding({ enabledRoutes: [], dashboardPermitted: false }).kind !== 'safe-no-authorized-capability') failures.push('no capability must not fall through to Dashboard')
  if (routeFromPath('/telemetry') !== 'telemetry' || routeFromPath('/not-a-route')) failures.push('route fixture must reject unknown paths')
  if (routeFixture('/audit', false).permitted) failures.push('fixture must preserve forbidden outcome')
  if (allIncludedRoutes.length !== 6) failures.push('included route set must remain unchanged')
  return failures
}
