import { ContextBar } from '../components/context/ContextBar'
import { PageHeader } from '../components/context/PageHeader'

export function runContextHeaderChecks(): string[] {
  const failures: string[] = []
  if (typeof ContextBar !== 'function' || typeof PageHeader !== 'function') failures.push('context components must be importable')
  return failures
}
