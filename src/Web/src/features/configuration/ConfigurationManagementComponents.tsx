import { useEffect, useState, type ReactNode } from 'react'
import type { ManagementFilter } from '../../gateways/webGateways'

export type ManagementItem = Record<string, unknown> & { id?: string }
export type { ManagementFilter }
export type ManagementState = 'loading' | 'ready' | 'forbidden' | 'expired' | 'no-data' | 'validation' | 'conflict' | 'not-found' | 'dependency' | 'runtime' | 'error'
export type ManagementFeedback = { tone: 'success' | 'warning' | 'error' | 'info'; message: string } | null
export type ManagementColumn = { key: string; label: string; render?: (item: ManagementItem) => ReactNode }
export type SortDirection = 'ascending' | 'descending'

export const configurationEntityKeys = ['sites', 'areas', 'assets', 'points', 'data-sources', 'source-point-mappings', 'simulator-configurations'] as const
export const MANAGEMENT_RESOURCES = [
  { key: 'sites', label: 'Địa điểm' },
  { key: 'areas', label: 'Khu vực' },
  { key: 'assets', label: 'Tài sản' },
  { key: 'points', label: 'Điểm đo' },
  { key: 'data-sources', label: 'Nguồn dữ liệu' },
  { key: 'source-point-mappings', label: 'Ánh xạ nguồn' },
  { key: 'simulator-configurations', label: 'Cấu hình mô phỏng' },
] as const

export function configurationContractChecks(): string[] {
  const failures: string[] = []
  if (configurationEntityKeys.length !== 7) failures.push('configuration hub must expose exactly seven entities')
  if (!configurationEntityKeys.includes('simulator-configurations')) failures.push('Simulator Configurations must remain a management entity')
  return failures
}

export function resourceLabel(resource: string): string {
  return MANAGEMENT_RESOURCES.find(value => value.key === resource)?.label ?? resource
}

export function textValue(value: unknown): string {
  if (value === null || value === undefined) return ''
  if (Array.isArray(value)) return value.map(item => textValue(item)).filter(Boolean).join(', ')
  if (typeof value === 'object') {
    const raw = (value as Record<string, unknown>).value
    return raw === null || raw === undefined ? '' : String(raw)
  }
  return String(value)
}

export function configurationValidationErrors(resource: string, mode: 'create' | 'edit', body: Record<string, unknown>): Array<{ key: string; message: string }> {
  const errors: Array<{ key: string; message: string }> = []
  const required = (key: string, message: string) => { if (!String(body[key] ?? '').trim()) errors.push({ key, message }) }
  if (['sites', 'areas', 'assets', 'points', 'data-sources'].includes(resource)) required('name', 'Tên là bắt buộc.')
  if (mode === 'create' && (resource === 'areas' || resource === 'data-sources')) required('siteId', 'Vui lòng chọn Địa điểm cha.')
  if (mode === 'create' && resource === 'assets') required('areaId', 'Vui lòng chọn Khu vực cha.')
  if (mode === 'create' && resource === 'points') required('assetId', 'Vui lòng chọn Tài sản cha.')
  if (mode === 'create' && resource === 'source-point-mappings') { required('sourceId', 'Vui lòng chọn Nguồn dữ liệu.'); required('pointId', 'Vui lòng chọn Điểm đo.') }
  if (mode === 'create' && resource === 'simulator-configurations') required('sourceId', 'Vui lòng chọn Nguồn dữ liệu.')
  return errors
}

export function sortManagementItems(items: readonly ManagementItem[], key: string, direction: SortDirection): ManagementItem[] {
  const sign = direction === 'ascending' ? 1 : -1
  return [...items].sort((left, right) => {
    const a = textValue(left[key]); const b = textValue(right[key])
    const numberA = Number(a); const numberB = Number(b)
    const comparison = a !== '' && b !== '' && Number.isFinite(numberA) && Number.isFinite(numberB)
      ? numberA - numberB : a.localeCompare(b, 'vi', { numeric: true, sensitivity: 'base' })
    return comparison * sign
  })
}

export function configurationLifecyclePresentation(status: string): { label: string; cue: string; tone: 'success' | 'warning' | 'neutral' | 'danger' } {
  if (status === 'Active') return { label: 'Đang hoạt động', cue: '●', tone: 'success' }
  if (status === 'Draft') return { label: 'Bản nháp', cue: '◌', tone: 'warning' }
  if (status === 'Suspended' || status === 'Inactive') return { label: status === 'Suspended' ? 'Tạm dừng' : 'Không hoạt động', cue: '!', tone: 'warning' }
  if (status === 'Decommissioned' || status === 'Superseded') return { label: status === 'Decommissioned' ? 'Ngừng sử dụng' : 'Đã thay thế', cue: '×', tone: 'neutral' }
  return { label: status || 'Chưa xác định', cue: '?', tone: 'neutral' }
}

export function managementStateMessage(state: ManagementState, resource: string, emptyMessage: string): { title: string; message: string; tone: 'loading' | 'empty' | 'forbidden' | 'error' | 'conflict' | 'blocked' | 'info' } | null {
  if (state === 'ready') return null
  if (state === 'loading') return { title: 'Đang tải', message: `Đang tải ${resourceLabel(resource)}…`, tone: 'loading' }
  if (state === 'forbidden') return { title: 'Không được phép', message: `Bạn không có quyền xem ${resourceLabel(resource)} trong phạm vi này.`, tone: 'forbidden' }
  if (state === 'expired') return { title: 'Phiên đã hết hạn', message: 'Vui lòng đăng nhập lại để tiếp tục.', tone: 'error' }
  if (state === 'conflict') return { title: 'Có xung đột', message: 'Dữ liệu đã thay đổi bởi người khác. Hãy tải lại trước khi lưu.', tone: 'conflict' }
  if (state === 'dependency' || state === 'runtime') return { title: 'Dịch vụ chưa sẵn sàng', message: 'Không hiển thị dữ liệu dự phòng. Hãy thử lại sau.', tone: 'blocked' }
  if (state === 'validation') return { title: 'Cần kiểm tra', message: 'Dữ liệu chưa hợp lệ; hãy sửa trường được đánh dấu.', tone: 'error' }
  if (state === 'not-found') return { title: 'Không tìm thấy', message: 'Thực thể không còn trong phạm vi được cấp quyền.', tone: 'info' }
  return { title: 'Chưa có dữ liệu', message: emptyMessage, tone: 'empty' }
}

export function DuplicateButton({ item, busyItem, onDuplicate }: { item: ManagementItem; busyItem?: string | null; onDuplicate: (item: ManagementItem) => void }) {
  const id = textValue(item.id ?? item.configurationId)
  return <button className="button button-secondary" type="button" disabled={!id || busyItem === id} onClick={() => onDuplicate(item)}>{busyItem === id ? 'Đang nhân bản…' : 'Nhân bản'}</button>
}

export function ActivateVersionButton({ item, busyItem, onActivate, readyForActivation = true }: { item: ManagementItem; busyItem?: string | null; onActivate: (item: ManagementItem) => void; readyForActivation?: boolean }) {
  const id = textValue(item.configurationId)
  const current = Number(item.currentConfigurationVersion ?? 0)
  const draft = Number(item.draftConfigurationVersion ?? 0)
  const hasDraft = draft > current
  return <button className="button button-primary" type="button" disabled={!id || !hasDraft || busyItem === id || !readyForActivation} title={!hasDraft ? 'Không có bản nháp để kích hoạt' : readyForActivation ? `Kích hoạt bản ${draft}` : 'Cần xem xét quan hệ và kiểm tra trước khi kích hoạt'} onClick={() => onActivate(item)}>{busyItem === id ? 'Đang kích hoạt…' : 'Kích hoạt'}</button>
}

export function ManagementActionButton({ label, onClick, disabled, tone = 'secondary', title }: { label: string; onClick: () => void; disabled?: boolean; tone?: 'primary' | 'secondary' | 'quiet' | 'danger'; title?: string }) {
  return <button className={`button button-${tone}`} type="button" disabled={disabled} title={title} onClick={onClick}>{label}</button>
}

export function useDebouncedSearch(value: string, delay = 350): string {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => { const handle = window.setTimeout(() => setDebounced(value), delay); return () => window.clearTimeout(handle) }, [value, delay])
  return debounced
}
