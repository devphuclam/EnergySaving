import { DataQualityIndicator } from '../components/status/DataQualityIndicator'
import { FreshnessIndicator } from '../components/status/FreshnessIndicator'
import { OperationalStatusBadge } from '../components/status/OperationalStatusBadge'

export function runStatusIndicatorChecks(): string[] {
  const failures: string[] = []
  if (typeof OperationalStatusBadge !== 'function' || typeof DataQualityIndicator !== 'function' || typeof FreshnessIndicator !== 'function') failures.push('status components must be importable')
  return failures
}
