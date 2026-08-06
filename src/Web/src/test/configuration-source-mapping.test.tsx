import { configurationValidationErrors, detailFieldsFor, normalizeConfigurationForm, safeConfigurationDate } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_SOURCE_MAPPING_EXPECTED_FAILURES = 0
export function configurationSourceMappingFailures(): string[] {
  const failures: string[] = []
  const errors = configurationValidationErrors('source-point-mappings', 'create', {})
  if (errors[0]?.key !== 'sourceId' || errors[1]?.key !== 'pointId') failures.push('mapping create must require explicit Source and Point selections')

  const mapping = normalizeConfigurationForm('source-point-mappings', 'create', { sourceId: 'src-1', pointId: 'pt-2', effectiveFromUtc: '2026-08-06T00:00', effectiveToUtc: '' })
  if (mapping.body.sourceId !== 'src-1' || mapping.body.pointId !== 'pt-2') failures.push('mapping body must carry the selected Source and Point')
  if (mapping.body.effectiveToUtc !== '') failures.push('an empty effective end must be preserved as absent, never converted to a fabricated date')

  const fields = detailFieldsFor('source-point-mappings')
  for (const key of ['pointId', 'effectiveFrom', 'effectiveTo']) {
    if (!fields.some(field => field.key === key)) failures.push(`mapping detail allowlist must include ${key}`)
  }
  if (fields.some(field => field.key === 'dataSourceId')) failures.push('the detail allowlist must use canonical keys, not an alias dump')

  if (safeConfigurationDate('') !== '—') failures.push('an absent effective-to must render an em dash')
  if (!safeConfigurationDate('2026-08-06T00:00:00Z').includes('2026')) failures.push('a valid effective-from must render a real date')

  return failures
}
