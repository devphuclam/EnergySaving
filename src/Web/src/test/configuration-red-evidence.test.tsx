import {
  configurationContractChecks,
  configurationEntityKeys,
  configurationFormDirty,
  configurationLifecyclePresentation,
  configurationValidationErrors,
  detailFieldsFor,
  duplicateIdentityFromResult,
  effectiveConfigurationSort,
  isRetryableManagementMutationResult,
  managementMutationFingerprint,
  normalizeConfigurationForm,
  safeConfigurationDate,
  sameManagementMutationIntent,
} from '../features/configuration/ConfigurationManagementComponents'

/**
 * Phase 3 red evidence is source-visible and type-checked by the Web build. There is no
 * approved frontend runtime runner in this repository, so this file deliberately exports
 * deterministic checks rather than pretending to execute a browser test.
 */
export const CONFIGURATION_RED_EVIDENCE_EXPECTED_FAILURES = 0

export function configurationRedEvidenceFailures(): string[] {
  const failures: string[] = []
  const expectedEntities = ['sites', 'areas', 'assets', 'points', 'data-sources', 'source-point-mappings', 'simulator-configurations']
  if (JSON.stringify(configurationEntityKeys) !== JSON.stringify(expectedEntities)) {
    failures.push('all seven configuration entities must be present in the canonical order')
  }
  failures.push(...configurationContractChecks())
  if (configurationValidationErrors('sites', 'create', { name: '' })[0]?.key !== 'name') {
    failures.push('site validation must identify the first invalid field')
  }
  if (configurationLifecyclePresentation('Active').tone !== 'success') {
    failures.push('lifecycle presentation must preserve a non-color status cue')
  }

  const createIntent = { resource: 'sites', kind: 'create' as const, identity: '', payload: JSON.stringify({ name: 'A' }) }
  const createIntentCopy = { resource: 'sites', kind: 'create' as const, identity: '', payload: JSON.stringify({ name: 'A' }) }
  const differentPayload = { resource: 'sites', kind: 'create' as const, identity: '', payload: JSON.stringify({ name: 'B' }) }
  const differentIdentity = { resource: 'sites', kind: 'update' as const, identity: 'site-1', payload: JSON.stringify({ name: 'A' }) }
  if (managementMutationFingerprint(createIntent.resource, createIntent.kind, createIntent.identity, createIntent.payload) !==
      managementMutationFingerprint(createIntentCopy.resource, createIntentCopy.kind, createIntentCopy.identity, createIntentCopy.payload)) {
    failures.push('mutation fingerprint must be stable for the same intent')
  }
  if (!sameManagementMutationIntent(createIntent, createIntentCopy)) failures.push('identical intents must compare equal')
  if (sameManagementMutationIntent(createIntent, differentPayload)) failures.push('a changed payload must be a different mutation intent')
  if (sameManagementMutationIntent(createIntent, differentIdentity)) failures.push('a changed identity must be a different mutation intent')

  if (!isRetryableManagementMutationResult({ ok: false, status: 503 })) failures.push('503 must be a retryable mutation result')
  if (!isRetryableManagementMutationResult({ ok: false, status: 503, errorCode: 'RUNTIME_FAILURE' })) failures.push('RUNTIME_FAILURE must be retryable')
  if (!isRetryableManagementMutationResult({ ok: false, status: 500, errorCode: 'DEPENDENCY_UNAVAILABLE' })) failures.push('DEPENDENCY_UNAVAILABLE must be retryable')
  if (isRetryableManagementMutationResult({ ok: false, status: 409 })) failures.push('a version conflict must never be retried with the same key')
  if (isRetryableManagementMutationResult({ ok: false, status: 422 })) failures.push('a definitive validation rejection must not be retried with the same key')
  if (isRetryableManagementMutationResult({ ok: true, status: 200 })) failures.push('a success must not be retried')

  const emptyNumeric = normalizeConfigurationForm('points', 'create', { name: 'P', expectedIntervalSeconds: '', noDataAfterSeconds: '' })
  if ('expectedIntervalSeconds' in emptyNumeric.body || 'noDataAfterSeconds' in emptyNumeric.body) failures.push('an empty optional numeric field must be absent from the request body')
  if (emptyNumeric.errors.length) failures.push('an empty optional numeric field must not be a validation error')
  const zeroNumeric = normalizeConfigurationForm('simulator-configurations', 'edit', { minimumValue: '0', maximumValue: '0' })
  if (zeroNumeric.body.minimumValue !== 0 || zeroNumeric.body.maximumValue !== 0) failures.push('a zero numeric value must stay zero and never be dropped')
  if (normalizeConfigurationForm('points', 'edit', { expectedIntervalSeconds: 'not-a-number' }).errors[0]?.key !== 'expectedIntervalSeconds') failures.push('non-numeric text must be rejected')
  if (normalizeConfigurationForm('points', 'edit', { expectedIntervalSeconds: '1.5' }).errors[0]?.key !== 'expectedIntervalSeconds') failures.push('a fractional interval must be rejected as non-integer')
  if (normalizeConfigurationForm('points', 'edit', { expectedIntervalSeconds: '-5' }).errors[0]?.key !== 'expectedIntervalSeconds') failures.push('a non-positive interval must be rejected')
  if (normalizeConfigurationForm('simulator-configurations', 'edit', { minimumValue: '10', maximumValue: '5' }).errors.some(error => error.key === 'minimumValue')) failures.push('minimum greater than maximum must be rejected')
  if (configurationFormDirty({ name: 'Kho A' }, { name: 'Kho A' })) failures.push('restoring the original values must not count as dirty')
  if (!configurationFormDirty({ name: 'Kho B' }, { name: 'Kho A' })) failures.push('a changed value must count as dirty')

  if (safeConfigurationDate('') !== '—') failures.push('an absent date must render as an em dash')
  if (safeConfigurationDate('not-a-date') !== 'Không hợp lệ') failures.push('a malformed date must never render Invalid Date')
  if (!safeConfigurationDate('2026-08-06T00:00:00Z').includes('2026')) failures.push('a valid date must render a real date')

  for (const entity of configurationEntityKeys) {
    const fields = detailFieldsFor(entity)
    if (fields.length === 0) failures.push(`detail allowlist must exist for ${entity}`)
    if (fields.some(field => !field.label)) failures.push(`every detail field must have a Vietnamese label for ${entity}`)
    if (fields.some(field => /secret|token|password|connection/i.test(field.key))) failures.push(`detail allowlist must never expose secrets for ${entity}`)
  }
  if (detailFieldsFor('unknown-resource').length !== 0) failures.push('unknown resources must expose no detail fields')

  if (effectiveConfigurationSort('points', { key: 'not-a-column', direction: 'ascending' }).key !== 'code') failures.push('an invalid sort key must fall back to the explicit default, never the first safe column implicitly')
  if (effectiveConfigurationSort('sites', { key: 'name', direction: 'descending' }).key !== 'name') failures.push('a valid sort key must be preserved')
  if (effectiveConfigurationSort('sites', { key: 'name', direction: 'bogus' as never }).direction !== 'ascending') failures.push('an unknown direction must normalize to ascending')
  if (effectiveConfigurationSort('sites', { key: 'name', direction: 'ascending' }).direction !== 'ascending') failures.push('ascending must remain ascending')

  if (duplicateIdentityFromResult({ ok: true, body: { id: 'draft-9' } }) !== 'draft-9') failures.push('the server-returned identity must be used for the duplicate')
  if (duplicateIdentityFromResult({ ok: true, body: {} }) !== '') failures.push('an identity must never be invented when the server returns none')
  if (duplicateIdentityFromResult({ ok: false }) !== '') failures.push('a failed duplicate must never expose a fabricated identity')

  return failures
}
