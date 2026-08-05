import type { AuthSession } from '../gateways/webGateways'
import type { NavigationRoute } from '../components/navigation/NavigationModel'

export const allIncludedRoutes: readonly NavigationRoute[] = ['dashboard', 'telemetry', 'configuration', 'simulator', 'audit', 'setup']
export const permittedOperatorRoutes: readonly NavigationRoute[] = ['dashboard', 'telemetry', 'configuration', 'simulator']

export function sessionFixture(capabilities?: string[]): AuthSession {
  return { state: 'ready', username: 'operator', scopeLabel: 'Site được cấp', capabilities }
}

export function routeFixture(path: string, permitted = true): { path: string; permitted: boolean } {
  return { path, permitted }
}
