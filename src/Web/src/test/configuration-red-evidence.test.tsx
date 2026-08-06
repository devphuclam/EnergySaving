import {
  configurationContractChecks,
  configurationEntityKeys,
  configurationFormDirty,
  configurationLifecyclePresentation,
  configurationValidationErrors,
  detailFieldsFor,
  detailRequestOwner,
  detailResponseApplies,
  duplicateIdentityFromResult,
  effectiveConfigurationSort,
  isRetryableManagementMutationResult,
  managementMutationDisposition,
  normalizeConfigurationForm,
  pendingManagementMutationFingerprint,
  safeConfigurationDate,
  samePendingManagementMutation,
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

  const baseCreate: Parameters<typeof samePendingManagementMutation>[0] = { resource: 'sites', kind: 'create', entityId: '', payload: { name: 'A' }, retryKey: 'K-1' }
  const sameCreate = { ...baseCreate }
  const differentPayload = { ...baseCreate, payload: { name: 'B' } }
  const differentEntity = { ...baseCreate, kind: 'update' as const, entityId: 'site-1' }
  const versionedUpdate = { ...baseCreate, kind: 'update' as const, entityId: 'site-1', expectedVersion: 3 }
  const versionBump = { ...versionedUpdate, expectedVersion: 4 }
  const duplicateA = { ...baseCreate, kind: 'duplicate' as const, entityId: 'cfg-1', targetSourceId: 'src-1' }
  const duplicateB = { ...duplicateA, targetSourceId: 'src-2' }
  const reviewA = { ...baseCreate, kind: 'review' as const, entityId: 'cfg-1', draftVersion: 2 }
  const reviewB = { ...reviewA, draftVersion: 3 }
  if (pendingManagementMutationFingerprint(baseCreate) !== pendingManagementMutationFingerprint(sameCreate)) failures.push('mutation fingerprint must be stable for the same descriptor')
  if (pendingManagementMutationFingerprint(baseCreate) === pendingManagementMutationFingerprint({ ...sameCreate, retryKey: 'K-2' })) failures.push('the fingerprint must ignore the retry key')
  if (samePendingManagementMutation(baseCreate, differentPayload)) failures.push('a changed payload must be a different mutation intent')
  if (samePendingManagementMutation(baseCreate, differentEntity)) failures.push('a changed entity must be a different mutation intent')
  if (samePendingManagementMutation(versionedUpdate, versionBump)) failures.push('an expected version change must create a different intent')
  if (samePendingManagementMutation(duplicateA, duplicateB)) failures.push('a target Source change must invalidate a pending duplicate')
  if (samePendingManagementMutation(reviewA, reviewB)) failures.push('a draft version change must invalidate a pending review')
  if (!baseCreate.retryKey || baseCreate.retryKey !== 'K-1') failures.push('the first submission must carry one generated retry key')
  if (baseCreate.retryKey !== sameCreate.retryKey) failures.push('an exact retry must reuse the stored retry key, never generate a new one')
  if (!samePendingManagementMutation(baseCreate, sameCreate)) failures.push('an exact retry must be the same mutation intent')

  if (managementMutationDisposition({ ok: true, status: 200 }) !== 'success') failures.push('a success must be classified as success')
  if (managementMutationDisposition({ ok: false, status: 503 }) !== 'retryable') failures.push('503 must be a retryable mutation result')
  if (managementMutationDisposition({ ok: false, status: 500, errorCode: 'RUNTIME_FAILURE' }) !== 'retryable') failures.push('RUNTIME_FAILURE must be retryable')
  if (managementMutationDisposition({ ok: false, status: 500, errorCode: 'DEPENDENCY_UNAVAILABLE' }) !== 'retryable') failures.push('DEPENDENCY_UNAVAILABLE must be retryable')
  if (managementMutationDisposition({ ok: false, status: 409 }) !== 'definitive') failures.push('a version conflict must never be retried with the same key')
  if (managementMutationDisposition({ ok: false, status: 422 }) !== 'definitive') failures.push('a definitive validation rejection must not be retried with the same key')
  if (managementMutationDisposition({ ok: false, status: 401, errorCode: 'expired' }) !== 'expired') failures.push('an expired mutation must be classified as expired, never retried')
  if (isRetryableManagementMutationResult({ ok: false, status: 503 })) failures.push('retryable classification must agree with the disposition')
  if (!isRetryableManagementMutationResult({ ok: false, status: 500, errorCode: 'DEPENDENCY_UNAVAILABLE' })) failures.push('DEPENDENCY_UNAVAILABLE must be retryable')
  if (isRetryableManagementMutationResult({ ok: true, status: 200 })) failures.push('a success must not be retried')

  const emptyNumeric = normalizeConfigurationForm('points', 'create', { name: 'P', expectedIntervalSeconds: '', noDataAfterSeconds: '' })
  if ('expectedIntervalSeconds' in emptyNumeric.body || 'noDataAfterSeconds' in emptyNumeric.body) failures.push('an empty optional numeric field must be absent from the request body')
  if (emptyNumeric.errors.length) failures.push('an empty optional numeric field must not be a validation error')
  const zeroInterval = normalizeConfigurationForm('points', 'edit', { expectedIntervalSeconds: '0' })
  if (!zeroInterval.errors.some(error => error.key === 'expectedIntervalSeconds')) failures.push('a zero interval must be rejected for a positive interval field per the server domain rule')
  if ('expectedIntervalSeconds' in zeroInterval.body) failures.push('a rejected interval must never be transmitted')
  if (normalizeConfigurationForm('points', 'edit', { expectedIntervalSeconds: '-5' }).errors.some(error => error.key === 'expectedIntervalSeconds')) failures.push('a negative interval must be rejected')
  if (normalizeConfigurationForm('points', 'edit', { expectedIntervalSeconds: '1.5' }).errors.some(error => error.key === 'expectedIntervalSeconds')) failures.push('a fractional interval must be rejected as non-integer')
  if (!normalizeConfigurationForm('points', 'edit', { expectedIntervalSeconds: 'not-a-number' }).errors.some(error => error.key === 'expectedIntervalSeconds')) failures.push('non-numeric text must be rejected')
  const crossField = normalizeConfigurationForm('points', 'edit', { expectedIntervalSeconds: '60', noDataAfterSeconds: '60' })
  if (!crossField.errors.some(error => error.key === 'noDataAfterSeconds')) failures.push('noDataAfterSeconds must exceed expectedIntervalSeconds per the server domain rule')
  const decimals = normalizeConfigurationForm('simulator-configurations', 'edit', { minimumValue: '1.5', maximumValue: '2.75' })
  if (decimals.body.minimumValue !== 1.5 || decimals.body.maximumValue !== 2.75) failures.push('finite decimals must be preserved for double contract fields')
  if (decimals.errors.length) failures.push('valid finite decimals must not be rejected')
  const zeroDecimals = normalizeConfigurationForm('simulator-configurations', 'edit', { minimumValue: '0', maximumValue: '0' })
  if (zeroDecimals.body.minimumValue !== 0 || zeroDecimals.body.maximumValue !== 0) failures.push('a zero numeric value must stay zero and never be dropped')
  const negativeSeed = normalizeConfigurationForm('simulator-configurations', 'edit', { deterministicSeed: '-1' })
  if (!negativeSeed.errors.some(error => error.key === 'deterministicSeed')) failures.push('a negative seed must be rejected for an unsigned contract field')
  const fractionalSeed = normalizeConfigurationForm('simulator-configurations', 'edit', { deterministicSeed: '1.5' })
  if (!fractionalSeed.errors.some(error => error.key === 'deterministicSeed')) failures.push('a fractional seed must be rejected')
  const unsafeSeed = normalizeConfigurationForm('simulator-configurations', 'edit', { deterministicSeed: '9007199254740992' })
  if (!unsafeSeed.errors.some(error => error.key === 'deterministicSeed')) failures.push('a seed beyond the safe integer range must be rejected')
  const zeroSeed = normalizeConfigurationForm('simulator-configurations', 'edit', { deterministicSeed: '0' })
  if (zeroSeed.errors.length || zeroSeed.body.deterministicSeed !== 0) failures.push('zero must be a valid unsigned seed')
  const minMax = normalizeConfigurationForm('simulator-configurations', 'edit', { minimumValue: '10', maximumValue: '5' })
  if (!minMax.errors.some(error => error.key === 'minimumValue')) failures.push('minimum greater than maximum must be rejected')
  if (minMax.errors.some(error => error.key === 'minimumValue' && !/Giá trị nhỏ nhất/.test(error.message))) failures.push('numeric errors must use Vietnamese field labels, never property keys')
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

  if (duplicateIdentityFromResult({ ok: true, body: { id: 'draft-9' } }) !== 'draft-9') failures.push('the server-returned id must be used for the duplicate')
  if (duplicateIdentityFromResult({ ok: true, body: { configurationId: 'cfg-1' } }) !== 'cfg-1') failures.push('the server-returned configurationId must be used for a Simulator duplicate')
  if (duplicateIdentityFromResult({ ok: true, body: { code: 'DS-01' } }) !== '') failures.push('a code alias must never be used as the duplicate identity')
  if (duplicateIdentityFromResult({ ok: true, body: {} }) !== '') failures.push('an identity must never be invented when the server returns none')
  if (duplicateIdentityFromResult({ ok: false }) !== '') failures.push('a failed duplicate must never expose a fabricated identity')

  const currentOwner = detailRequestOwner(1, 'sites', 'site-1')
  if (!detailResponseApplies(currentOwner, detailRequestOwner(1, 'sites', 'site-1'))) failures.push('the current detail owner must accept its own response')
  if (detailResponseApplies(null, detailRequestOwner(1, 'sites', 'site-1'))) failures.push('a closed or invalidated detail must reject every response')
  if (detailResponseApplies(currentOwner, detailRequestOwner(2, 'sites', 'site-1'))) failures.push('a newer detail request must invalidate the older response')
  if (detailResponseApplies(detailRequestOwner(1, 'areas', 'area-1'), detailRequestOwner(1, 'sites', 'site-1'))) failures.push('a resource switch must reject the previous resource response')
  if (detailResponseApplies(currentOwner, detailRequestOwner(1, 'sites', 'site-2'))) failures.push('a different entity must reject the previous entity response')
  if (detailResponseApplies(detailRequestOwner(1, 'simulator-configurations', 'cfg-2'), detailRequestOwner(1, 'sites', 'site-1'))) failures.push('a post-duplicate detail on another resource must never apply to the old page')

  return failures
}
