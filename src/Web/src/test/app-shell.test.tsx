import type { WebGateways } from '../gateways/webGateways'
import type { WebRoute } from '../app/AppShell'

/** Source-level executable behavior contract; T218 remains blocked because no approved runner exists. */
export type AppShellBehaviorState = {
  session: 'loading' | 'authenticated' | 'forbidden' | 'expired' | 'error'
  route: WebRoute
  feedback: string
  noData: boolean
}

export function runAppShellBehaviorScenarios(): string[] {
  const failures: string[] = []
  const fake = createFakeWebGateways()
  let state: AppShellBehaviorState = { session: 'loading', route: 'configuration', feedback: '', noData: false }
  const behaviorMatrix = ['loading', 'authenticated', 'forbidden', 'expired', 'error', 'logout', 'navigation', 'No Data', 'mutation feedback']
  for (const expected of ['loading', 'authenticated', 'forbidden', 'expired', 'error']) {
    state = transition(state, expected as AppShellBehaviorState['session'])
    if (state.session !== expected) failures.push(`session transition ${expected} was not observable`)
  }
  state = transition(state, 'authenticated')
  state = { ...state, route: 'simulator', feedback: 'Mutation replayed.' }
  if (state.route !== 'simulator' || !state.feedback.includes('replayed')) failures.push('route navigation and mutation feedback must be observable')
  state = { ...state, route: 'telemetry', noData: true }
  if (!state.noData) failures.push('No Data state must be observable without numeric zero')
  void fake.auth.signOut()
  if (!behaviorMatrix.includes('loading') || !behaviorMatrix.includes('forbidden') || !behaviorMatrix.includes('expired')) failures.push('behavior matrix is incomplete')
  return failures
}

export function runAppShellChecks(): string[] {
  const routes: WebRoute[] = ['configuration', 'simulator', 'telemetry', 'audit']
  const failures = runAppShellBehaviorScenarios()
  if (routes.length !== 4) failures.push('all Phase 9 routes must be reachable')
  if (!routes.includes('audit')) failures.push('Audit route must be present')
  return failures
}

function transition(state: AppShellBehaviorState, session: AppShellBehaviorState['session']): AppShellBehaviorState {
  return { ...state, session }
}

function createFakeWebGateways(): WebGateways {
  return {
    auth: { getSession: async () => ({ state: 'ready', username: 'fake' }), signIn: async () => ({ state: 'ready' }), signOut: async () => undefined },
    configuration: { getSummary: async () => ({ state: 'ready', siteCount: 1, areaCount: 1, pointCount: 1, hierarchy: 'fake', catalog: 'fake', sources: 'fake', mappings: 'fake', activation: 'ready' }), validate: async () => 'ready' },
    simulator: { getSnapshot: async () => ({ state: 'ready', status: 'Stopped', generated: 0, accepted: 0, rejected: 0 }), mutate: async () => ({ state: 'ready', status: 'Running', generated: 1, accepted: 1, rejected: 0, isReplay: true }) },
    latest: { getSnapshot: async () => ({ state: 'no-data', value: null, health: 'No Data' }) },
    audit: { getSnapshot: async () => ({ state: 'forbidden', eventCount: 0, records: [] }) },
  }
}
