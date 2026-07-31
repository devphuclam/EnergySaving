import { useEffect, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { ManagementFilter, ManagementItem } from './ConfigurationManagementComponents'
import {
  ActivateVersionButton,
  DuplicateButton,
  FeedbackBanner,
  ManagementFilterBar,
  ManagementTable,
  PaginationControls,
  resourceLabel,
  textValue,
  useDebouncedSearch,
  type ManagementColumn,
  type ManagementFeedback,
  type ManagementState,
} from './ConfigurationManagementComponents'

const emptyFilter: ManagementFilter = { page: 1, pageSize: 20 }

function statusOf(error: unknown): ManagementState {
  if (error instanceof Error && error.message === 'forbidden') return 'forbidden'
  if (error instanceof Error && error.message === 'expired') return 'expired'
  return 'error'
}

export function ConfigurationManagementRoutes() {
  const gateways = useWebGateways()
  const [resource, setResource] = useState('sites')
  const [filter, setFilter] = useState(emptyFilter)
  const [state, setState] = useState<ManagementState>('loading')
  const [items, setItems] = useState<ManagementItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [sites, setSites] = useState<Array<{ id: string; label: string }>>([])
  const [busyItem, setBusyItem] = useState<string | null>(null)
  const [feedback, setFeedback] = useState<ManagementFeedback>(null)
  const search = useDebouncedSearch(filter.search ?? '')

  useEffect(() => {
    let cancelled = false
    setState('loading')
    void gateways.management.list(resource, { ...filter, search: search || undefined, page: 1 })
      .then(page => {
        if (cancelled) return
        setItems(page.items)
        setTotalCount(page.totalCount)
        setState(page.items.length === 0 ? 'no-data' : 'ready')
      })
      .catch(error => {
        if (cancelled) return
        setState(statusOf(error))
      })
    return () => { cancelled = true }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [resource, search, filter.status, filter.siteId, filter.areaId, filter.page, gateways.management])

  useEffect(() => {
    let cancelled = false
    void gateways.management.list('sites', { page: 1, pageSize: 200 })
      .then(page => {
        if (cancelled) return
        setSites(page.items.map(value => ({ id: textValue(value.id), label: `${textValue(value.code)} – ${textValue(value.name)}` })).filter(value => value.id))
      })
      .catch(() => { if (!cancelled) setSites([]) })
    return () => { cancelled = true }
  }, [gateways.management])

  async function duplicate(item: ManagementItem) {
    const id = textValue(item.id ?? item.configurationId)
    setBusyItem(id)
    const result = await gateways.management.duplicate(resource, id)
    setBusyItem(null)
    if (result.ok) {
      setFeedback({ tone: 'success', message: `Đã tạo bản nháp ${resourceLabel(resource)} mới (${textValue(result.body?.id)}) để xem xét.` })
      reload()
    } else {
      setFeedback({ tone: 'error', message: `Không thể nhân bản: ${result.errorCode ?? `HTTP ${result.status}`}.` })
    }
  }

  async function activate(item: ManagementItem) {
    const id = textValue(item.configurationId)
    const headVersion = Number(item.version ?? 0)
    const draftVersion = Number(item.draftConfigurationVersion ?? 0)
    setBusyItem(id)
    const result = await gateways.management.activateSimulatorConfigurationVersion(id, headVersion, draftVersion)
    setBusyItem(null)
    if (result.ok) {
      setFeedback({ tone: 'success', message: `Đã kích hoạt bản ${draftVersion} của cấu hình mô phỏng.` })
      reload()
    } else {
      setFeedback({ tone: 'error', message: `Không thể kích hoạt: ${result.errorCode ?? `HTTP ${result.status}`}.` })
    }
  }

  function reload() {
    setFilter(current => ({ ...current, page: 1 }))
    setFeedback(null)
  }

  const columns = columnsFor(resource)
  return (
    <section className="page" aria-labelledby="configuration-management-title">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Quản lý cấu hình</p>
          <h1 id="configuration-management-title">Cấu hình vận hành</h1>
          <p className="lede">Tìm kiếm, lọc và nhân bản cấu hình theo phạm vi được ủy quyền. Mọi thay đổi đều tạo bản nháp cần kích hoạt.</p>
        </div>
        <span className="badge badge-neutral">{resourceLabel(resource)}</span>
      </div>
      <nav className="tabs" aria-label="Loại cấu hình">
        {(['sites', 'areas', 'assets', 'points', 'data-sources', 'source-point-mappings', 'simulator-configurations'] as const).map(value => (
          <button key={value} type="button"
            className={`tab ${resource === value ? 'tab-active' : ''}`}
            onClick={() => { setResource(value); setFilter(emptyFilter); setFeedback(null) }}>
            {resourceLabel(value)}
          </button>
        ))}
      </nav>
      <ManagementFilterBar
        search={filter.search}
        onSearchChange={value => setFilter(current => ({ ...current, search: value, page: 1 }))}
        statuses={statusesFor(resource)}
        status={filter.status}
        onStatusChange={value => setFilter(current => ({ ...current, status: value || undefined, page: 1 }))}
        siteOptions={sites}
        siteId={filter.siteId}
        onSiteChange={value => setFilter(current => ({ ...current, siteId: value || undefined, page: 1 }))}
        busy={state === 'loading'}
      />
      <FeedbackBanner feedback={feedback} />
      <div className="card form-card">
        <ManagementTable
          resource={resource}
          state={state}
          columns={columns}
          items={items}
          emptyMessage={`Chưa có ${resourceLabel(resource).toLocaleLowerCase('vi')} nào trong phạm vi hiện tại.`}
          renderActions={item => resource === 'simulator-configurations'
            ? (<span className="actions-stack">
                <DuplicateButton item={item} busyItem={busyItem} onDuplicate={duplicate} />
                <ActivateVersionButton item={item} busyItem={busyItem} onActivate={activate} />
              </span>)
            : <DuplicateButton item={item} busyItem={busyItem} onDuplicate={duplicate} />}
        />
      </div>
      <PaginationControls
        page={filter.page}
        pageSize={filter.pageSize}
        totalCount={totalCount}
        onPageChange={page => setFilter(current => ({ ...current, page }))}
        busy={state === 'loading'}
      />
    </section>
  )
}

function statusesFor(resource: string): string[] {
  switch (resource) {
    case 'sites': return ['Draft', 'Active', 'Decommissioned', 'Superseded']
    case 'areas': return ['Draft', 'Active', 'Decommissioned', 'Superseded']
    case 'assets': return ['Draft', 'Active', 'Decommissioned', 'Superseded']
    case 'points': return ['Draft', 'Active', 'Inactive', 'Decommissioned']
    case 'data-sources': return ['Draft', 'Active', 'Suspended', 'Decommissioned']
    case 'source-point-mappings': return ['Draft', 'Active', 'Inactive', 'Superseded']
    case 'simulator-configurations': return []
    default: return []
  }
}

function columnsFor(resource: string): ManagementColumn[] {
  switch (resource) {
    case 'sites':
      return [
        { key: 'code', label: 'Mã' },
        { key: 'name', label: 'Tên' },
        { key: 'timezone', label: 'Múi giờ' },
        { key: 'status', label: 'Trạng thái' },
        { key: 'version', label: 'Phiên bản' },
      ]
    case 'areas':
      return [
        { key: 'code', label: 'Mã' },
        { key: 'name', label: 'Tên' },
        { key: 'status', label: 'Trạng thái' },
        { key: 'version', label: 'Phiên bản' },
      ]
    case 'assets':
      return [
        { key: 'code', label: 'Mã' },
        { key: 'name', label: 'Tên' },
        { key: 'status', label: 'Trạng thái' },
        { key: 'version', label: 'Phiên bản' },
      ]
    case 'points':
      return [
        { key: 'code', label: 'Mã' },
        { key: 'metricId', label: 'Chỉ số' },
        { key: 'unitId', label: 'Đơn vị' },
        { key: 'dataOwnerUserId', label: 'Chủ dữ liệu' },
        { key: 'status', label: 'Trạng thái' },
        { key: 'version', label: 'Phiên bản' },
      ]
    case 'data-sources':
      return [
        { key: 'code', label: 'Mã' },
        { key: 'name', label: 'Tên' },
        { key: 'sourceType', label: 'Loại nguồn' },
        { key: 'status', label: 'Trạng thái' },
        { key: 'version', label: 'Phiên bản' },
      ]
    case 'source-point-mappings':
      return [
        { key: 'pointId', label: 'Điểm đo' },
        { key: 'status', label: 'Trạng thái' },
        { key: 'effectiveFrom', label: 'Hiệu lực từ', render: item => new Date(textValue(item.effectiveFrom)).toLocaleString('vi-VN') },
        { key: 'effectiveTo', label: 'Đến', render: item => item.effectiveTo ? new Date(textValue(item.effectiveTo)).toLocaleString('vi-VN') : '—' },
        { key: 'version', label: 'Phiên bản' },
      ]
    case 'simulator-configurations':
      return [
        { key: 'configurationId', label: 'Mã cấu hình' },
        { key: 'sourceId', label: 'Nguồn dữ liệu' },
        { key: 'currentConfigurationVersion', label: 'Bản hiện hành' },
        { key: 'version', label: 'Phiên bản tổng hợp' },
      ]
    default:
      return []
  }
}
