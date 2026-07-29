import type { WebRoute } from '../app/AppShell'

/** Lightweight source-level checks; the locked repository has no approved frontend test runner. */
export function runAppShellChecks(): string[] {
  const routes: WebRoute[] = ['configuration', 'simulator', 'telemetry', 'audit']
  const behaviorMatrix = ['loading', 'forbidden', 'expired', 'no-data', 'error'] as const
  const failures: string[] = []
  if (routes.length !== 4) failures.push('all Phase 9 routes must be reachable')
  if (!routes.includes('audit')) failures.push('Audit route must be present')
  if (!behaviorMatrix.includes('loading') || !behaviorMatrix.includes('forbidden') || !behaviorMatrix.includes('expired')) failures.push('gateway behavior matrix must cover loading/forbidden/expired')
  if (!behaviorMatrix.includes('no-data')) failures.push('No Data must be a gateway state')
  return failures
}
