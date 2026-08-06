import { configurationEntityKeys, duplicateIdentityFromResult, resourceLabel, simulatorActivationReadiness } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_ENTITY_FLOW_EXPECTED_FAILURES = 0
export function configurationEntityFlowFailures(): string[] {
  const failures: string[] = []
  if (configurationEntityKeys.length !== 7) failures.push('Sites, Areas, Assets, Points, Sources, Mappings and Simulator Configurations must be present')
  if (!resourceLabel('sites') || !resourceLabel('areas') || !resourceLabel('assets')) failures.push('hierarchy labels must be available')

  if (duplicateIdentityFromResult({ ok: true, body: { configurationId: 'sim-cfg-3' } }) !== 'sim-cfg-3') failures.push('a duplicate must adopt the exact identity returned by the server')
  if (duplicateIdentityFromResult({ ok: true, body: { code: 'DS-01' } }) !== 'DS-01') failures.push('a code fallback identity is acceptable only when the server returns one')

  const draftReady = { configurationId: 'sim-cfg-1', status: 'Draft', draftConfigurationVersion: 2, currentConfigurationVersion: 1, relationshipReviewed: true, validationRecorded: true }
  const staleReview = { ...draftReady, relationshipReceiptStale: true }
  const staleValidation = { ...draftReady, validationReceiptStale: true }
  const unreviewed = { ...draftReady, relationshipReviewed: false }
  const invalidated = { ...draftReady, validationRecorded: false }
  const noDraft = { ...draftReady, draftConfigurationVersion: 1 }
  const notDraft = { ...draftReady, status: 'Active' }
  if (!simulatorActivationReadiness(draftReady).ready) failures.push('a Draft with confirmed review and validation must be ready to activate')
  if (simulatorActivationReadiness(staleReview).ready) failures.push('a stale review receipt must never allow activation')
  if (simulatorActivationReadiness(staleValidation).ready) failures.push('a stale validation receipt must never allow activation')
  if (simulatorActivationReadiness(unreviewed).ready) failures.push('an unreviewed draft must never allow activation')
  if (simulatorActivationReadiness(invalidated).ready) failures.push('an unvalidated draft must never allow activation')
  if (simulatorActivationReadiness(noDraft).ready) failures.push('no draft version must never allow activation')
  if (simulatorActivationReadiness(notDraft).ready) failures.push('a non-Draft record must never allow activation')

  return failures
}
