import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { ManagementFilter } from '../../gateways/webGateways'
import { DataTable, type DataTableColumn } from '../../components/data/DataTable'
import { FilterBar } from '../../components/data/FilterBar'
import { Pagination } from '../../components/data/Pagination'
import { FeedbackBanner } from '../../components/feedback/FeedbackBanner'
import { LoadingState } from '../../components/feedback/LoadingState'
import { EmptyState } from '../../components/feedback/EmptyState'
import { ErrorState } from '../../components/feedback/ErrorState'
import { ForbiddenState } from '../../components/feedback/ForbiddenState'
import { ConflictState } from '../../components/feedback/ConflictState'
import { BlockedState } from '../../components/feedback/BlockedState'
import { FormSection } from '../../components/forms/FormSection'
import { Field } from '../../components/forms/Field'
import { FieldErrorSummary, type FieldError } from '../../components/forms/FieldErrorSummary'
import { UnsavedChangesGuard } from '../../components/forms/UnsavedChangesGuard'
import { ConfirmDialog } from '../../components/dialogs/ConfirmDialog'
import { DetailPanel } from '../../components/disclosure/DetailPanel'
import { Drawer } from '../../components/disclosure/Drawer'
import { OperationalStatusBadge, type OperationalStatus } from '../../components/status/OperationalStatusBadge'
import {
  ActivateVersionButton,
  DuplicateButton,
  ManagementActionButton,
  configurationLifecyclePresentation,
  configurationValidationErrors,
  managementStateMessage,
  resourceLabel,
  sortManagementItems,
  textValue,
  useDebouncedSearch,
  type ManagementColumn,
  type ManagementFeedback,
  type ManagementItem,
  type ManagementState,
} from './ConfigurationManagementComponents'

const RESOURCE_KEYS = ['sites', 'areas', 'assets', 'points', 'data-sources', 'source-point-mappings', 'simulator-configurations'] as const
const emptyFilter: ManagementFilter = { page: 1, pageSize: 20 }
type OptionState = 'loading' | 'ready' | 'empty' | 'forbidden' | 'dependency' | 'runtime'
type OptionName = 'sites' | 'areas' | 'assets' | 'sources' | 'points'
type SelectName = 'site' | 'area' | 'asset' | 'source' | 'point'
type ReviewState = { id: string; sourceId: string; sourceLabel: string; draftVersion: number; relationships: string[]; excluded: string[]; reviewed: boolean; validated: boolean; relationshipStale: boolean; validationStale: boolean }

function statusOf(error: unknown): ManagementState {
  if (error instanceof Error && error.message === 'forbidden') return 'forbidden'
  if (error instanceof Error && error.message === 'expired') return 'expired'
  if (error instanceof Error && error.message.includes('request-503')) return 'dependency'
  return 'error'
}

function optionStateOf(error: unknown): OptionState {
  if (error instanceof Error && error.message === 'forbidden') return 'forbidden'
  if (error instanceof Error && error.message.includes('request-503')) return 'dependency'
  return 'runtime'
}

function optionMessage(name: OptionName, state: OptionState): string {
  const label = resourceLabel(name === 'sources' ? 'data-sources' : name === 'points' ? 'points' : name)
  if (state === 'forbidden') return `${label} không nằm trong phạm vi được cấp quyền.`
  if (state === 'dependency') return `Dịch vụ cung cấp ${label.toLocaleLowerCase('vi')} đang không sẵn sàng.`
  if (state === 'runtime') return `Không thể tải ${label.toLocaleLowerCase('vi')} do lỗi kết nối.`
  if (state === 'empty') return `Không có ${label.toLocaleLowerCase('vi')} hợp lệ trong phạm vi hiện tại.`
  return `Đang tải ${label.toLocaleLowerCase('vi')}…`
}

function idOf(item: ManagementItem): string { return textValue(item.id ?? item.configurationId) }

function reviewFromItem(item: ManagementItem): ReviewState | null {
  const id = textValue(item.configurationId ?? item.id)
  const draftVersion = Number(item.draftConfigurationVersion ?? 0)
  if (!id || draftVersion <= Number(item.currentConfigurationVersion ?? 0)) return null
  const sourceId = textValue(item.sourceId)
  const sourceLabel = [textValue(item.sourceCode), textValue(item.sourceName)].filter(Boolean).join(' – ') || sourceId
  const listValue = (value: unknown, fallback: string[]) => Array.isArray(value) ? value.map(String) : fallback
  return { id, sourceId, sourceLabel, draftVersion, relationships: listValue(item.reviewRelationships, ['Data Source']), excluded: listValue(item.excludedFields, []), reviewed: Boolean(item.relationshipReviewed) && !item.relationshipReceiptStale, validated: Boolean(item.validationRecorded) && !item.validationReceiptStale, relationshipStale: Boolean(item.relationshipReceiptStale), validationStale: Boolean(item.validationReceiptStale) }
}

function messageFor(result: { status: number; errorCode?: string }, action: string): string {
  const code = result.errorCode
  if (code === 'VERSION_CONFLICT' || result.status === 409) return `${action} thất bại: dữ liệu đã thay đổi, hãy tải lại và thử lại.`
  if (code === 'FORBIDDEN' || result.status === 403) return `${action} thất bại: bạn không có quyền trong phạm vi này.`
  if (code === 'NOT_FOUND' || result.status === 404) return `${action} thất bại: không tìm thấy thực thể.`
  if (code === 'DEPENDENCY_UNAVAILABLE' || result.status === 503) return `${action} thất bại: dịch vụ dữ liệu chưa sẵn sàng.`
  if (code === 'UNSUPPORTED_ACTION') return `${action} chưa được hỗ trợ: thao tác bị tắt theo quy tắc miền nghiệp vụ.`
  if (code === 'DEPENDENT_HISTORY' || code === 'INVALID_STATE' || code === 'INVALIDSTATE') return `${action} bị từ chối vì thực thể đang được tham chiếu hoặc có lịch sử.`
  return `${action} thất bại${code ? `: ${code}` : ` (HTTP ${result.status})`}.`
}

function editorFields(resource: string, mode: 'create' | 'edit'): Array<{ key: string; label: string; type?: string; readOnly?: boolean; help?: string; select?: SelectName }> {
  const common = [{ key: 'name', label: 'Tên', type: 'text' }]
  const immutable = (label: string) => ({ label, readOnly: true, help: 'Trường quan hệ thuộc miền sở hữu quản lý; không thể đổi trên bản ghi này.' })
  switch (resource) {
    case 'areas': return mode === 'create' ? [...common, { key: 'siteId', label: 'Địa điểm cha', select: 'site' as const }] : common
    case 'assets': return mode === 'create' ? [...common, { key: 'areaId', label: 'Khu vực cha', select: 'area' as const }] : common
    case 'points': return mode === 'create' ? [...common, { key: 'description', label: 'Mô tả', type: 'text' }, { key: 'assetId', label: 'Tài sản cha', select: 'asset' as const }, { key: 'metricId', label: 'Mã chỉ số', type: 'text' }, { key: 'unitId', label: 'Mã đơn vị', type: 'text' }, { key: 'dataOwnerUserId', label: 'Mã chủ dữ liệu', type: 'text' }, { key: 'expectedIntervalSeconds', label: 'Chu kỳ (giây)', type: 'number' }, { key: 'noDataAfterSeconds', label: 'No Data sau (giây)', type: 'number' }] : [...common, { key: 'description', label: 'Mô tả', type: 'text' }, { key: 'metricId', label: 'Mã chỉ số', type: 'text' }, { key: 'unitId', label: 'Mã đơn vị', type: 'text' }, { key: 'dataOwnerUserId', label: 'Mã chủ dữ liệu', type: 'text' }, { key: 'expectedIntervalSeconds', label: 'Chu kỳ (giây)', type: 'number' }, { key: 'noDataAfterSeconds', label: 'No Data sau (giây)', type: 'number' }]
    case 'data-sources': return mode === 'create' ? [...common, { key: 'siteId', label: 'Địa điểm', select: 'site' as const }] : common
    case 'source-point-mappings': return [{ key: 'sourceId', ...(mode === 'create' ? { label: 'Nguồn dữ liệu', select: 'source' as const } : immutable('Mã nguồn dữ liệu')) }, { key: 'pointId', ...(mode === 'create' ? { label: 'Điểm đo', select: 'point' as const } : immutable('Mã điểm đo')) }, { key: 'effectiveFromUtc', label: 'Hiệu lực từ', type: 'datetime-local' }, { key: 'effectiveToUtc', label: 'Hiệu lực đến', type: 'datetime-local' }]
    case 'simulator-configurations': return [{ key: 'sourceId', ...(mode === 'create' ? { label: 'Nguồn dữ liệu', select: 'source' as const } : immutable('Mã nguồn dữ liệu')) }, { key: 'scenarioType', label: 'Kịch bản', type: 'text' }, { key: 'minimumValue', label: 'Giá trị nhỏ nhất', type: 'number' }, { key: 'maximumValue', label: 'Giá trị lớn nhất', type: 'number' }, { key: 'intervalSeconds', label: 'Chu kỳ (giây)', type: 'number' }, { key: 'deterministicSeed', label: 'Hạt giống xác định', type: 'number' }]
    default: return common
  }
}

function defaultForm(resource: string, siteId?: string): Record<string, string> {
  const result: Record<string, string> = { name: '' }
  if (resource === 'areas' || resource === 'data-sources') result.siteId = siteId ?? ''
  if (resource === 'source-point-mappings' || resource === 'simulator-configurations') result.sourceId = ''
  if (resource === 'source-point-mappings') result.pointId = ''
  if (resource === 'points') { result.expectedIntervalSeconds = '60'; result.noDataAfterSeconds = '180' }
  if (resource === 'simulator-configurations') { result.scenarioType = 'Constant'; result.minimumValue = '42'; result.maximumValue = '42'; result.intervalSeconds = '60'; result.deterministicSeed = '42' }
  return result
}

function formFromItem(resource: string, item: ManagementItem): Record<string, string> {
  const form = defaultForm(resource)
  for (const field of editorFields(resource, 'edit')) form[field.key] = textValue(item[field.key])
  if (resource === 'source-point-mappings') { form.sourceId = textValue(item.dataSourceId); form.pointId = textValue(item.pointId); form.effectiveFromUtc = textValue(item.effectiveFrom); form.effectiveToUtc = textValue(item.effectiveTo) }
  return form
}

function normalizedForm(form: Record<string, string>): Record<string, unknown> {
  const result: Record<string, unknown> = { ...form }
  for (const key of ['expectedIntervalSeconds', 'noDataAfterSeconds', 'intervalSeconds', 'deterministicSeed', 'minimumValue', 'maximumValue']) if (result[key]) result[key] = Number(result[key])
  return result
}

function lifecycleActions(resource: string, status: string): string[] {
  if (status === 'Draft') return resource === 'data-sources' ? ['activate', 'decommission'] : resource === 'source-point-mappings' ? ['activate'] : ['activate']
  if (status === 'Active') return resource === 'data-sources' ? ['suspend', 'decommission'] : resource === 'source-point-mappings' ? ['inactivate', 'supersede'] : ['deactivate']
  if (status === 'Suspended') return resource === 'data-sources' ? ['activate', 'decommission'] : []
  if (status === 'Inactive' && resource === 'source-point-mappings') return ['supersede']
  return []
}

function actionLabel(action: string): string { return ({ activate: 'Kích hoạt', deactivate: 'Tắt hoạt động', decommission: 'Ngừng sử dụng', suspend: 'Tạm dừng', inactivate: 'Đặt không hoạt động', supersede: 'Thay thế' } as Record<string, string>)[action] ?? action }
function canDelete(resource: string, status: string): boolean { return (resource === 'data-sources' || resource === 'source-point-mappings') && status === 'Draft' }
function statusesFor(resource: string): string[] { if (resource === 'sites' || resource === 'areas') return ['Draft', 'Active', 'Inactive']; if (resource === 'assets' || resource === 'points') return ['Draft', 'Active', 'Inactive', 'Decommissioned']; if (resource === 'data-sources') return ['Draft', 'Active', 'Suspended', 'Decommissioned']; if (resource === 'source-point-mappings') return ['Draft', 'Active', 'Inactive', 'Superseded']; return [] }

function columnsFor(resource: string): ManagementColumn[] {
  switch (resource) {
    case 'sites': return [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'timezone', label: 'Múi giờ' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'areas': case 'assets': return [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'points': return [{ key: 'code', label: 'Mã' }, { key: 'metricId', label: 'Chỉ số' }, { key: 'unitId', label: 'Đơn vị' }, { key: 'dataOwnerUserId', label: 'Chủ dữ liệu' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'data-sources': return [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'sourceType', label: 'Loại nguồn' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'source-point-mappings': return [{ key: 'pointId', label: 'Điểm đo' }, { key: 'status', label: 'Trạng thái' }, { key: 'effectiveFrom', label: 'Hiệu lực từ', render: item => new Date(textValue(item.effectiveFrom)).toLocaleString('vi-VN') }, { key: 'effectiveTo', label: 'Đến', render: item => item.effectiveTo ? new Date(textValue(item.effectiveTo)).toLocaleString('vi-VN') : '—' }, { key: 'version', label: 'Phiên bản' }]
    case 'simulator-configurations': return [{ key: 'configurationId', label: 'Mã cấu hình' }, { key: 'sourceId', label: 'Nguồn dữ liệu' }, { key: 'currentConfigurationVersion', label: 'Bản hiện hành' }, { key: 'version', label: 'Phiên bản tổng hợp' }]
    default: return []
  }
}

function statusBadge(status: string): ReactNode {
  const mapping: Record<string, OperationalStatus> = { Active: 'Available', Draft: 'Pending', Suspended: 'Blocked', Inactive: 'Unavailable', Decommissioned: 'Unavailable', Superseded: 'Unavailable' }
  const presentation = configurationLifecyclePresentation(status)
  return <OperationalStatusBadge status={mapping[status] ?? 'Unavailable'} detail={`${presentation.label} · ${presentation.cue}`} />
}

export function ConfigurationManagementRoutes() {
  const gateways = useWebGateways()
  const [resource, setResource] = useState<string>('sites')
  const [filter, setFilter] = useState<ManagementFilter>(emptyFilter)
  const [state, setState] = useState<ManagementState>('loading')
  const [items, setItems] = useState<ManagementItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [options, setOptions] = useState<Record<OptionName, Array<{ id: string; label: string }>>>({ sites: [], areas: [], assets: [], sources: [], points: [] })
  const [optionStates, setOptionStates] = useState<Record<OptionName, OptionState>>({ sites: 'loading', areas: 'loading', assets: 'loading', sources: 'loading', points: 'loading' })
  const [optionRetryNonce, setOptionRetryNonce] = useState(0)
  const [busyItem, setBusyItem] = useState<string | null>(null)
  const [feedback, setFeedback] = useState<ManagementFeedback>(null)
  const [refreshNonce, setRefreshNonce] = useState(0)
  const [detail, setDetail] = useState<ManagementItem | null>(null)
  const [editor, setEditor] = useState<'create' | 'edit' | null>(null)
  const [form, setForm] = useState<Record<string, string>>({})
  const [initialForm, setInitialForm] = useState<Record<string, string>>({})
  const [review, setReview] = useState<ReviewState | null>(null)
  const [duplicateSourceId, setDuplicateSourceId] = useState('')
  const [invalidField, setInvalidField] = useState<string | null>(null)
  const [submitAttempt, setSubmitAttempt] = useState(0)
  const [sort, setSort] = useState<{ key: string; direction: 'ascending' | 'descending' }>({ key: 'name', direction: 'ascending' })
  const [pendingAction, setPendingAction] = useState<{ kind: 'lifecycle' | 'remove'; item: ManagementItem; action?: string } | null>(null)
  const [discardChangesOpen, setDiscardChangesOpen] = useState(false)
  const search = useDebouncedSearch(filter.search ?? '')

  useEffect(() => {
    let cancelled = false
    setState('loading')
    void gateways.management.list(resource, { ...filter, search: search || undefined }).then(page => { if (!cancelled) { setItems(page.items); setTotalCount(page.totalCount); setState(page.items.length ? 'ready' : 'no-data') } }).catch(error => { if (!cancelled) setState(statusOf(error)) })
    return () => { cancelled = true }
  }, [filter, gateways.management, refreshNonce, resource, search])

  useEffect(() => {
    let cancelled = false
    const requests: Array<[OptionName, Promise<{ items: Array<Record<string, unknown>> }>]> = [
      ['sites', gateways.management.list('sites', { page: 1, pageSize: 200 })],
      ['areas', gateways.management.list('areas', { page: 1, pageSize: 200 })],
      ['assets', gateways.management.list('assets', { page: 1, pageSize: 200 })],
      ['sources', gateways.management.list('data-sources', { page: 1, pageSize: 200, status: 'Active' })],
      ['points', gateways.management.list('points', { page: 1, pageSize: 200 })],
    ]
    setOptionStates({ sites: 'loading', areas: 'loading', assets: 'loading', sources: 'loading', points: 'loading' })
    void Promise.allSettled(requests.map(([, request]) => request)).then(results => { if (cancelled) return; requests.forEach(([name], index) => { const result = results[index]; if (result.status === 'fulfilled') { const values = result.value.items.map(item => ({ id: textValue(item.id), label: `${textValue(item.code)} – ${textValue(item.name)}` })).filter(value => value.id); setOptions(current => ({ ...current, [name]: values })); setOptionStates(current => ({ ...current, [name]: values.length ? 'ready' : 'empty' })) } else { setOptions(current => ({ ...current, [name]: [] })); setOptionStates(current => ({ ...current, [name]: optionStateOf(result.reason) })) } }) })
    return () => { cancelled = true }
  }, [gateways.management, optionRetryNonce, refreshNonce])

  const reload = () => { setFilter(current => ({ ...current, page: 1 })); setRefreshNonce(value => value + 1) }
  const formDirty = editor !== null && JSON.stringify(form) !== JSON.stringify(initialForm)
  const columns = columnsFor(resource)
  const sortedItems = sortManagementItems(items, columns.some(column => column.key === sort.key) ? sort.key : (columns[0]?.key ?? 'name'), sort.direction)

  function beginCreate() { const next = defaultForm(resource, filter.siteId); setForm(next); setInitialForm(next); setEditor('create'); setDetail(null); setReview(null); setInvalidField(null); setSubmitAttempt(0); setFeedback(null) }
  function beginEdit(item: ManagementItem) { const next = formFromItem(resource, item); setForm(next); setInitialForm(next); setEditor('edit'); setDetail(item); setReview(resource === 'simulator-configurations' ? reviewFromItem(item) : null); setInvalidField(null); setSubmitAttempt(0); setFeedback(null) }
  function closeEditor() { if (formDirty) setDiscardChangesOpen(true); else setEditor(null) }

  async function openDetail(item: ManagementItem) { const id = idOf(item); if (!id) return; try { const loaded = await gateways.management.detail(resource, id); if (!loaded) { setState('not-found'); return }; setDetail(loaded as ManagementItem); setReview(resource === 'simulator-configurations' ? reviewFromItem(loaded as ManagementItem) : null) } catch (error) { setState(statusOf(error)) } }
  async function submitEditor() {
    const body = normalizedForm(form); const errors = editor ? configurationValidationErrors(resource, editor, body) : []; setSubmitAttempt(value => value + 1)
    if (errors.length) { setInvalidField(errors[0].key); setState('validation'); setFeedback({ tone: 'warning', message: errors[0].message }); return }
    setInvalidField(null); setBusyItem(editor === 'edit' && detail ? idOf(detail) : 'create')
    const result = editor === 'create' ? await gateways.management.create(resource, body) : detail ? await gateways.management.update(resource, idOf(detail), Number(detail.version ?? 0), body) : { ok: false, status: 404, errorCode: 'NOT_FOUND' }
    setBusyItem(null)
    if (!result.ok) { setState(result.status === 409 ? 'conflict' : result.status === 422 ? 'validation' : result.status === 503 ? 'dependency' : 'error'); setFeedback({ tone: 'error', message: messageFor(result, editor === 'create' ? 'Tạo mới' : 'Cập nhật') }); return }
    setEditor(null); setInitialForm({}); setFeedback({ tone: 'success', message: `${editor === 'create' ? 'Đã tạo' : 'Đã cập nhật'} ${resourceLabel(resource)} thành công.` }); reload()
  }
  async function duplicate(item: ManagementItem) { const id = idOf(item); if (!id) return; if (resource === 'simulator-configurations' && (!duplicateSourceId || duplicateSourceId === textValue(item.sourceId))) { setState('validation'); setFeedback({ tone: 'warning', message: 'Hãy chọn một Nguồn dữ liệu đích khác với nguồn hiện tại.' }); return }; setBusyItem(id); const result = await gateways.management.duplicate(resource, id, resource === 'simulator-configurations' ? duplicateSourceId : undefined); setBusyItem(null); if (!result.ok) { setFeedback({ tone: 'error', message: messageFor(result, 'Nhân bản') }); return }; setFeedback({ tone: 'success', message: `Đã tạo bản nháp ${resourceLabel(resource)} mới.` }); reload() }
  async function reviewRelationships(target: ReviewState | null) { if (!target) return; setBusyItem(target.id); const result = await gateways.management.reviewSimulatorConfiguration(target.id, target.draftVersion); setBusyItem(null); if (!result.ok) { setFeedback({ tone: 'error', message: messageFor(result, 'Xem xét quan hệ') }); return }; setFeedback({ tone: 'success', message: 'Đã lưu biên nhận xem xét quan hệ trên máy chủ.' }); const loaded = await gateways.management.detail('simulator-configurations', target.id); if (loaded) { setDetail(loaded as ManagementItem); setReview(reviewFromItem(loaded as ManagementItem)) }; reload() }
  async function validate(item: ManagementItem) { const id = idOf(item); if (resource === 'simulator-configurations' && !reviewFromItem(item)?.reviewed) { setFeedback({ tone: 'warning', message: 'Cần xem xét quan hệ trên máy chủ trước khi kiểm tra bản nháp.' }); return }; setBusyItem(id); const result = await gateways.management.validate(resource, id); setBusyItem(null); if (!result.ok) { setFeedback({ tone: 'error', message: messageFor(result, 'Kiểm tra') }); return }; setFeedback({ tone: 'success', message: `Kiểm tra ${resourceLabel(resource)} thành công.` }); reload() }
  function lifecycle(item: ManagementItem, action: string) { if (action === 'activate' && resource === 'simulator-configurations' && !reviewFromItem(item)?.validated) { setFeedback({ tone: 'warning', message: 'Cần xem xét quan hệ và kiểm tra bản nháp trước khi kích hoạt.' }); return }; setPendingAction({ kind: 'lifecycle', item, action }) }
  function remove(item: ManagementItem) { setPendingAction({ kind: 'remove', item }) }
  async function confirmPendingAction() { const pending = pendingAction; if (!pending) return; setPendingAction(null); const id = idOf(pending.item); setBusyItem(id); const result = pending.kind === 'remove' ? await gateways.management.remove(resource, id, Number(pending.item.version ?? 0)) : await gateways.management.lifecycle(resource, id, pending.action ?? '', Number(pending.item.version ?? 0)); setBusyItem(null); if (!result.ok) { setFeedback({ tone: 'error', message: messageFor(result, pending.kind === 'remove' ? 'Xóa' : 'Chuyển trạng thái') }); return }; setFeedback({ tone: 'success', message: pending.kind === 'remove' ? `Đã xóa bản nháp ${resourceLabel(resource).toLocaleLowerCase('vi')}.` : `Đã ${pending.action} ${resourceLabel(resource).toLocaleLowerCase('vi')}.` }); reload() }
  async function activate(item: ManagementItem) { const id = textValue(item.configurationId); const review = reviewFromItem(item); if (!review?.reviewed || !review.validated || review.relationshipStale || review.validationStale) { setFeedback({ tone: 'warning', message: 'Cần xem xét quan hệ và kiểm tra bản nháp trước khi kích hoạt.' }); return }; setBusyItem(id); const result = await gateways.management.activateSimulatorConfigurationVersion(id, Number(item.version ?? 0), Number(item.draftConfigurationVersion ?? 0)); setBusyItem(null); if (!result.ok) { setFeedback({ tone: 'error', message: messageFor(result, 'Kích hoạt') }); return }; setFeedback({ tone: 'success', message: 'Đã kích hoạt bản cấu hình mô phỏng.' }); reload() }

  const optionLists = options
  const createOptionNames: OptionName[] = resource === 'areas' || resource === 'data-sources' ? ['sites'] : resource === 'assets' ? ['areas'] : resource === 'points' ? ['assets'] : resource === 'source-point-mappings' ? ['sources', 'points'] : resource === 'simulator-configurations' ? ['sources'] : []
  const unavailable = createOptionNames.find(name => optionStates[name] !== 'ready')
  const errors: FieldError[] = editor ? configurationValidationErrors(resource, editor, normalizedForm(form)).map(error => ({ id: `configuration-field-${error.key}`, label: error.key, message: error.message })) : []

  return <section className="page configuration-page" aria-labelledby="configuration-management-title">
    <div className="page-heading"><div><p className="eyebrow">Quản lý cấu hình</p><h1 id="configuration-management-title">Cấu hình vận hành</h1><p className="lede">Quản lý bảy nhóm cấu hình trong phạm vi được cấp quyền, với trạng thái, phiên bản và hành động có thể truy xuất.</p></div><div className="actions-stack"><span className="badge badge-neutral">{resourceLabel(resource)}</span><ManagementActionButton label="Tạo mới" tone="primary" onClick={beginCreate} disabled={Boolean(unavailable)} title={unavailable ? optionMessage(unavailable, optionStates[unavailable]) : undefined} /></div></div>
    <nav className="tabs entity-tabs" aria-label="Loại cấu hình">{RESOURCE_KEYS.map(value => <button key={value} type="button" className={`tab ${resource === value ? 'tab-active' : ''}`} aria-current={resource === value ? 'page' : undefined} onClick={() => { setResource(value); setFilter(emptyFilter); setDetail(null); setEditor(null); setReview(null); setFeedback(null) }}>{resourceLabel(value)}</button>)}</nav>
    <FilterBar fields={[{ id: 'search', label: 'Tìm kiếm', value: filter.search ?? '', placeholder: 'Mã, tên hoặc định danh…', type: 'search' }]} onChange={(id, value) => { if (id === 'search') setFilter(current => ({ ...current, search: value, page: 1 })) }} onSubmit={(event: FormEvent<HTMLFormElement>) => event.preventDefault()} onReset={() => setFilter(current => ({ ...current, search: undefined, status: undefined, siteId: undefined, areaId: undefined, page: 1 }))} resultCount={totalCount}>
      <label className="field compact-filter"><span>Trạng thái</span><select className="input" value={filter.status ?? ''} onChange={event => setFilter(current => ({ ...current, status: event.target.value || undefined, page: 1 }))}><option value="">Tất cả</option>{statusesFor(resource).map(value => <option key={value}>{value}</option>)}</select></label>
      <label className="field compact-filter"><span>Địa điểm</span><select className="input" value={filter.siteId ?? ''} onChange={event => setFilter(current => ({ ...current, siteId: event.target.value || undefined, page: 1 }))}><option value="">Tất cả</option>{optionLists.sites.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select></label>
      <label className="field compact-filter"><span>Khu vực</span><select className="input" value={filter.areaId ?? ''} onChange={event => setFilter(current => ({ ...current, areaId: event.target.value || undefined, page: 1 }))}><option value="">Tất cả</option>{optionLists.areas.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select></label>
    </FilterBar>
    {resource === 'simulator-configurations' && <label className="field inline-control"><span>Nguồn đích nhân bản</span><select className="input" value={duplicateSourceId} disabled={optionStates.sources !== 'ready'} onChange={event => setDuplicateSourceId(event.target.value)}><option value="">Chọn nguồn đích</option>{optionLists.sources.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select><small className="muted">Bản nháp phải gắn với một Source được cấp quyền; không tự chọn phần tử đầu tiên.</small></label>}
    {feedback && <FeedbackBanner tone={feedback.tone === 'error' ? 'danger' : feedback.tone} message={feedback.message} />}
    {review && <RelationshipReview review={review} onReviewed={() => void reviewRelationships(review)} />}
    {editor && <UnsavedChangesGuard when={formDirty}><EditorPanel resource={resource} mode={editor} form={form} onFieldChange={(key, value) => { setForm(current => ({ ...current, [key]: value })); if (invalidField === key) setInvalidField(null) }} invalidField={invalidField} errors={errors} activationKey={submitAttempt} optionLists={optionLists} optionStates={optionStates} busy={busyItem !== null} onSave={() => void submitEditor()} onCancel={closeEditor} onRetry={() => setOptionRetryNonce(value => value + 1)} /></UnsavedChangesGuard>}
    {detail && <Drawer open title={`Chi tiết ${resourceLabel(resource)}`} onClose={() => setDetail(null)}><DetailPanel title={`Chi tiết ${resourceLabel(resource)}`} description="Thông tin được giới hạn theo phạm vi quyền hiện tại." action={<ManagementActionButton label="Đóng" onClick={() => setDetail(null)} tone="quiet" />}><dl className="detail-grid">{Object.entries(detail).filter(([key]) => !key.toLowerCase().includes('secret') && !key.toLowerCase().includes('token')).map(([key, value]) => <div key={key}><dt>{key}</dt><dd>{textValue(value) || '—'}</dd></div>)}</dl></DetailPanel></Drawer>}
    <div className="table-toolbar"><div><p className="eyebrow">Danh sách hiện tại</p><p className="muted">Bảng gọn cho thao tác dài hạn; sắp xếp chỉ áp dụng trên trang đã tải.</p></div><label className="field compact-filter"><span>Sắp xếp trang hiện tại</span><select className="input" value={`${sort.key}:${sort.direction}`} onChange={event => { const [key, direction] = event.target.value.split(':') as [string, 'ascending' | 'descending']; setSort({ key, direction }) }}>{columns.map(column => <option key={`${column.key}:ascending`} value={`${column.key}:ascending`}>{column.label} ↑</option>)}{columns.map(column => <option key={`${column.key}:descending`} value={`${column.key}:descending`}>{column.label} ↓</option>)}</select></label></div>
    <ConfigurationTable state={state} resource={resource} columns={columns} items={sortedItems} totalCount={totalCount} filter={filter} busyItem={busyItem} onPageChange={page => setFilter(current => ({ ...current, page }))} onDetail={item => void openDetail(item)} onEdit={beginEdit} onDuplicate={duplicate} onValidate={validate} onLifecycle={lifecycle} onRemove={remove} onActivate={activate} />
    <ConfirmDialog open={Boolean(pendingAction)} title="Xác nhận thay đổi trạng thái" description={pendingAction?.kind === 'remove' ? 'Chỉ bản nháp an toàn mới được xóa. Không có lý do được thu thập vì hợp đồng hiện tại không lưu trường reason.' : `Thao tác ${actionLabel(pendingAction?.action ?? '')} sẽ được gửi với phiên bản hiện tại và có thể bị từ chối nếu dữ liệu đã thay đổi.`} onCancel={() => setPendingAction(null)} onConfirm={() => void confirmPendingAction()} confirmLabel="Xác nhận" />
    <ConfirmDialog open={discardChangesOpen} title="Bỏ thay đổi chưa lưu?" description="Các trường đã sửa sẽ bị bỏ. Bạn có thể tiếp tục chỉnh sửa hoặc hủy bỏ thay đổi." onCancel={() => setDiscardChangesOpen(false)} onConfirm={() => { setDiscardChangesOpen(false); setEditor(null) }} confirmLabel="Bỏ thay đổi" />
  </section>
}

function ConfigurationTable({ state, resource, columns, items, totalCount, filter, busyItem, onPageChange, onDetail, onEdit, onDuplicate, onValidate, onLifecycle, onRemove, onActivate }: { state: ManagementState; resource: string; columns: ManagementColumn[]; items: ManagementItem[]; totalCount: number; filter: ManagementFilter; busyItem: string | null; onPageChange: (page: number) => void; onDetail: (item: ManagementItem) => void; onEdit: (item: ManagementItem) => void; onDuplicate: (item: ManagementItem) => void; onValidate: (item: ManagementItem) => void; onLifecycle: (item: ManagementItem, action: string) => void; onRemove: (item: ManagementItem) => void; onActivate: (item: ManagementItem) => void }) {
  const stateMessage = managementStateMessage(state, resource, `Chưa có ${resourceLabel(resource).toLocaleLowerCase('vi')} nào trong phạm vi hiện tại.`)
  if (stateMessage?.tone === 'loading') return <LoadingState message={stateMessage.message} />
  if (stateMessage?.tone === 'empty') return <EmptyState title={stateMessage.title} message={stateMessage.message} />
  if (stateMessage?.tone === 'forbidden') return <ForbiddenState message={stateMessage.message} />
  if (stateMessage?.tone === 'conflict') return <ConflictState message={stateMessage.message} />
  if (stateMessage?.tone === 'blocked') return <BlockedState message={stateMessage.message} />
  if (stateMessage) return <ErrorState message={stateMessage.message} />
  const tableColumns: DataTableColumn<ManagementItem>[] = columns.map(column => ({ key: column.key, header: column.label, render: item => column.key === 'status' ? statusBadge(textValue(item.status)) : column.render ? column.render(item) : textValue(item[column.key]) }))
  return <><DataTable caption={`${resourceLabel(resource)} · sắp xếp trang hiện tại`} columns={tableColumns} rows={items} rowKey={(item, index) => idOf(item) || String(index)} rowAction={item => <div className="actions-stack"><ManagementActionButton label="Chi tiết" onClick={() => onDetail(item)} /><ManagementActionButton label="Sửa" onClick={() => onEdit(item)} disabled={busyItem !== null} /><ManagementActionButton label="Kiểm tra" onClick={() => onValidate(item)} disabled={busyItem === idOf(item)} /><DuplicateButton item={item} busyItem={busyItem} onDuplicate={onDuplicate} />{lifecycleActions(resource, textValue(item.status)).map(action => <ManagementActionButton key={action} label={actionLabel(action)} onClick={() => onLifecycle(item, action)} disabled={busyItem === idOf(item)} tone={action === 'decommission' ? 'danger' : 'secondary'} />)}{resource === 'simulator-configurations' && <ActivateVersionButton item={item} busyItem={busyItem} onActivate={onActivate} />}{canDelete(resource, textValue(item.status)) && <ManagementActionButton label="Xóa bản nháp" tone="danger" onClick={() => onRemove(item)} disabled={busyItem === idOf(item)} />}</div>} /><Pagination page={filter.page} pageSize={filter.pageSize} total={totalCount} onPageChange={onPageChange} /></>
}

function RelationshipReview({ review, onReviewed }: { review: ReviewState; onReviewed: () => void }) { return <section className="card form-card" aria-labelledby="relationship-review-title"><h2 id="relationship-review-title">Xem xét quan hệ bản nháp</h2><p className="muted">Bản nháp <code>{review.id}</code> phiên bản {review.draftVersion}, Source {review.sourceLabel}, chỉ được kích hoạt sau khi biên nhận quan hệ và kiểm tra được lưu trên máy chủ.</p><p><strong>Quan hệ được sao chép:</strong> {review.relationships.length ? review.relationships.join(', ') : 'Không có quan hệ tự động.'}</p><p><strong>Trường bị loại trừ:</strong> {review.excluded.length ? review.excluded.join(', ') : 'Không có dữ liệu lịch sử hoặc bí mật được sao chép.'}</p><ManagementActionButton label={review.reviewed ? 'Đã xem xét' : 'Xem xét quan hệ'} onClick={onReviewed} disabled={review.reviewed} tone="primary" /></section> }

function EditorPanel({ resource, mode, form, onFieldChange, invalidField, errors, activationKey, optionLists, optionStates, busy, onSave, onCancel, onRetry }: { resource: string; mode: 'create' | 'edit'; form: Record<string, string>; onFieldChange: (key: string, value: string) => void; invalidField: string | null; errors: FieldError[]; activationKey: number; optionLists: Record<OptionName, Array<{ id: string; label: string }>>; optionStates: Record<OptionName, OptionState>; busy: boolean; onSave: () => void; onCancel: () => void; onRetry: () => void }) {
  const fields = editorFields(resource, mode)
  const selectOptions = { site: optionLists.sites, area: optionLists.areas, asset: optionLists.assets, source: optionLists.sources, point: optionLists.points } as Record<SelectName, Array<{ id: string; label: string }>>
  const optionFor = (select: SelectName): OptionName => select === 'site' ? 'sites' : select === 'area' ? 'areas' : select === 'asset' ? 'assets' : select === 'source' ? 'sources' : 'points'
  const unavailable = fields.find(field => field.select && optionStates[optionFor(field.select)] !== 'ready')
  return <section className="card form-card" aria-labelledby="configuration-editor-title"><FormSection title={`${mode === 'create' ? 'Tạo mới' : 'Chỉnh sửa'} ${resourceLabel(resource)}`} description="Thay đổi được gửi như bản nháp và chịu kiểm tra phiên bản ở máy chủ.">
    <FieldErrorSummary errors={errors} activationKey={activationKey} />
    {unavailable && <div className="notice notice-warning" role="alert">{optionMessage(optionFor(unavailable.select!), optionStates[optionFor(unavailable.select!)] )}<ManagementActionButton label="Thử lại" onClick={onRetry} tone="quiet" /></div>}
    <div className="form-grid">{fields.map(field => {
      const fieldError = invalidField === field.key ? errors.find(error => error.id.endsWith(field.key))?.message : undefined
      const required = field.key === 'name' || (mode === 'create' && Boolean(field.select))
      const describedBy = fieldError ? `configuration-field-${field.key}-error` : undefined
      const inputProps = { id: `configuration-field-${field.key}`, className: 'input', 'aria-invalid': fieldError ? true : undefined, 'aria-describedby': describedBy, required }
      return <Field key={field.key} id={`configuration-field-${field.key}`} label={field.label} required={required} error={fieldError} helper={field.help}>{field.select ? <select {...inputProps} value={form[field.key] ?? ''} disabled={optionStates[optionFor(field.select)] !== 'ready'} onChange={event => onFieldChange(field.key, event.target.value)}><option value="">Chọn {field.label.toLocaleLowerCase('vi')}</option>{selectOptions[field.select].map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select> : <input {...inputProps} type={field.type ?? 'text'} value={form[field.key] ?? ''} readOnly={field.readOnly} onChange={event => onFieldChange(field.key, event.target.value)} />}</Field>
    })}</div>
    <div className="actions-stack"><ManagementActionButton label={busy ? 'Đang lưu…' : 'Lưu bản nháp'} tone="primary" disabled={busy || Boolean(unavailable)} onClick={onSave} /><ManagementActionButton label="Hủy" onClick={onCancel} /></div>
  </FormSection></section>
}
