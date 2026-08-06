import { configurationEntityKeys, resourceLabel } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_ENTITY_FLOW_EXPECTED_FAILURES = 0
export function configurationEntityFlowFailures(): string[] {
  const failures: string[] = []
  if (configurationEntityKeys.length !== 7) failures.push('Sites, Areas, Assets, Points, Sources, Mappings and Simulator Configurations must be present')
  if (!resourceLabel('sites') || !resourceLabel('areas') || !resourceLabel('assets')) failures.push('hierarchy labels must be available')
  return failures
}

