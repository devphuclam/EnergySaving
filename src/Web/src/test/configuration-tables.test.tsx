import { DataTable } from '../components/data/DataTable'
import { FilterBar } from '../components/data/FilterBar'
import { Pagination } from '../components/data/Pagination'
import { sortManagementItems } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_TABLE_EXPECTED_FAILURES = 0
export function configurationTableContractFailures(): string[] {
  const failures: string[] = []
  if (typeof DataTable !== 'function' || typeof FilterBar !== 'function' || typeof Pagination !== 'function') failures.push('configuration tables must use shared data primitives')
  const rows = sortManagementItems([{ id: 'b', name: 'B' }, { id: 'a', name: 'A' }], 'name', 'ascending')
  if (rows[0]?.name !== 'A') failures.push('current-page sort must be deterministic')
  return failures
}

