import {
  configurationContractChecks,
  configurationEntityKeys,
  configurationLifecyclePresentation,
  configurationValidationErrors,
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
  return failures
}

