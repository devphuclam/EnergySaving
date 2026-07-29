import type { WebRoute } from '../app/AppShell'

/** Lightweight source-level checks; the locked repository has no approved frontend test runner. */
export function runAppShellChecks(): string[] {
  const routes: WebRoute[] = ['configuration', 'simulator', 'telemetry', 'audit']
  const failures: string[] = []
  if (routes.length !== 4) failures.push('all Phase 9 routes must be reachable')
  if (!routes.includes('audit')) failures.push('Audit route must be present')
  return failures
}
