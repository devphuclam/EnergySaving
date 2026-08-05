import { DataTable } from '../components/data/DataTable'
import { FilterBar } from '../components/data/FilterBar'
import { Pagination } from '../components/data/Pagination'

export function runDataTableChecks(): string[] {
  return typeof DataTable === 'function' && typeof FilterBar === 'function' && typeof Pagination === 'function' ? [] : ['data primitives must be importable']
}
