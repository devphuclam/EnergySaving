import { configurationEntityKeys, duplicateIdentityFromResult, resourceLabel, simulatorActivationReadiness } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_ENTITY_FLOW_EXPECTED_FAILURES = 0
export function configurationEntityFlowFailures(): string[] {
  const failures: string[] = []
  if (configurationEntityKeys.length !== 7) failures.push('Sites, Areas, Assets, Points, Sources, Mappings and Simulator Configurations must be present')
  if (!resourceLabel('sites') || !resourceLabel('areas') || !resourceLabel('assets')) failures.push('hierarchy labels must be available')

  if (duplicateIdentityFromResult({ ok: true, body: { configurationId: 'sim-cfg-3' } }) !== 'sim-cfg-3') failures.push('a duplicate must adopt the exact identity returned by the server')
  if (duplicateIdentityFromResult({ ok: true, body: { id: 'draft-9' } }) !== 'draft-9') failures.push('a duplicate must adopt the server-returned id')
  if (duplicateIdentityFromResult({ ok: true, body: { code: 'DS-01' } }) !== '') failures.push('a code alias must never be treated as the duplicate identity; only the server-returned id/configurationId counts')

  const draftReady = { configurationId: 'sim-cfg-1', draftConfigurationVersion: 2, currentConfigurationVersion: 1, relationshipReviewed: true, validationRecorded: true }
  const staleReview = { ...draftReady, relationshipReceiptStale: true }
  const staleValidation = { ...draftReady, validationReceiptStale: true }
  const unreviewed = { ...draftReady, relationshipReviewed: false }
  const invalidated = { ...draftReady, validationRecorded: false }
  const noDraft = { ...draftReady, draftConfigurationVersion: 1 }
  const missingDraft = { ...draftReady, draftConfigurationVersion: null }
  const noIdentity = { ...draftReady, configurationId: '' }
  if (!simulatorActivationReadiness(draftReady).ready) failures.push('a contract-realistic draft with confirmed review and validation must be ready to activate')
  if (simulatorActivationReadiness(staleReview).ready) failures.push('a stale review receipt must never allow activation')
  if (simulatorActivationReadiness(staleValidation).ready) failures.push('a stale validation receipt must never allow activation')
  if (simulatorActivationReadiness(unreviewed).ready) failures.push('an unreviewed draft must never allow activation')
  if (simulatorActivationReadiness(invalidated).ready) failures.push('an unvalidated draft must never allow activation')
  if (simulatorActivationReadiness(noDraft).ready) failures.push('no draft version must never allow activation')
  if (simulatorActivationReadiness(missingDraft).ready) failures.push('an absent draft version must never allow activation')
  if (simulatorActivationReadiness(noIdentity).ready) failures.push('a missing configuration identity must never allow activation')
  if (Object.prototype.hasOwnProperty.call(draftReady, 'status')) failures.push('simulator readiness fixtures must not fabricate a status field that the management contract does not carry')

  return failures
}
