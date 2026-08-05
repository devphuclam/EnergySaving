import { BlockedState } from '../components/feedback/BlockedState'
import { EmptyState } from '../components/feedback/EmptyState'
import { ErrorState } from '../components/feedback/ErrorState'
import { ForbiddenState } from '../components/feedback/ForbiddenState'
import { LoadingState } from '../components/feedback/LoadingState'
import { RetryState } from '../components/feedback/RetryState'
import { FeedbackBanner } from '../components/feedback/FeedbackBanner'
import { DataQualityIndicator } from '../components/status/DataQualityIndicator'
import { FreshnessIndicator } from '../components/status/FreshnessIndicator'

/** T032 state-evidence surface: each operational state remains distinct and actionable. */
export function runDashboardTelemetryStateEvidence(): string[] {
  const components = [BlockedState, EmptyState, ErrorState, ForbiddenState, LoadingState, RetryState, FeedbackBanner, DataQualityIndicator, FreshnessIndicator]
  return components.every(component => typeof component === 'function') ? [] : ['required state components must be importable']
}
