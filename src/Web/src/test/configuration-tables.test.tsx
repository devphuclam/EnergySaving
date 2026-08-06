import { DataTable } from '../components/data/DataTable'
import { FilterBar } from '../components/data/FilterBar'
import { Pagination } from '../components/data/Pagination'
import { configurationSortKeys, effectiveConfigurationSort, safeConfigurationDate, sortManagementItems } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_TABLE_EXPECTED_FAILURES = 0
export function configurationTableContractFailures(): string[] {
  const failures: string[] = []
  if (typeof DataTable !== 'function' || typeof FilterBar !== 'function' || typeof Pagination !== 'function') failures.push('configuration tables must use shared data primitives')
  const rows = sortManagementItems([{ id: 'b', name: 'B' }, { id: 'a', name: 'A' }], 'name', 'ascending')
  if (rows[0]?.name !== 'A') failures.push('current-page sort must be deterministic')

  const resources = ['sites', 'areas', 'assets', 'points', 'data-sources', 'source-point-mappings', 'simulator-configurations']
  for (const resource of resources) {
    if (configurationSortKeys(resource).length === 0) failures.push(`every resource must declare explicit current-page sort keys (${resource})`)
    const sorted = effectiveConfigurationSort(resource, { key: 'status', direction: 'descending' })
    if (!configurationSortKeys(resource).includes(sorted.key)) failures.push(`effective sort must stay inside the declared current-page keys (${resource})`)
  }
  if (effectiveConfigurationSort('points', { key: 'name', direction: 'ascending' }).key !== 'code') failures.push('a key absent from the current-page columns must fall back to the explicit default, not a first-column guess')

  if (safeConfigurationDate('') !== '—') failures.push('an absent effective date must render an em dash in date columns')
  if (safeConfigurationDate('garbage') === 'Invalid Date') failures.push('a malformed date must never render Invalid Date')

  return failures
}
