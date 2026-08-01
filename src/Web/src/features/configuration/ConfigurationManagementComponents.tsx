import { useEffect, useState } from 'react'
import type { ManagementFilter, ManagementPage } from '../../gateways/webGateways'

export type ManagementItem = Record<string, unknown> & { id?: string }

export type { ManagementFilter, ManagementPage }

export type ManagementState = 'loading' | 'ready' | 'forbidden' | 'expired' | 'no-data' | 'validation' | 'conflict' | 'not-found' | 'dependency' | 'runtime' | 'error'

export type ManagementFeedback = {
  tone: 'success' | 'warning' | 'error' | 'info'
  message: string
} | null

export type ManagementColumn = {
  key: string
  label: string
  render?: (item: ManagementItem) => React.ReactNode
}

export const MANAGEMENT_RESOURCES = [
  { key: 'sites', label: 'Địa điểm' },
  { key: 'areas', label: 'Khu vực' },
  { key: 'assets', label: 'Tài sản' },
  { key: 'points', label: 'Điểm đo' },
  { key: 'data-sources', label: 'Nguồn dữ liệu' },
  { key: 'source-point-mappings', label: 'Ánh xạ nguồn' },
  { key: 'simulator-configurations', label: 'Cấu hình mô phỏng' },
] as const

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

export function ManagementFilterBar(props: {
  search?: string
  onSearchChange: (value: string) => void
  statuses: string[]
  status?: string
  onStatusChange: (value: string) => void
  siteOptions: Array<{ id: string; label: string }>
  siteId?: string
  onSiteChange: (value: string) => void
  busy?: boolean
}) {
  const { search, onSearchChange, statuses, status, onStatusChange, siteOptions, siteId, onSiteChange, busy } = props
  return (
    <div className="filter-bar" role="search" aria-label="Bộ lọc cấu hình">
      <label className="field">
        <span className="field-label">Tìm kiếm</span>
        <input className="input" type="search" value={search ?? ''} disabled={busy}
          placeholder="Mã, tên hoặc định danh…" onChange={event => onSearchChange(event.target.value)} />
      </label>
      <label className="field">
        <span className="field-label">Trạng thái</span>
        <select className="input" value={status ?? ''} disabled={busy}
          onChange={event => onStatusChange(event.target.value)}>
          <option value="">Tất cả</option>
          {statuses.map(value => <option key={value} value={value}>{value}</option>)}
        </select>
      </label>
      <label className="field">
        <span className="field-label">Địa điểm</span>
        <select className="input" value={siteId ?? ''} disabled={busy}
          onChange={event => onSiteChange(event.target.value)}>
          <option value="">Tất cả</option>
          {siteOptions.map(value => <option key={value.id} value={value.id}>{value.label}</option>)}
        </select>
      </label>
    </div>
  )
}

export function ManagementTable(props: {
  resource: string
  state: ManagementState
  columns: ManagementColumn[]
  items: ManagementItem[]
  emptyMessage: string
  renderActions?: (item: ManagementItem) => React.ReactNode
}) {
  const { resource, state, columns, items, emptyMessage, renderActions } = props
  if (state === 'loading') {
    return <p className="notice notice-info" role="status">Đang tải {resourceLabel(resource)}…</p>
  }
  if (state === 'forbidden') {
    return <p className="notice notice-warning" role="alert">Bạn không có quyền xem {resourceLabel(resource)} trong phạm vi này.</p>
  }
  if (state === 'expired') {
    return <p className="notice notice-warning" role="alert">Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.</p>
  }
  if (state === 'error') {
    return <p className="notice notice-warning" role="alert">Không thể tải {resourceLabel(resource)}. Hãy thử lại sau.</p>
  }
  if (state === 'dependency' || state === 'runtime') {
    return <p className="notice notice-warning" role="alert">Dịch vụ dữ liệu hiện không sẵn sàng. Không hiển thị dữ liệu dự phòng.</p>
  }
  if (state === 'conflict') {
    return <p className="notice notice-warning" role="alert">Dữ liệu đã thay đổi bởi người khác. Hãy tải lại trước khi lưu.</p>
  }
  if (state === 'validation') {
    return <p className="notice notice-warning" role="alert">Dữ liệu chưa hợp lệ; hãy sửa các trường được đánh dấu.</p>
  }
  if (state === 'not-found') {
    return <p className="notice notice-info" role="status">Không tìm thấy thực thể trong phạm vi được cấp quyền.</p>
  }
  if (state === 'no-data' || items.length === 0) {
    return <p className="notice notice-info" role="status">{emptyMessage}</p>
  }
  return (
    <div className="table-scroll" role="region" aria-label={`Danh sách ${resourceLabel(resource)}`}>
      <table className="data-table">
        <thead>
          <tr>
            {columns.map(column => <th key={column.key} scope="col">{column.label}</th>)}
            {renderActions ? <th scope="col"><span className="sr-only">Thao tác</span></th> : null}
          </tr>
        </thead>
        <tbody>
          {items.map((item, index) => (
            <tr key={textValue(item.id ?? item.configurationId) || index}>
              {columns.map(column => (
                <td key={column.key}>{column.render ? column.render(item) : textValue(item[column.key])}</td>
              ))}
              {renderActions ? <td className="actions-cell">{renderActions(item)}</td> : null}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function PaginationControls(props: {
  page: number
  pageSize: number
  totalCount: number
  onPageChange: (page: number) => void
  busy?: boolean
}) {
  const { page, pageSize, totalCount, onPageChange, busy } = props
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)
  return (
    <div className="pagination" role="navigation" aria-label="Phân trang">
      <span className="muted">{from}–{to} / {totalCount}</span>
      <button className="button button-quiet" type="button" disabled={busy || page <= 1}
        onClick={() => onPageChange(page - 1)}>Trước</button>
      <button className="button button-quiet" type="button" disabled={busy || page >= totalPages}
        onClick={() => onPageChange(page + 1)}>Sau</button>
    </div>
  )
}

export function FeedbackBanner(props: { feedback: ManagementFeedback }) {
  const { feedback } = props
  if (!feedback) return null
  return <p className={`notice notice-${feedback.tone}`} role={feedback.tone === 'error' || feedback.tone === 'warning' ? 'alert' : 'status'}>{feedback.message}</p>
}

export function DuplicateButton(props: {
  item: ManagementItem
  busyItem?: string | null
  onDuplicate: (item: ManagementItem) => void
}) {
  const { item, busyItem, onDuplicate } = props
  const id = textValue(item.id ?? item.configurationId)
  const busy = busyItem === id
  return (
    <button className="button button-secondary" type="button" disabled={!id || busy}
      onClick={() => onDuplicate(item)}>
      {busy ? 'Đang nhân bản…' : 'Nhân bản'}
    </button>
  )
}

export function ActivateVersionButton(props: {
  item: ManagementItem
  busyItem?: string | null
  onActivate: (item: ManagementItem) => void
  readyForActivation?: boolean
}) {
  const { item, busyItem, onActivate, readyForActivation = true } = props
  const id = textValue(item.configurationId)
  const current = Number(item.currentConfigurationVersion ?? 0)
  const draft = Number(item.draftConfigurationVersion ?? 0)
  const hasDraft = draft > current
  const busy = busyItem === id
  return (
    <button className="button button-primary" type="button"
      disabled={!id || !hasDraft || busy || !readyForActivation}
      title={!hasDraft ? 'Không có bản nháp để kích hoạt' : readyForActivation ? `Kích hoạt bản ${draft}` : 'Cần xem xét quan hệ và kiểm tra trước khi kích hoạt'}
      onClick={() => onActivate(item)}>
      {busy ? 'Đang kích hoạt…' : 'Kích hoạt'}
    </button>
  )
}

export function ManagementActionButton(props: {
  label: string
  onClick: () => void
  disabled?: boolean
  tone?: 'primary' | 'secondary' | 'quiet' | 'danger'
  title?: string
}) {
  const { label, onClick, disabled, tone = 'secondary', title } = props
  return <button className={`button button-${tone}`} type="button" disabled={disabled} title={title} onClick={onClick}>{label}</button>
}

export function useDebouncedSearch(value: string, delay = 350): string {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const handle = window.setTimeout(() => setDebounced(value), delay)
    return () => window.clearTimeout(handle)
  }, [value, delay])
  return debounced
}
