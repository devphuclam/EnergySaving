import { configurationValidationErrors } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_SOURCE_MAPPING_EXPECTED_FAILURES = 0
export function configurationSourceMappingFailures(): string[] {
  const failures: string[] = []
  const errors = configurationValidationErrors('source-point-mappings', 'create', {})
  if (errors[0]?.key !== 'sourceId' || errors[1]?.key !== 'pointId') failures.push('mapping create must require explicit Source and Point selections')
  return failures
}

