import { ConfirmDialog } from '../components/dialogs/ConfirmDialog'
import { FeedbackBanner } from '../components/feedback/FeedbackBanner'
import { ConflictState } from '../components/feedback/ConflictState'
import { BlockedState } from '../components/feedback/BlockedState'
import { ErrorState } from '../components/feedback/ErrorState'
import { OperationalStatusBadge } from '../components/status/OperationalStatusBadge'
import { actionLabelFor, canDeleteResource, configurationLifecyclePresentation, lifecycleActionsFor, managementStateMessage, simulatorActivationReadiness } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_LIFECYCLE_EXPECTED_FAILURES = 0
export function configurationLifecycleStateFailures(): string[] {
  const failures: string[] = []
  if (typeof ConfirmDialog !== 'function' || typeof FeedbackBanner !== 'function' || typeof ConflictState !== 'function' || typeof BlockedState !== 'function' || typeof OperationalStatusBadge !== 'function' || typeof ErrorState !== 'function') failures.push('lifecycle feedback must use shared states and dialogs')
  if (configurationLifecyclePresentation('Draft').cue === '') failures.push('lifecycle status must include a non-color cue')

  if (!lifecycleActionsFor('data-sources', 'Draft').includes('decommission')) failures.push('a Draft Data Source must offer safe lifecycle actions including decommission')
  if (!lifecycleActionsFor('source-point-mappings', 'Active').includes('supersede')) failures.push('an Active Source Mapping must offer supersede')
  if (lifecycleActionsFor('sites', 'Suspended').length !== 0) failures.push('no lifecycle action may be offered for unsupported statuses')
  if (!actionLabelFor('decommission') || !actionLabelFor('supersede') || !actionLabelFor('inactivate')) failures.push('every lifecycle action must have a Vietnamese label')
  if (!canDeleteResource('data-sources', 'Draft') || !canDeleteResource('source-point-mappings', 'Draft')) failures.push('only safe Draft resources may be deleted')
  if (canDeleteResource('sites', 'Draft') || canDeleteResource('data-sources', 'Active')) failures.push('delete must be refused outside the safe Draft set')

  const draftReady = { configurationId: 'sim-cfg-1', status: 'Draft', draftConfigurationVersion: 2, currentConfigurationVersion: 1, relationshipReviewed: true, relationshipReceiptStale: false, validationRecorded: true, validationReceiptStale: false }
  if (!simulatorActivationReadiness(draftReady).ready) failures.push('confirmed review and validation receipts must allow activation')
  if (simulatorActivationReadiness({ ...draftReady, relationshipReceiptStale: true }).ready) failures.push('a stale review receipt must block activation')
  if (simulatorActivationReadiness({ ...draftReady, validationReceiptStale: true }).ready) failures.push('a stale validation receipt must block activation')

  if (managementStateMessage('expired', 'sites', 'x')?.title !== 'Phiên đã hết hạn') failures.push('an expired session must present a distinct recovery state')
  if (managementStateMessage('ready', 'sites', 'x') !== null) failures.push('a ready list must not render a state message')
  if (managementStateMessage('no-data', 'sites', 'x')?.tone !== 'empty') failures.push('no data must render the empty state')
  if (managementStateMessage('dependency', 'sites', 'x')?.tone !== 'blocked') failures.push('a dependency failure must render the blocked state')

  return failures
}
