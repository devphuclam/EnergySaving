import { initialAppShellState, transitionAppShell } from '../app/AppShell'

export function runRouteTitleFocusChecks(): string[] {
  const failures: string[] = []
  const next = transitionAppShell(initialAppShellState, { type: 'landing', resolution: { kind: 'route', route: 'dashboard', reason: 'priority' } })
  if (!next.landingResolved || next.route !== 'dashboard') failures.push('landing must expose the selected route for title focus')
  if (typeof document !== 'undefined' && !document.getElementById('main-content')) failures.push('shell must provide #main-content')
  return failures
}
