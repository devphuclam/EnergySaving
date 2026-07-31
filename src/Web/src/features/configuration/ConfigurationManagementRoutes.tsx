import { useEffect, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { ManagementFilter, ManagementItem } from './ConfigurationManagementComponents'
import {
  ActivateVersionButton,
  DuplicateButton,
  FeedbackBanner,
  ManagementActionButton,
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

const RESOURCE_KEYS = ['sites', 'areas', 'assets', 'points', 'data-sources', 'source-point-mappings', 'simulator-configurations'] as const
const emptyFilter: ManagementFilter = { page: 1, pageSize: 20 }

function statusOf(error: unknown): ManagementState {
  if (error instanceof Error && error.message === 'forbidden') return 'forbidden'
  if (error instanceof Error && error.message === 'expired') return 'expired'
  if (error instanceof Error && error.message.includes('request-503')) return 'dependency'
  return 'error'
}

function idOf(item: ManagementItem): string {
  return textValue(item.id ?? item.configurationId)
}

function messageFor(result: { status: number; errorCode?: string }, action: string): string {
  const code = result.errorCode
  if (code === 'VERSION_CONFLICT' || result.status === 409) return `${action} thất bại: dữ liệu đã thay đổi, hãy tải lại và thử lại.`
  if (code === 'FORBIDDEN' || result.status === 403) return `${action} thất bại: bạn không có quyền trong phạm vi này.`
  if (code === 'NOT_FOUND' || result.status === 404) return `${action} thất bại: không tìm thấy thực thể.`
  if (code === 'DEPENDENCY_UNAVAILABLE' || result.status === 503) return `${action} thất bại: dịch vụ dữ liệu chưa sẵn sàng.`
  if (code === 'UNSUPPORTED_ACTION') return `${action} chưa được hỗ trợ: thao tác này bị tắt theo quy tắc miền nghiệp vụ.`
  if (code === 'DEPENDENT_HISTORY' || code === 'INVALID_STATE' || code === 'INVALIDSTATE') return `${action} bị từ chối vì thực thể đang được tham chiếu hoặc có lịch sử.`
  return `${action} thất bại${code ? `: ${code}` : ` (HTTP ${result.status})`}.`
}

export function ConfigurationManagementRoutes() {
  const gateways = useWebGateways()
  const [resource, setResource] = useState<string>('sites')
  const [filter, setFilter] = useState<ManagementFilter>(emptyFilter)
  const [state, setState] = useState<ManagementState>('loading')
  const [items, setItems] = useState<ManagementItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [sites, setSites] = useState<Array<{ id: string; label: string }>>([])
  const [busyItem, setBusyItem] = useState<string | null>(null)
  const [feedback, setFeedback] = useState<ManagementFeedback>(null)
  const [refreshNonce, setRefreshNonce] = useState(0)
  const [selected, setSelected] = useState<ManagementItem | null>(null)
  const [detail, setDetail] = useState<ManagementItem | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [editor, setEditor] = useState<'create' | 'edit' | null>(null)
  const [form, setForm] = useState<Record<string, string>>({})
  const [review, setReview] = useState<{ id: string; relationships: string[]; excluded: string[]; reviewed: boolean } | null>(null)
  const [validated, setValidated] = useState<Set<string>>(new Set())
  const search = useDebouncedSearch(filter.search ?? '')

  useEffect(() => {
    let cancelled = false
    setState('loading')
    void gateways.management.list(resource, { ...filter, search: search || undefined })
      .then(page => {
        if (cancelled) return
        setItems(page.items)
        setTotalCount(page.totalCount)
        setState(page.items.length === 0 ? 'no-data' : 'ready')
      })
      .catch(error => { if (!cancelled) setState(statusOf(error)) })
    return () => { cancelled = true }
  }, [resource, search, filter, refreshNonce, gateways.management])

  useEffect(() => {
    let cancelled = false
    void gateways.management.list('sites', { page: 1, pageSize: 200 })
      .then(page => {
        if (cancelled) return
        setSites(page.items.map(value => ({ id: textValue(value.id), label: `${textValue(value.code)} – ${textValue(value.name)}` })).filter(value => value.id))
      })
      .catch(() => { if (!cancelled) setSites([]) })
    return () => { cancelled = true }
  }, [gateways.management, refreshNonce])

  async function openDetail(item: ManagementItem) {
    const id = idOf(item)
    if (!id) return
    setSelected(item)
    setDetail(null)
    setDetailLoading(true)
    try {
      const loaded = await gateways.management.detail(resource, id)
      if (!loaded) { setState('not-found'); return }
      setDetail(loaded as ManagementItem)
    } catch (error) { setState(statusOf(error)) }
    finally { setDetailLoading(false) }
  }

  function beginCreate() {
    setSelected(null)
    setDetail(null)
    setEditor('create')
    setForm(defaultForm(resource, filter.siteId))
    setFeedback(null)
  }

  function beginEdit(item: ManagementItem) {
    setSelected(item)
    setDetail(item)
    setEditor('edit')
    setForm(formFromItem(resource, item))
    setFeedback(null)
  }

  async function submitEditor() {
    const body = normalizedForm(resource, form)
    if (!body.name && resource !== 'source-point-mappings' && resource !== 'simulator-configurations') {
      setState('validation')
      setFeedback({ tone: 'warning', message: 'Tên là bắt buộc.' })
      return
    }
    setBusyItem(editor === 'edit' && selected ? idOf(selected) : 'create')
    let result
    try {
      result = editor === 'create'
        ? await gateways.management.create(resource, body)
        : selected ? await gateways.management.update(resource, idOf(selected), Number(selected.version ?? 0), body) : { ok: false, status: 400, errorCode: 'NOT_FOUND' }
    } catch {
      setState('runtime')
      setFeedback({ tone: 'error', message: 'Không thể kết nối dịch vụ khi lưu cấu hình.' })
      setBusyItem(null)
      return
    }
    setBusyItem(null)
    if (!result.ok) {
      setState(result.status === 409 ? 'conflict' : result.status === 422 ? 'validation' : result.status === 503 ? 'dependency' : 'error')
      setFeedback({ tone: 'error', message: messageFor(result, editor === 'create' ? 'Tạo mới' : 'Cập nhật') })
      return
    }
    setEditor(null)
    if (editor === 'edit' && selected) invalidateValidation(idOf(selected))
    setFeedback({ tone: 'success', message: `${editor === 'create' ? 'Đã tạo' : 'Đã cập nhật'} ${resourceLabel(resource)} thành công.` })
    reload()
  }

  async function duplicate(item: ManagementItem) {
    const id = idOf(item)
    if (!id) return
    setBusyItem(id)
    let result
    try {
      result = await gateways.management.duplicate(resource, id)
    } catch {
      setBusyItem(null)
      setState('runtime')
      setFeedback({ tone: 'error', message: 'Không thể kết nối dịch vụ khi nhân bản.' })
      return
    }
    setBusyItem(null)
    if (!result.ok) {
      setFeedback({ tone: 'error', message: messageFor(result, 'Nhân bản') })
      return
    }
    const newId = textValue(result.body?.id)
    const relationships = Array.isArray(result.body?.reviewRelationships) ? result.body?.reviewRelationships.map(String) : []
    const excluded = Array.isArray(result.body?.excludedFields) ? result.body?.excludedFields.map(String) : []
    setReview({ id: newId, relationships, excluded, reviewed: false })
    setFeedback({ tone: 'success', message: `Đã tạo bản nháp ${resourceLabel(resource)} mới (${newId}). Hãy xem xét quan hệ và kiểm tra trước khi kích hoạt.` })
    if (newId) {
      try {
        const loaded = await gateways.management.detail(resource, newId)
        if (loaded) { setSelected(loaded as ManagementItem); setDetail(loaded as ManagementItem) }
      } catch {
        setState('runtime')
        setFeedback({ tone: 'warning', message: 'Đã nhân bản nhưng chưa tải được chi tiết bản nháp mới.' })
      }
    }
    reload()
  }

  async function validate(item: ManagementItem) {
    const id = idOf(item)
    setBusyItem(id)
    let result
    try {
      result = await gateways.management.validate(resource, id)
    } catch {
      setBusyItem(null)
      setState('runtime')
      setFeedback({ tone: 'error', message: 'Không thể kết nối dịch vụ khi kiểm tra.' })
      return
    }
    setBusyItem(null)
    if (!result.ok) { setFeedback({ tone: 'error', message: messageFor(result, 'Kiểm tra') }); return }
    setValidated(current => new Set(current).add(id))
    setFeedback({ tone: 'success', message: `Kiểm tra ${resourceLabel(resource)} thành công. Có thể tiếp tục theo trạng thái hợp lệ.` })
  }

  async function lifecycle(item: ManagementItem, action: string) {
    const id = idOf(item)
    if (!id || !window.confirm(`Xác nhận ${action} ${resourceLabel(resource).toLocaleLowerCase('vi')} này?`)) return
    if (action === 'activate' && review?.id === id && (!review.reviewed || !validated.has(id))) {
      setFeedback({ tone: 'warning', message: 'Cần xem xét quan hệ và kiểm tra bản nháp trước khi kích hoạt.' })
      return
    }
    setBusyItem(id)
    let result
    try {
      result = await gateways.management.lifecycle(resource, id, action, Number(item.version ?? 0))
    } catch {
      setBusyItem(null)
      setState('runtime')
      setFeedback({ tone: 'error', message: 'Không thể kết nối dịch vụ khi chuyển trạng thái.' })
      return
    }
    setBusyItem(null)
    if (!result.ok) { setFeedback({ tone: 'error', message: messageFor(result, 'Chuyển trạng thái') }); return }
    if (action !== 'activate') invalidateValidation(id)
    setFeedback({ tone: 'success', message: `Đã ${action} ${resourceLabel(resource).toLocaleLowerCase('vi')}.` })
    reload()
  }

  async function remove(item: ManagementItem) {
    const id = idOf(item)
    if (!id || !window.confirm('Chỉ bản nháp an toàn mới được xóa. Bạn có chắc chắn?')) return
    setBusyItem(id)
    let result
    try {
      result = await gateways.management.remove(resource, id, Number(item.version ?? 0))
    } catch {
      setBusyItem(null)
      setState('runtime')
      setFeedback({ tone: 'error', message: 'Không thể kết nối dịch vụ khi xóa.' })
      return
    }
    setBusyItem(null)
    if (!result.ok) { setFeedback({ tone: 'error', message: messageFor(result, 'Xóa') }); return }
    setFeedback({ tone: 'success', message: `Đã xóa bản nháp ${resourceLabel(resource).toLocaleLowerCase('vi')}.` })
    reload()
  }

  async function activate(item: ManagementItem) {
    const id = textValue(item.configurationId)
    const headVersion = Number(item.version ?? 0)
    const draftVersion = Number(item.draftConfigurationVersion ?? 0)
    const relationshipReviewed = review?.id === id ? review.reviewed : true
    if (!relationshipReviewed || !validated.has(id)) {
      setFeedback({ tone: 'warning', message: 'Cần xem xét quan hệ và kiểm tra bản nháp trước khi kích hoạt.' })
      return
    }
    setBusyItem(id)
    let result
    try {
      result = await gateways.management.activateSimulatorConfigurationVersion(id, headVersion, draftVersion, true, true)
    } catch {
      setBusyItem(null)
      setState('runtime')
      setFeedback({ tone: 'error', message: 'Không thể kết nối dịch vụ khi kích hoạt.' })
      return
    }
    setBusyItem(null)
    if (!result.ok) { setFeedback({ tone: 'error', message: messageFor(result, 'Kích hoạt') }); return }
    setFeedback({ tone: 'success', message: `Đã kích hoạt bản ${draftVersion} của cấu hình mô phỏng.` })
    reload()
  }

  function reload() {
    setFilter(current => ({ ...current, page: 1 }))
    setRefreshNonce(value => value + 1)
  }

  function invalidateValidation(id: string) {
    setValidated(current => { const next = new Set(current); next.delete(id); return next })
    setReview(current => current?.id === id ? null : current)
  }

  const columns = columnsFor(resource)
  const hasDraftReady = (item: ManagementItem) => {
    const id = idOf(item)
    const relationshipReviewed = review?.id === id ? review.reviewed : true
    return relationshipReviewed && validated.has(id)
  }

  return (
    <section className="page" aria-labelledby="configuration-management-title">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Quản lý cấu hình</p>
          <h1 id="configuration-management-title">Cấu hình vận hành</h1>
          <p className="lede">Tìm kiếm, xem chi tiết, tạo, sửa, nhân bản, kiểm tra và chuyển trạng thái theo phạm vi được ủy quyền.</p>
        </div>
        <div className="actions-stack"><span className="badge badge-neutral">{resourceLabel(resource)}</span><ManagementActionButton label="Tạo mới" tone="primary" onClick={beginCreate} /></div>
      </div>
      <nav className="tabs" aria-label="Loại cấu hình">
        {RESOURCE_KEYS.map(value => <button key={value} type="button" className={`tab ${resource === value ? 'tab-active' : ''}`} onClick={() => { setResource(value); setFilter(emptyFilter); setSelected(null); setDetail(null); setEditor(null); setReview(null); setFeedback(null) }}>{resourceLabel(value)}</button>)}
      </nav>
      <ManagementFilterBar search={filter.search} onSearchChange={value => setFilter(current => ({ ...current, search: value, page: 1 }))} statuses={statusesFor(resource)} status={filter.status} onStatusChange={value => setFilter(current => ({ ...current, status: value || undefined, page: 1 }))} siteOptions={sites} siteId={filter.siteId} onSiteChange={value => setFilter(current => ({ ...current, siteId: value || undefined, page: 1 }))} busy={state === 'loading'} />
      <FeedbackBanner feedback={feedback} />
      {review ? <RelationshipReview review={review} onReviewed={() => setReview(current => current ? { ...current, reviewed: true } : current)} /> : null}
      {editor ? <EditorPanel resource={resource} mode={editor} form={form} setForm={setForm} busy={busyItem !== null} onSave={submitEditor} onCancel={() => setEditor(null)} /> : null}
      {detailLoading ? <p className="notice notice-info" role="status">Đang tải chi tiết…</p> : null}
      {detail ? <DetailPanel resource={resource} detail={detail} onClose={() => { setDetail(null); setSelected(null) }} supportedActions={supportedActions(resource, detail)} /> : null}
      <div className="card form-card">
        <ManagementTable resource={resource} state={state} columns={columns} items={items} emptyMessage={`Chưa có ${resourceLabel(resource).toLocaleLowerCase('vi')} nào trong phạm vi hiện tại.`} renderActions={item => {
          const id = idOf(item)
          const actions = lifecycleActions(resource, textValue(item.status))
          return <span className="actions-stack">
            <ManagementActionButton label="Chi tiết" onClick={() => void openDetail(item)} />
            <ManagementActionButton label="Sửa" onClick={() => beginEdit(item)} disabled={busyItem !== null || (resource === 'source-point-mappings' && textValue(item.status) === 'Active') || (resource === 'points' && textValue(item.status) === 'Active') || (resource === 'simulator-configurations' && Number(item.draftConfigurationVersion ?? 0) > Number(item.currentConfigurationVersion ?? 0))} title={resource === 'source-point-mappings' && textValue(item.status) === 'Active' ? 'Ánh xạ đang Active là bất biến; hãy tạo bản nháp thay thế.' : resource === 'points' && textValue(item.status) === 'Active' ? 'Điểm đo Active cần quy trình điều phối; chỉnh sửa hành vi đang bị tắt.' : resource === 'simulator-configurations' && Number(item.draftConfigurationVersion ?? 0) > Number(item.currentConfigurationVersion ?? 0) ? 'Cấu hình đã có bản nháp; hãy xem xét và kích hoạt bản nháp hiện tại trước khi sửa tiếp.' : undefined} />
            <ManagementActionButton label="Kiểm tra" onClick={() => void validate(item)} disabled={busyItem === id} />
            <DuplicateButton item={item} busyItem={busyItem} onDuplicate={duplicate} />
            {actions.map(action => <ManagementActionButton key={action} label={actionLabel(action)} onClick={() => void lifecycle(item, action)} disabled={busyItem === id} tone={action === 'decommission' ? 'danger' : 'secondary'} />)}
            {resource === 'simulator-configurations' ? <ActivateVersionButton item={item} busyItem={busyItem} readyForActivation={hasDraftReady(item)} onActivate={activate} /> : null}
            {canDelete(resource, textValue(item.status)) ? <ManagementActionButton label="Xóa nháp" tone="danger" onClick={() => void remove(item)} disabled={busyItem === id} /> : null}
          </span>
        }} />
      </div>
      <PaginationControls page={filter.page} pageSize={filter.pageSize} totalCount={totalCount} onPageChange={page => setFilter(current => ({ ...current, page }))} busy={state === 'loading'} />
    </section>
  )
}

function RelationshipReview(props: { review: { id: string; relationships: string[]; excluded: string[]; reviewed: boolean }; onReviewed: () => void }) {
  const { review, onReviewed } = props
  return <div className="card form-card" aria-labelledby="relationship-review-title">
    <h2 id="relationship-review-title">Xem xét quan hệ bản nháp</h2>
    <p className="muted">Bản nháp <code>{review.id}</code> chưa được kích hoạt cho đến khi quan hệ được xem xét và kiểm tra.</p>
    <p><strong>Quan hệ được sao chép:</strong> {review.relationships.length ? review.relationships.join(', ') : 'Không có quan hệ tự động.'}</p>
    <p><strong>Trường bị loại trừ:</strong> {review.excluded.length ? review.excluded.join(', ') : 'Không có dữ liệu lịch sử hoặc bí mật nào được sao chép.'}</p>
    <ManagementActionButton label={review.reviewed ? 'Đã xem xét' : 'Đánh dấu đã xem xét'} onClick={onReviewed} disabled={review.reviewed} tone="primary" />
  </div>
}

function DetailPanel(props: { resource: string; detail: ManagementItem; onClose: () => void; supportedActions: string[] }) {
  return <div className="card form-card" role="dialog" aria-labelledby="configuration-detail-title">
    <div className="page-heading"><h2 id="configuration-detail-title">Chi tiết {resourceLabel(props.resource)}</h2><ManagementActionButton label="Đóng" onClick={props.onClose} tone="quiet" /></div>
    <dl className="detail-grid">{Object.entries(props.detail).filter(([key]) => !key.toLowerCase().includes('secret') && !key.toLowerCase().includes('token')).map(([key, value]) => <div key={key}><dt>{fieldLabel(key)}</dt><dd>{textValue(value) || '—'}</dd></div>)}</dl>
    <p className="muted">Thao tác hợp lệ: {props.supportedActions.length ? props.supportedActions.join(', ') : 'Không có chuyển trạng thái trực tiếp.'}</p>
  </div>
}

function EditorPanel(props: { resource: string; mode: 'create' | 'edit'; form: Record<string, string>; setForm: (value: Record<string, string>) => void; busy: boolean; onSave: () => void; onCancel: () => void }) {
  const fields = editorFields(props.resource, props.mode)
  return <div className="card form-card" role="dialog" aria-labelledby="configuration-editor-title"><h2 id="configuration-editor-title">{props.mode === 'create' ? 'Tạo mới' : 'Chỉnh sửa'} {resourceLabel(props.resource)}</h2><div className="filter-bar">{fields.map(field => <label className="field" key={field.key}><span className="field-label">{field.label}</span><input className="input" type={field.type ?? 'text'} value={props.form[field.key] ?? ''} readOnly={field.readOnly} aria-readonly={field.readOnly || undefined} onChange={event => props.setForm({ ...props.form, [field.key]: event.target.value })} />{field.help ? <small className="muted">{field.help}</small> : null}</label>)}</div><div className="actions-stack"><ManagementActionButton label={props.busy ? 'Đang lưu…' : 'Lưu'} tone="primary" disabled={props.busy} onClick={props.onSave} /><ManagementActionButton label="Hủy" onClick={props.onCancel} /></div></div>
}

function editorFields(resource: string, mode: 'create' | 'edit'): Array<{ key: string; label: string; type?: string; readOnly?: boolean; help?: string }> {
  const common = [{ key: 'name', label: 'Tên' }]
  const immutable = (label: string) => ({ label, readOnly: true, help: 'Trường quan hệ do miền sở hữu quản lý; không thể đổi trên bản ghi này.' })
  switch (resource) {
    case 'areas': return mode === 'create' ? [...common, { key: 'siteId', label: 'Mã địa điểm cha' }] : common
    case 'assets': return mode === 'create' ? [...common, { key: 'areaId', label: 'Mã khu vực cha' }] : common
    case 'points': return mode === 'create' ? [...common, { key: 'description', label: 'Mô tả' }, { key: 'assetId', label: 'Mã tài sản cha' }, { key: 'metricId', label: 'Mã chỉ số' }, { key: 'unitId', label: 'Mã đơn vị' }, { key: 'dataOwnerUserId', label: 'Mã chủ dữ liệu' }, { key: 'expectedIntervalSeconds', label: 'Chu kỳ (giây)', type: 'number' }, { key: 'noDataAfterSeconds', label: 'No Data sau (giây)', type: 'number' }] : [...common, { key: 'description', label: 'Mô tả' }, { key: 'metricId', label: 'Mã chỉ số' }, { key: 'unitId', label: 'Mã đơn vị' }, { key: 'dataOwnerUserId', label: 'Mã chủ dữ liệu' }, { key: 'expectedIntervalSeconds', label: 'Chu kỳ (giây)', type: 'number' }, { key: 'noDataAfterSeconds', label: 'No Data sau (giây)', type: 'number' }]
    case 'data-sources': return mode === 'create' ? [...common, { key: 'siteId', label: 'Mã địa điểm' }] : common
    case 'source-point-mappings': return [{ key: 'sourceId', ...immutable('Mã nguồn dữ liệu') }, { key: 'pointId', ...immutable('Mã điểm đo') }, { key: 'effectiveFromUtc', label: 'Hiệu lực từ', type: 'datetime-local' }, { key: 'effectiveToUtc', label: 'Hiệu lực đến', type: 'datetime-local' }]
    case 'simulator-configurations': return [{ key: 'sourceId', ...immutable('Mã nguồn dữ liệu') }, { key: 'scenarioType', label: 'Kịch bản' }, { key: 'minimumValue', label: 'Giá trị nhỏ nhất', type: 'number' }, { key: 'maximumValue', label: 'Giá trị lớn nhất', type: 'number' }, { key: 'intervalSeconds', label: 'Chu kỳ (giây)', type: 'number' }, { key: 'deterministicSeed', label: 'Hạt giống xác định', type: 'number' }]
    default: return common
  }
}

function fieldLabel(key: string): string {
  return ({
    id: 'Định danh', code: 'Mã', name: 'Tên', description: 'Mô tả', status: 'Trạng thái', version: 'Phiên bản',
    siteId: 'Mã địa điểm', areaId: 'Mã khu vực', assetId: 'Mã tài sản', metricId: 'Mã chỉ số', unitId: 'Mã đơn vị',
    dataOwnerUserId: 'Mã chủ dữ liệu', dataSourceId: 'Mã nguồn dữ liệu', sourceId: 'Mã nguồn dữ liệu', pointId: 'Mã điểm đo',
    effectiveFrom: 'Hiệu lực từ', effectiveTo: 'Hiệu lực đến', currentConfigurationVersion: 'Bản hiện hành',
    draftConfigurationVersion: 'Bản nháp', scenarioType: 'Kịch bản', intervalSeconds: 'Chu kỳ (giây)',
    minimumValue: 'Giá trị nhỏ nhất', maximumValue: 'Giá trị lớn nhất', deterministicSeed: 'Hạt giống xác định',
  } as Record<string, string>)[key] ?? key
}

function defaultForm(resource: string, siteId?: string): Record<string, string> {
  const result: Record<string, string> = { name: '' }
  if (resource === 'areas' || resource === 'data-sources') result.siteId = siteId ?? ''
  if (resource === 'points') { result.expectedIntervalSeconds = '60'; result.noDataAfterSeconds = '180' }
  if (resource === 'simulator-configurations') { result.scenarioType = 'Constant'; result.minimumValue = '42'; result.maximumValue = '42'; result.intervalSeconds = '60'; result.deterministicSeed = '42' }
  return result
}

function formFromItem(resource: string, item: ManagementItem): Record<string, string> {
  const form = defaultForm(resource)
  for (const field of editorFields(resource, 'edit')) form[field.key] = textValue(item[field.key])
  if (resource === 'source-point-mappings') {
    form.sourceId = textValue(item.dataSourceId)
    form.pointId = textValue(item.pointId)
    form.effectiveFromUtc = textValue(item.effectiveFrom)
    form.effectiveToUtc = textValue(item.effectiveTo)
  }
  return form
}

function normalizedForm(_resource: string, form: Record<string, string>): Record<string, unknown> {
  const result: Record<string, unknown> = { ...form }
  for (const key of ['expectedIntervalSeconds', 'noDataAfterSeconds', 'intervalSeconds', 'deterministicSeed']) if (result[key]) result[key] = Number(result[key])
  for (const key of ['minimumValue', 'maximumValue']) if (result[key]) result[key] = Number(result[key])
  return result
}

function lifecycleActions(resource: string, status: string): string[] {
  if (status === 'Draft') return resource === 'data-sources' ? ['activate', 'decommission'] : resource === 'source-point-mappings' ? ['activate'] : resource === 'points' ? ['activate'] : ['activate']
  if (status === 'Active') return resource === 'data-sources' ? ['suspend', 'decommission'] : resource === 'source-point-mappings' ? ['inactivate', 'supersede'] : resource === 'points' ? ['deactivate'] : ['deactivate']
  if (status === 'Suspended') return resource === 'data-sources' ? ['activate', 'decommission'] : []
  if (status === 'Inactive') return resource === 'source-point-mappings' ? ['supersede'] : []
  if (resource === 'simulator-configurations') return []
  return []
}

function actionLabel(action: string): string {
  return ({ activate: 'Kích hoạt', deactivate: 'Tắt hoạt động', decommission: 'Ngừng sử dụng', suspend: 'Tạm dừng', inactivate: 'Đặt không hoạt động', supersede: 'Thay thế' } as Record<string, string>)[action] ?? action
}

function supportedActions(resource: string, detail: ManagementItem): string[] {
  return [...lifecycleActions(resource, textValue(detail.status)), ...(canDelete(resource, textValue(detail.status)) ? ['delete-draft'] : []), 'validate', 'duplicate']
}

function canDelete(resource: string, status: string): boolean {
  return (resource === 'data-sources' || resource === 'source-point-mappings') && status === 'Draft'
}

function columnsFor(resource: string): ManagementColumn[] {
  switch (resource) {
    case 'sites': return [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'timezone', label: 'Múi giờ' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'areas': return [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'assets': return [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'points': return [{ key: 'code', label: 'Mã' }, { key: 'metricId', label: 'Chỉ số' }, { key: 'unitId', label: 'Đơn vị' }, { key: 'dataOwnerUserId', label: 'Chủ dữ liệu' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'data-sources': return [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'sourceType', label: 'Loại nguồn' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'source-point-mappings': return [{ key: 'pointId', label: 'Điểm đo' }, { key: 'status', label: 'Trạng thái' }, { key: 'effectiveFrom', label: 'Hiệu lực từ', render: item => new Date(textValue(item.effectiveFrom)).toLocaleString('vi-VN') }, { key: 'effectiveTo', label: 'Đến', render: item => item.effectiveTo ? new Date(textValue(item.effectiveTo)).toLocaleString('vi-VN') : '—' }, { key: 'version', label: 'Phiên bản' }]
    case 'simulator-configurations': return [{ key: 'configurationId', label: 'Mã cấu hình' }, { key: 'sourceId', label: 'Nguồn dữ liệu' }, { key: 'currentConfigurationVersion', label: 'Bản hiện hành' }, { key: 'version', label: 'Phiên bản tổng hợp' }]
    default: return []
  }
}

function statusesFor(resource: string): string[] {
  switch (resource) {
    case 'sites': case 'areas': return ['Draft', 'Active', 'Inactive']
    case 'assets': return ['Draft', 'Active', 'Inactive', 'Decommissioned']
    case 'points': return ['Draft', 'Active', 'Inactive', 'Decommissioned']
    case 'data-sources': return ['Draft', 'Active', 'Suspended', 'Decommissioned']
    case 'source-point-mappings': return ['Draft', 'Active', 'Inactive', 'Superseded']
    default: return []
  }
}
