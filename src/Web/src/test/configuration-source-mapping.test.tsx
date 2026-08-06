import { configurationValidationErrors, detailFieldsFor, normalizeConfigurationForm, safeConfigurationDate } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_SOURCE_MAPPING_EXPECTED_FAILURES = 0
export function configurationSourceMappingFailures(): string[] {
  const failures: string[] = []
  const errors = configurationValidationErrors('source-point-mappings', 'create', {})
  if (errors[0]?.key !== 'sourceId' || errors[1]?.key !== 'pointId') failures.push('mapping create must require explicit Source and Point selections')

  const mapping = normalizeConfigurationForm('source-point-mappings', 'create', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-06T00:00', effectiveToUtc: '' })
  if (mapping.body.sourceId !== 'src-1' || mapping.body.pointId !== 'pt-2') failures.push('mapping body must carry the selected Source and Point')
  if (mapping.body.effectiveFrom !== '2026-08-06T00:00') failures.push('the approved contract representation must be preserved for the effective-from value')
  if ('effectiveTo' in mapping.body) failures.push('an optional blank effective end must be omitted, never sent as an empty string')
  if ('effectiveFromUtc' in mapping.body || 'effectiveToUtc' in mapping.body) failures.push('internal form keys must never leak into the request body')

  const badFrom = normalizeConfigurationForm('source-point-mappings', 'create', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: 'not-a-date' })
  if (!badFrom.errors.some(error => error.key === 'effectiveFromUtc')) failures.push('a malformed effective-from must fail closed')
  const badTo = normalizeConfigurationForm('source-point-mappings', 'create', { sourceId: 'src-1', pointId: 'pt-2', effectiveToUtc: 'garbage' })
  if (!badTo.errors.some(error => error.key === 'effectiveToUtc')) failures.push('a malformed effective-to must fail closed')
  const reversed = normalizeConfigurationForm('source-point-mappings', 'create', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-07T00:00', effectiveToUtc: '2026-08-06T00:00' })
  if (!reversed.errors.some(error => error.key === 'effectiveToUtc')) failures.push('an effective end before its start must be rejected')

  const fields = detailFieldsFor('source-point-mappings')
  for (const key of ['dataSourceId', 'pointId', 'effectiveFrom', 'effectiveTo']) {
    if (!fields.some(field => field.key === key)) failures.push(`mapping detail allowlist must include the actual server field ${key}`)
  }
  if (fields.some(field => field.key === 'sourceId')) failures.push('the detail allowlist must use the actual server field dataSourceId, never the nonexistent sourceId alias')

  if (safeConfigurationDate('') !== '—') failures.push('an absent effective-to must render an em dash')
  if (!safeConfigurationDate('2026-08-06T00:00:00Z').includes('2026')) failures.push('a valid effective-from must render a real date')

  return failures
}
