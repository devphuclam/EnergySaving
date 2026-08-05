import { BlockedState } from '../components/feedback/BlockedState'
import { ConflictState } from '../components/feedback/ConflictState'
import { EmptyState } from '../components/feedback/EmptyState'
import { ErrorState } from '../components/feedback/ErrorState'
import { ForbiddenState } from '../components/feedback/ForbiddenState'
import { LoadingState } from '../components/feedback/LoadingState'
import { RetryState } from '../components/feedback/RetryState'

export function runStatePresentationChecks(): string[] {
  const states = [BlockedState, ConflictState, EmptyState, ErrorState, ForbiddenState, LoadingState, RetryState]
  return states.every(state => typeof state === 'function') ? [] : ['all non-happy-path state components must be importable']
}
