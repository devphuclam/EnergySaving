import { ConfirmDialog } from '../components/dialogs/ConfirmDialog'
import { FeedbackBanner } from '../components/feedback/FeedbackBanner'
import { ConflictState } from '../components/feedback/ConflictState'
import { BlockedState } from '../components/feedback/BlockedState'
import { OperationalStatusBadge } from '../components/status/OperationalStatusBadge'
import { configurationLifecyclePresentation } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_LIFECYCLE_EXPECTED_FAILURES = 0
export function configurationLifecycleStateFailures(): string[] {
  const failures: string[] = []
  if (typeof ConfirmDialog !== 'function' || typeof FeedbackBanner !== 'function' || typeof ConflictState !== 'function' || typeof BlockedState !== 'function' || typeof OperationalStatusBadge !== 'function') failures.push('lifecycle feedback must use shared states and dialogs')
  if (configurationLifecyclePresentation('Draft').cue === '') failures.push('lifecycle status must include a non-color cue')
  return failures
}

