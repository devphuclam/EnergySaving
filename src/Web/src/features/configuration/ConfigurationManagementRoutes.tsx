import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { ManagementFilter, ManagementMutation } from '../../gateways/webGateways'
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
  actionLabelFor,
  canDeleteResource,
  configurationFormDirty,
  configurationLifecyclePresentation,
  detailFieldsFor,
  duplicateIdentityFromResult,
  effectiveConfigurationSort,
  isRetryableManagementMutationResult,
  lifecycleActionsFor,
  managementStateMessage,
  normalizeConfigurationForm,
  resourceLabel,
  safeConfigurationDate,
  sameManagementMutationIntent,
  simulatorActivationReadiness,
  sortManagementItems,
  statusesForResource,
  textValue,
  type ConfigurationMutationIntent,
  type ManagementColumn,
  type ManagementFeedback,
  type ManagementItem,
  type ManagementState,
  type OptionName,
  type OptionState,
  type SortDirection,
} from './ConfigurationManagementComponents'

const RESOURCE_KEYS = ['sites', 'areas', 'assets', 'points', 'data-sources', 'source-point-mappings', 'simulator-configurations'] as const
const emptyFilter: ManagementFilter = { page: 1, pageSize: 20 }
type DetailState = 'loading' | 'ready' | 'forbidden' | 'expired' | 'not-found' | 'error'
type ConfigurationTransition = { kind: 'tab'; resource: string } | { kind: 'create' } | { kind: 'edit'; item: ManagementItem } | { kind: 'close-editor' }
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
  if (error instanceof Error && error.message === 'expired') return 'expired'
  if (error instanceof Error && error.message.includes('request-503')) return 'dependency'
  return 'runtime'
}

function optionMessage(name: OptionName, state: OptionState): string {
  const label = resourceLabel(name === 'sources' ? 'data-sources' : name === 'points' ? 'points' : name)
  if (state === 'forbidden') return `${label} không nằm trong phạm vi được cấp quyền.`
  if (state === 'dependency') return `Dịch vụ cung cấp ${label.toLocaleLowerCase('vi')} đang không sẵn sàng.`
  if (state === 'runtime') return `Không thể tải ${label.toLocaleLowerCase('vi')} do lỗi kết nối.`
  if (state === 'empty') return `Không có ${label.toLocaleLowerCase('vi')} hợp lệ trong phạm vi hiện tại.`
  if (state === 'expired') return `Phiên đã hết hạn; vui lòng đăng nhập lại để tải ${label.toLocaleLowerCase('vi')}.`
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

function columnsFor(resource: string): ManagementColumn[] {
  switch (resource) {
    case 'sites': return [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'timezone', label: 'Múi giờ' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'areas': case 'assets': return [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'points': return [{ key: 'code', label: 'Mã' }, { key: 'metricId', label: 'Chỉ số' }, { key: 'unitId', label: 'Đơn vị' }, { key: 'dataOwnerUserId', label: 'Chủ dữ liệu' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'data-sources': return [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'sourceType', label: 'Loại nguồn' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }]
    case 'source-point-mappings': return [{ key: 'pointId', label: 'Điểm đo' }, { key: 'status', label: 'Trạng thái' }, { key: 'effectiveFrom', label: 'Hiệu lực từ', render: item => safeConfigurationDate(item.effectiveFrom) }, { key: 'effectiveTo', label: 'Đến', render: item => safeConfigurationDate(item.effectiveTo) }, { key: 'version', label: 'Phiên bản' }]
    case 'simulator-configurations': return [{ key: 'configurationId', label: 'Mã cấu hình' }, { key: 'sourceId', label: 'Nguồn dữ liệu' }, { key: 'currentConfigurationVersion', label: 'Bản hiện hành' }, { key: 'version', label: 'Phiên bản tổng hợp' }]
    default: return []
  }
}

function statusBadge(status: string): ReactNode {
  const mapping: Record<string, OperationalStatus> = { Active: 'Available', Draft: 'Pending', Suspended: 'Blocked', Inactive: 'Unavailable', Decommissioned: 'Unavailable', Superseded: 'Unavailable' }
  const presentation = configurationLifecyclePresentation(status)
  return <OperationalStatusBadge status={mapping[status] ?? 'Unavailable'} detail={`${presentation.label} · ${presentation.cue}`} />
}

export function ConfigurationManagementRoutes({ onSessionRecovery }: { onSessionRecovery?: () => void }) {
  const gateways = useWebGateways()
  const [resource, setResource] = useState<string>('sites')
  const [draftFilter, setDraftFilter] = useState<ManagementFilter>(emptyFilter)
  const [appliedFilter, setAppliedFilter] = useState<ManagementFilter>(emptyFilter)
  const [listState, setListState] = useState<ManagementState>('loading')
  const [items, setItems] = useState<ManagementItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [options, setOptions] = useState<Record<OptionName, Array<{ id: string; label: string }>>>({ sites: [], areas: [], assets: [], sources: [], points: [] })
  const [optionStates, setOptionStates] = useState<Record<OptionName, OptionState>>({ sites: 'loading', areas: 'loading', assets: 'loading', sources: 'loading', points: 'loading' })
  const [optionRetryNonce, setOptionRetryNonce] = useState(0)
  const [busyItem, setBusyItem] = useState<string | null>(null)
  const [feedback, setFeedback] = useState<ManagementFeedback>(null)
  const [mutationRetry, setMutationRetry] = useState<{ intent: ConfigurationMutationIntent; retryKey: string } | null>(null)
  const mutationRetryRun = useRef<(() => void) | null>(null)
  const [refreshNonce, setRefreshNonce] = useState(0)
  const [detailRecord, setDetailRecord] = useState<ManagementItem | null>(null)
  const [detailState, setDetailState] = useState<DetailState | null>(null)
  const detailRequestToken = useRef(0)
  const [editingRecord, setEditingRecord] = useState<ManagementItem | null>(null)
  const [editorMode, setEditorMode] = useState<'create' | 'edit' | null>(null)
  const [form, setForm] = useState<Record<string, string>>({})
  const [initialForm, setInitialForm] = useState<Record<string, string>>({})
  const [review, setReview] = useState<ReviewState | null>(null)
  const [duplicateSourceId, setDuplicateSourceId] = useState('')
  const [invalidField, setInvalidField] = useState<string | null>(null)
  const [submitAttempt, setSubmitAttempt] = useState(0)
  const [sort, setSort] = useState<{ key: string; direction: SortDirection }>({ key: '', direction: 'ascending' })
  const [pendingAction, setPendingAction] = useState<{ kind: 'lifecycle' | 'remove'; item: ManagementItem; action?: string } | null>(null)
  const [discardChangesOpen, setDiscardChangesOpen] = useState(false)
  const [pendingTransition, setPendingTransition] = useState<ConfigurationTransition | null>(null)

  useEffect(() => {
    let cancelled = false
    setListState('loading')
    void gateways.management.list(resource, appliedFilter).then(page => { if (!cancelled) { setItems(page.items); setTotalCount(page.totalCount); setListState(page.items.length ? 'ready' : 'no-data') } }).catch(error => { if (!cancelled) setListState(statusOf(error)) })
    return () => { cancelled = true }
  }, [appliedFilter, gateways.management, refreshNonce, resource])

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

  const reload = () => { setAppliedFilter(current => ({ ...current, page: 1 })); setRefreshNonce(value => value + 1) }
  const formDirty = editorMode !== null && configurationFormDirty(form, initialForm)
  const columns = columnsFor(resource)
  const effectiveSort = effectiveConfigurationSort(resource, sort)
  const sortedItems = sortManagementItems(items, effectiveSort.key, effectiveSort.direction)
  const filterActive = Boolean(appliedFilter.search || appliedFilter.status || appliedFilter.siteId || appliedFilter.areaId)
  const emptyMessage = filterActive
    ? `Không có ${resourceLabel(resource).toLocaleLowerCase('vi')} nào khớp bộ lọc hiện tại trong phạm vi được cấp quyền.`
    : `Chưa có ${resourceLabel(resource).toLocaleLowerCase('vi')} nào trong phạm vi hiện tại.`

  function openEditor(mode: 'create' | 'edit', next: Record<string, string>, record: ManagementItem | null) {
    setForm(next); setInitialForm(next); setEditorMode(mode); setEditingRecord(record); setInvalidField(null); setSubmitAttempt(0); setFeedback(null); setMutationRetry(null); mutationRetryRun.current = null
  }

  function performTransition(transition: ConfigurationTransition) {
    setDiscardChangesOpen(false); setPendingTransition(null)
    if (transition.kind === 'tab') {
      const next = transition.resource
      setResource(next); setAppliedFilter(emptyFilter); setDraftFilter(emptyFilter)
      setDetailRecord(null); setDetailState(null); setEditingRecord(null); setEditorMode(null)
      setForm({}); setInitialForm({}); setInvalidField(null); setSubmitAttempt(0)
      setReview(null); setFeedback(null); setMutationRetry(null); mutationRetryRun.current = null
      setPendingAction(null); setDuplicateSourceId('')
      setSort(effectiveConfigurationSort(next, { key: '', direction: 'ascending' }))
      return
    }
    if (transition.kind === 'create') { openEditor('create', defaultForm(resource, appliedFilter.siteId), null); return }
    if (transition.kind === 'edit') { openEditor('edit', formFromItem(resource, transition.item), transition.item); setReview(resource === 'simulator-configurations' ? reviewFromItem(transition.item) : null); return }
    setEditorMode(null); setEditingRecord(null); setForm({}); setInitialForm({}); setInvalidField(null); setSubmitAttempt(0); setMutationRetry(null); mutationRetryRun.current = null
  }

  function requestConfigurationTransition(transition: ConfigurationTransition) {
    if (!editorMode || !configurationFormDirty(form, initialForm)) { performTransition(transition); return }
    setPendingTransition(transition); setDiscardChangesOpen(true)
  }

  function closeEditor() { requestConfigurationTransition({ kind: 'close-editor' }) }

  async function refreshDetail(identity: string) {
    const token = ++detailRequestToken.current
    setDetailState('loading')
    try {
      const loaded = await gateways.management.detail(resource, identity)
      if (token !== detailRequestToken.current) return
      if (loaded) { setDetailRecord(loaded as ManagementItem); setDetailState('ready'); setReview(resource === 'simulator-configurations' ? reviewFromItem(loaded as ManagementItem) : null) } else { setDetailState('not-found') }
    } catch (error) {
      if (token !== detailRequestToken.current) return
      const mapped = statusOf(error)
      setDetailState(mapped === 'forbidden' ? 'forbidden' : mapped === 'expired' ? 'expired' : 'error')
    }
  }

  async function openDetail(item: ManagementItem) {
    const id = idOf(item); if (!id) return
    const token = ++detailRequestToken.current
    setDetailState('loading')
    try {
      const loaded = await gateways.management.detail(resource, id)
      if (token !== detailRequestToken.current) return
      if (!loaded) { setDetailState('not-found'); return }
      setDetailRecord(loaded as ManagementItem); setDetailState('ready')
      setReview(resource === 'simulator-configurations' ? reviewFromItem(loaded as ManagementItem) : null)
    } catch (error) {
      if (token !== detailRequestToken.current) return
      const mapped = statusOf(error)
      setDetailState(mapped === 'forbidden' ? 'forbidden' : mapped === 'expired' ? 'expired' : 'error')
    }
  }

  async function performMutation(attempt: { kind: ConfigurationMutationIntent['kind']; identity: string; payload: string; label: string; run: (retryKey: string) => Promise<ManagementMutation> }): Promise<ManagementMutation | null> {
    const intent: ConfigurationMutationIntent = { resource, kind: attempt.kind, identity: attempt.identity, payload: attempt.payload }
    const retryKey = mutationRetry && sameManagementMutationIntent(mutationRetry.intent, intent) ? mutationRetry.retryKey : crypto.randomUUID()
    setBusyItem(attempt.identity)
    const result = await attempt.run(retryKey)
    setBusyItem(null)
    if (!result.ok) {
      if (isRetryableManagementMutationResult(result)) {
        setMutationRetry({ intent, retryKey })
        mutationRetryRun.current = () => { void performMutation(attempt) }
      } else {
        setMutationRetry(null); mutationRetryRun.current = null
      }
      setFeedback({ tone: 'error', message: messageFor(result, attempt.label) })
      return null
    }
    setMutationRetry(null); mutationRetryRun.current = null
    return result
  }

  async function submitEditor() {
    if (!editorMode) return
    const normalized = normalizeConfigurationForm(resource, editorMode, form)
    setSubmitAttempt(value => value + 1)
    if (normalized.errors.length) { setInvalidField(normalized.errors[0].key); setFeedback({ tone: 'warning', message: normalized.errors[0].message }); return }
    const kind = editorMode === 'create' ? 'create' : 'update'
    const identity = editorMode === 'edit' ? (editingRecord ? idOf(editingRecord) : '') : ''
    if (editorMode === 'edit' && !identity) { setFeedback({ tone: 'error', message: 'Không tìm thấy thực thể để cập nhật; hãy tải lại danh sách.' }); return }
    const payload = JSON.stringify(normalized.body)
    const label = editorMode === 'create' ? 'Tạo mới' : 'Cập nhật'
    const result = await performMutation({
      kind, identity, payload, label,
      run: retryKey => editorMode === 'create'
        ? gateways.management.create(resource, normalized.body, retryKey)
        : gateways.management.update(resource, identity, Number(editingRecord?.version ?? 0), normalized.body, retryKey),
    })
    if (!result) return
    setEditorMode(null); setEditingRecord(null); setForm({}); setInitialForm({}); setInvalidField(null)
    setFeedback({ tone: 'success', message: `${editorMode === 'create' ? 'Đã tạo' : 'Đã cập nhật'} ${resourceLabel(resource)} thành công.` })
    reload()
  }

  async function duplicate(item: ManagementItem) {
    const id = idOf(item); if (!id) return
    if (resource === 'simulator-configurations' && (!duplicateSourceId || duplicateSourceId === textValue(item.sourceId))) { setFeedback({ tone: 'warning', message: 'Hãy chọn một Nguồn dữ liệu đích khác với nguồn hiện tại.' }); return }
    const target = resource === 'simulator-configurations' ? duplicateSourceId : undefined
    const result = await performMutation({ kind: 'duplicate', identity: id, payload: JSON.stringify({ targetSourceId: target ?? null }), label: 'Nhân bản', run: retryKey => gateways.management.duplicate(resource, id, target, retryKey) })
    if (!result) return
    const newId = duplicateIdentityFromResult(result)
    setFeedback({ tone: 'success', message: newId ? `Đã tạo bản nháp ${resourceLabel(resource)} mới (${newId}).` : `Đã tạo bản nháp ${resourceLabel(resource)} mới.` })
    reload()
    if (newId) void refreshDetail(newId)
  }

  async function reviewRelationships(target: ReviewState | null) {
    if (!target) return
    const result = await performMutation({ kind: 'review', identity: target.id, payload: JSON.stringify({ draftVersion: target.draftVersion }), label: 'Xem xét quan hệ', run: retryKey => gateways.management.reviewSimulatorConfiguration(target.id, target.draftVersion, retryKey) })
    if (!result) return
    setFeedback({ tone: 'success', message: 'Đã lưu biên nhận xem xét quan hệ trên máy chủ.' })
    reload()
    void refreshDetail(target.id)
  }

  async function validate(item: ManagementItem) {
    const id = idOf(item); if (!id) return
    if (resource === 'simulator-configurations' && !reviewFromItem(item)?.reviewed) { setFeedback({ tone: 'warning', message: 'Cần xem xét quan hệ trên máy chủ trước khi kiểm tra bản nháp.' }); return }
    const result = await performMutation({ kind: 'validate', identity: id, payload: '{}', label: 'Kiểm tra', run: retryKey => gateways.management.validate(resource, id, retryKey) })
    if (!result) return
    setFeedback({ tone: 'success', message: `Kiểm tra ${resourceLabel(resource)} thành công.` })
    reload()
    if (resource === 'simulator-configurations') void refreshDetail(id)
  }

  function lifecycle(item: ManagementItem, action: string) {
    if (action === 'activate' && resource === 'simulator-configurations') {
      const readiness = simulatorActivationReadiness(item)
      if (!readiness.ready) { setFeedback({ tone: 'warning', message: readiness.reason ?? 'Cần xem xét quan hệ và kiểm tra bản nháp trước khi kích hoạt.' }); return }
    }
    setPendingAction({ kind: 'lifecycle', item, action })
  }

  function remove(item: ManagementItem) { setPendingAction({ kind: 'remove', item }) }

  async function confirmPendingAction() {
    const pending = pendingAction; if (!pending) return
    setPendingAction(null)
    const id = idOf(pending.item)
    const action = pending.kind === 'remove' ? '' : pending.action ?? ''
    const result = await performMutation({
      kind: pending.kind === 'remove' ? 'remove' : 'lifecycle',
      identity: id,
      payload: JSON.stringify({ action: pending.kind === 'remove' ? null : action, expectedVersion: Number(pending.item.version ?? 0) }),
      label: pending.kind === 'remove' ? 'Xóa' : 'Chuyển trạng thái',
      run: retryKey => pending.kind === 'remove'
        ? gateways.management.remove(resource, id, Number(pending.item.version ?? 0), retryKey)
        : gateways.management.lifecycle(resource, id, action, Number(pending.item.version ?? 0), retryKey),
    })
    if (!result) return
    setFeedback({ tone: 'success', message: pending.kind === 'remove' ? `Đã xóa bản nháp ${resourceLabel(resource).toLocaleLowerCase('vi')}.` : `Đã ${pending.action} ${resourceLabel(resource).toLocaleLowerCase('vi')}.` })
    reload()
  }

  async function activate(item: ManagementItem) {
    const id = textValue(item.configurationId); if (!id) return
    const readiness = simulatorActivationReadiness(item)
    if (!readiness.ready) { setFeedback({ tone: 'warning', message: readiness.reason ?? 'Cần xem xét quan hệ và kiểm tra bản nháp trước khi kích hoạt.' }); return }
    const draft = Number(item.draftConfigurationVersion ?? 0)
    const result = await performMutation({ kind: 'activate', identity: id, payload: JSON.stringify({ expectedHeadVersion: Number(item.version ?? 0), draftConfigurationVersion: draft }), label: 'Kích hoạt', run: retryKey => gateways.management.activateSimulatorConfigurationVersion(id, Number(item.version ?? 0), draft, retryKey) })
    if (!result) return
    setFeedback({ tone: 'success', message: 'Đã kích hoạt bản cấu hình mô phỏng.' })
    reload()
  }

  const optionLists = options
  const createOptionNames: OptionName[] = resource === 'areas' || resource === 'data-sources' ? ['sites'] : resource === 'assets' ? ['areas'] : resource === 'points' ? ['assets'] : resource === 'source-point-mappings' ? ['sources', 'points'] : resource === 'simulator-configurations' ? ['sources'] : []
  const unavailable = createOptionNames.find(name => optionStates[name] !== 'ready')
  const errors: FieldError[] = editorMode ? normalizeConfigurationForm(resource, editorMode, form).errors.map(error => ({ id: `configuration-field-${error.key}`, label: error.key, message: error.message })) : []

  return <section className="page configuration-page" aria-labelledby="configuration-management-title">
    <div className="page-heading"><div><p className="eyebrow">Quản lý cấu hình</p><h1 id="configuration-management-title">Cấu hình vận hành</h1><p className="lede">Quản lý bảy nhóm cấu hình trong phạm vi được cấp quyền, với trạng thái, phiên bản và hành động có thể truy xuất.</p></div><div className="actions-stack"><span className="badge badge-neutral">{resourceLabel(resource)}</span><ManagementActionButton label="Tạo mới" tone="primary" onClick={() => requestConfigurationTransition({ kind: 'create' })} disabled={Boolean(unavailable)} title={unavailable ? optionMessage(unavailable, optionStates[unavailable]) : undefined} /></div></div>
    <nav className="tabs entity-tabs" aria-label="Loại cấu hình">{RESOURCE_KEYS.map(value => <button key={value} type="button" className={`tab ${resource === value ? 'tab-active' : ''}`} aria-current={resource === value ? 'page' : undefined} onClick={() => requestConfigurationTransition({ kind: 'tab', resource: value })}>{resourceLabel(value)}</button>)}</nav>
    <FilterBar fields={[{ id: 'search', label: 'Tìm kiếm', value: draftFilter.search ?? '', placeholder: 'Mã, tên hoặc định danh…', type: 'search' }]} onChange={(id, value) => { if (id === 'search') setDraftFilter(current => ({ ...current, search: value || undefined })) }} onSubmit={(event: FormEvent<HTMLFormElement>) => { event.preventDefault(); setAppliedFilter(current => ({ ...current, ...draftFilter, page: 1 })) }} onReset={() => { setDraftFilter(current => ({ ...current, search: undefined, status: undefined, siteId: undefined, areaId: undefined })); setAppliedFilter(current => ({ ...current, search: undefined, status: undefined, siteId: undefined, areaId: undefined, page: 1 })) }} resultCount={totalCount}>
      <label className="field compact-filter"><span>Trạng thái</span><select className="input" value={draftFilter.status ?? ''} onChange={event => setDraftFilter(current => ({ ...current, status: event.target.value || undefined }))}><option value="">Tất cả</option>{statusesForResource(resource).map(value => <option key={value}>{value}</option>)}</select></label>
      <label className="field compact-filter"><span>Địa điểm</span><select className="input" value={draftFilter.siteId ?? ''} onChange={event => setDraftFilter(current => ({ ...current, siteId: event.target.value || undefined }))}><option value="">Tất cả</option>{optionLists.sites.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select></label>
      <label className="field compact-filter"><span>Khu vực</span><select className="input" value={draftFilter.areaId ?? ''} onChange={event => setDraftFilter(current => ({ ...current, areaId: event.target.value || undefined }))}><option value="">Tất cả</option>{optionLists.areas.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select></label>
    </FilterBar>
    {resource === 'simulator-configurations' && <label className="field inline-control"><span>Nguồn đích nhân bản</span><select className="input" value={duplicateSourceId} disabled={optionStates.sources !== 'ready'} onChange={event => setDuplicateSourceId(event.target.value)}><option value="">Chọn nguồn đích</option>{optionLists.sources.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select><small className="muted">Bản nháp phải gắn với một Source được cấp quyền; không tự chọn phần tử đầu tiên.</small></label>}
    {feedback && <FeedbackBanner tone={feedback.tone === 'error' ? 'danger' : feedback.tone} message={feedback.message} action={mutationRetry ? <ManagementActionButton label="Thử lại cùng yêu cầu" tone="quiet" onClick={() => mutationRetryRun.current?.()} /> : undefined} />}
    {review && <RelationshipReview review={review} onReviewed={() => void reviewRelationships(review)} />}
    {editorMode && <UnsavedChangesGuard when={formDirty}><EditorPanel resource={resource} mode={editorMode} form={form} onFieldChange={(key, value) => { setForm(current => ({ ...current, [key]: value })); if (invalidField === key) setInvalidField(null) }} invalidField={invalidField} errors={errors} activationKey={submitAttempt} optionLists={optionLists} optionStates={optionStates} busy={busyItem !== null} onSave={() => void submitEditor()} onCancel={closeEditor} onRetry={() => setOptionRetryNonce(value => value + 1)} /></UnsavedChangesGuard>}
    {detailState !== null && <Drawer open title={`Chi tiết ${resourceLabel(resource)}`} onClose={() => { setDetailRecord(null); setDetailState(null) }}>{detailState === 'loading' ? <LoadingState message={`Đang tải chi tiết ${resourceLabel(resource)}…`} /> : detailState === 'not-found' ? <EmptyState title="Không tìm thấy" message="Thực thể không còn trong phạm vi được cấp quyền." /> : detailState === 'forbidden' ? <ForbiddenState message={`Bạn không có quyền xem chi tiết ${resourceLabel(resource)} trong phạm vi này.`} /> : detailState === 'expired' ? <ErrorState message="Phiên đã hết hạn. Vui lòng đăng nhập lại để tiếp tục." action={onSessionRecovery ? <ManagementActionButton label="Đăng nhập lại" tone="primary" onClick={onSessionRecovery} /> : undefined} /> : detailState === 'error' ? <ErrorState message={`Không thể tải chi tiết ${resourceLabel(resource)}.`} /> : detailRecord ? <DetailPanel title={`Chi tiết ${resourceLabel(resource)}`} description="Thông tin được giới hạn theo danh sách trường được phép trong phạm vi quyền hiện tại." action={<ManagementActionButton label="Đóng" onClick={() => { setDetailRecord(null); setDetailState(null) }} tone="quiet" />}><dl className="detail-grid">{detailFieldsFor(resource).map(field => <div key={field.key}><dt>{field.label}</dt><dd>{field.key === 'effectiveFrom' || field.key === 'effectiveTo' ? safeConfigurationDate(detailRecord[field.key]) : textValue(detailRecord[field.key]) || '—'}</dd></div>)}</dl></DetailPanel> : <EmptyState title="Không tìm thấy" message="Thực thể không còn trong phạm vi được cấp quyền." />}</Drawer>}
    <div className="table-toolbar"><div><p className="eyebrow">Danh sách hiện tại</p><p className="muted">Bảng gọn cho thao tác dài hạn; sắp xếp chỉ áp dụng trên trang đã tải.</p></div><label className="field compact-filter"><span>Sắp xếp trang hiện tại</span><select className="input" value={`${effectiveSort.key}:${effectiveSort.direction}`} onChange={event => { const [key, direction] = event.target.value.split(':') as [string, SortDirection]; setSort(effectiveConfigurationSort(resource, { key, direction })) }}>{columns.map(column => <option key={`${column.key}:ascending`} value={`${column.key}:ascending`}>{column.label} ↑</option>)}{columns.map(column => <option key={`${column.key}:descending`} value={`${column.key}:descending`}>{column.label} ↓</option>)}</select></label></div>
    <ConfigurationTable state={listState} resource={resource} columns={columns} items={sortedItems} totalCount={totalCount} filter={appliedFilter} busyItem={busyItem} onPageChange={page => setAppliedFilter(current => ({ ...current, page }))} onDetail={item => void openDetail(item)} onEdit={item => requestConfigurationTransition({ kind: 'edit', item })} onDuplicate={duplicate} onValidate={validate} onLifecycle={lifecycle} onRemove={remove} onActivate={activate} onSessionRecovery={onSessionRecovery} emptyMessage={emptyMessage} />
    <ConfirmDialog open={Boolean(pendingAction)} title="Xác nhận thay đổi trạng thái" description={pendingAction?.kind === 'remove' ? 'Chỉ bản nháp an toàn mới được xóa. Không có lý do được thu thập vì hợp đồng hiện tại không lưu trường reason.' : `Thao tác ${actionLabelFor(pendingAction?.action ?? '')} sẽ được gửi với phiên bản hiện tại và có thể bị từ chối nếu dữ liệu đã thay đổi.`} onCancel={() => setPendingAction(null)} onConfirm={() => void confirmPendingAction()} confirmLabel="Xác nhận" />
    <ConfirmDialog open={discardChangesOpen} title="Bỏ thay đổi chưa lưu?" description="Các trường đã sửa sẽ bị bỏ. Bạn có thể tiếp tục chỉnh sửa hoặc hủy bỏ thay đổi." onCancel={() => setDiscardChangesOpen(false)} onConfirm={() => { if (pendingTransition) performTransition(pendingTransition) }} confirmLabel="Bỏ thay đổi" />
  </section>
}

function ConfigurationTable({ state, resource, columns, items, totalCount, filter, busyItem, onPageChange, onDetail, onEdit, onDuplicate, onValidate, onLifecycle, onRemove, onActivate, onSessionRecovery, emptyMessage }: { state: ManagementState; resource: string; columns: ManagementColumn[]; items: ManagementItem[]; totalCount: number; filter: ManagementFilter; busyItem: string | null; onPageChange: (page: number) => void; onDetail: (item: ManagementItem) => void; onEdit: (item: ManagementItem) => void; onDuplicate: (item: ManagementItem) => void; onValidate: (item: ManagementItem) => void; onLifecycle: (item: ManagementItem, action: string) => void; onRemove: (item: ManagementItem) => void; onActivate: (item: ManagementItem) => void; onSessionRecovery?: () => void; emptyMessage: string }) {
  const stateMessage = managementStateMessage(state, resource, emptyMessage)
  if (stateMessage?.tone === 'loading') return <LoadingState message={stateMessage.message} />
  if (stateMessage?.tone === 'empty') return <EmptyState title={stateMessage.title} message={stateMessage.message} />
  if (stateMessage?.tone === 'forbidden') return <ForbiddenState message={stateMessage.message} />
  if (stateMessage?.tone === 'conflict') return <ConflictState message={stateMessage.message} />
  if (stateMessage?.tone === 'blocked') return <BlockedState message={stateMessage.message} />
  if (stateMessage?.tone === 'error' && state === 'expired') return <ErrorState message={stateMessage.message} action={onSessionRecovery ? <ManagementActionButton label="Đăng nhập lại" tone="primary" onClick={onSessionRecovery} /> : undefined} />
  if (stateMessage) return <ErrorState message={stateMessage.message} />
  const tableColumns: DataTableColumn<ManagementItem>[] = columns.map(column => ({ key: column.key, header: column.label, render: item => column.key === 'status' ? statusBadge(textValue(item.status)) : column.render ? column.render(item) : textValue(item[column.key]) }))
  return <><DataTable caption={`${resourceLabel(resource)} · sắp xếp trang hiện tại`} columns={tableColumns} rows={items} rowKey={(item, index) => idOf(item) || String(index)} rowAction={item => <div className="actions-stack"><ManagementActionButton label="Chi tiết" onClick={() => onDetail(item)} /><ManagementActionButton label="Sửa" onClick={() => onEdit(item)} disabled={busyItem !== null} /><ManagementActionButton label="Kiểm tra" onClick={() => onValidate(item)} disabled={busyItem === idOf(item)} /><DuplicateButton item={item} busyItem={busyItem} onDuplicate={onDuplicate} />{lifecycleActionsFor(resource, textValue(item.status)).map(action => <ManagementActionButton key={action} label={actionLabelFor(action)} onClick={() => onLifecycle(item, action)} disabled={busyItem === idOf(item)} tone={action === 'decommission' ? 'danger' : 'secondary'} />)}{resource === 'simulator-configurations' && <ActivateVersionButton item={item} busyItem={busyItem} onActivate={onActivate} readyForActivation={simulatorActivationReadiness(item).ready} />}{canDeleteResource(resource, textValue(item.status)) && <ManagementActionButton label="Xóa bản nháp" tone="danger" onClick={() => onRemove(item)} disabled={busyItem === idOf(item)} />}</div>} /><Pagination page={filter.page} pageSize={filter.pageSize} total={totalCount} onPageChange={onPageChange} /></>
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
