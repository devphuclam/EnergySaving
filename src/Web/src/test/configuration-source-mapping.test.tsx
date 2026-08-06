import { configurationValidationErrors, detailFieldsFor, normalizeConfigurationForm, safeConfigurationDate } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_SOURCE_MAPPING_EXPECTED_FAILURES = 0
export function configurationSourceMappingFailures(): string[] {
  const failures: string[] = []
  const errors = configurationValidationErrors('source-point-mappings', 'create', {})
  if (errors[0]?.key !== 'sourceId' || errors[1]?.key !== 'pointId') failures.push('mapping create must require explicit Source and Point selections')

  const create = normalizeConfigurationForm('source-point-mappings', 'create', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-06T00:00', effectiveToUtc: '' })
  if (create.body.sourceId !== 'src-1' || create.body.pointId !== 'pt-2') failures.push('mapping body must carry the selected Source and Point')
  if (create.body.effectiveFromUtc !== '2026-08-06T00:00') failures.push('create must send the entered start under the exact request key effectiveFromUtc')
  if ('effectiveToUtc' in create.body) failures.push('an optional blank effective end must be omitted on create, never sent as an empty string')
  if ('effectiveFrom' in create.body || 'effectiveTo' in create.body) failures.push('the request body must never contain the unsupported effectiveFrom/effectiveTo aliases')
  if (create.errors.length) failures.push('a valid create mapping must not produce validation errors')

  const createBlankStart = normalizeConfigurationForm('source-point-mappings', 'create', { sourceId: 'src-1', pointId: 'pt-2' })
  if ('effectiveFromUtc' in createBlankStart.body) failures.push('a blank create start must be omitted so the server applies its own default')

  const initialEdit = { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-06T00:00', effectiveToUtc: '2026-08-20T00:00' }
  const updateStart = normalizeConfigurationForm('source-point-mappings', 'edit', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-07T00:00', effectiveToUtc: '2026-08-20T00:00' }, initialEdit)
  if (updateStart.body.effectiveFromUtc !== '2026-08-07T00:00') failures.push('update must send the edited start under the exact request key effectiveFromUtc')
  if (updateStart.body.effectiveToUtc !== '2026-08-20T00:00') failures.push('an unchanged effective end must be sent as the preserved value')
  const updateClearEnd = normalizeConfigurationForm('source-point-mappings', 'edit', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-06T00:00', effectiveToUtc: '' }, initialEdit)
  if (updateClearEnd.body.effectiveToUtc !== null) failures.push('clearing an existing effective end must send the exact server-supported explicit clear representation: explicit null')
  if ('effectiveTo' in updateClearEnd.body || 'effectiveFrom' in updateClearEnd.body) failures.push('update must never send the unsupported effectiveFrom/effectiveTo aliases')
  const updatePreserveEnd = normalizeConfigurationForm('source-point-mappings', 'edit', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-06T00:00', effectiveToUtc: '' }, { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-06T00:00', effectiveToUtc: '' })
  if ('effectiveToUtc' in updatePreserveEnd.body) failures.push('an effective end that was already open-ended must be omitted so the server preserves its current value')
  const updateImmutable = normalizeConfigurationForm('source-point-mappings', 'edit', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-06T00:00', effectiveToUtc: '' }, initialEdit)
  if (updateImmutable.body.sourceId !== 'src-1' || updateImmutable.body.pointId !== 'pt-2') failures.push('immutable Source and Point identities must be preserved, never fabricated or silently changed')
  const editBlankStart = normalizeConfigurationForm('source-point-mappings', 'edit', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '', effectiveToUtc: '' }, initialEdit)
  if (!editBlankStart.errors.some(error => error.key === 'effectiveFromUtc')) failures.push('clearing the effective start on edit must fail closed because a mapping always has a start')
  if ('effectiveFromUtc' in editBlankStart.body) failures.push('a rejected blank start must never be transmitted')

  const badFrom = normalizeConfigurationForm('source-point-mappings', 'create', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: 'not-a-date' })
  if (!badFrom.errors.some(error => error.key === 'effectiveFromUtc')) failures.push('a malformed effective-from must fail closed')
  const badTo = normalizeConfigurationForm('source-point-mappings', 'create', { sourceId: 'src-1', pointId: 'pt-2', effectiveToUtc: 'garbage' })
  if (!badTo.errors.some(error => error.key === 'effectiveToUtc')) failures.push('a malformed effective-to must fail closed')
  const reversed = normalizeConfigurationForm('source-point-mappings', 'create', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-07T00:00', effectiveToUtc: '2026-08-06T00:00' })
  if (!reversed.errors.some(error => error.key === 'effectiveToUtc')) failures.push('an effective end before its start must be rejected')
  if ('effectiveFromUtc' in reversed.body || 'effectiveToUtc' in reversed.body) failures.push('a reversed interval must never be transmitted')

  const fields = detailFieldsFor('source-point-mappings')
  for (const key of ['dataSourceId', 'pointId', 'effectiveFrom', 'effectiveTo']) {
    if (!fields.some(field => field.key === key)) failures.push(`mapping detail allowlist must include the actual server field ${key}`)
  }
  if (fields.some(field => field.key === 'sourceId')) failures.push('the detail allowlist must use the actual server field dataSourceId, never the nonexistent sourceId alias')

  if (safeConfigurationDate('') !== '—') failures.push('an absent effective-to must render an em dash')
  if (!safeConfigurationDate('2026-08-06T00:00:00Z').includes('2026')) failures.push('a valid effective-from must render a real date')

  return failures
}
