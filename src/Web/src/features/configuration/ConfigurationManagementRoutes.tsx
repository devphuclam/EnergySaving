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
type OptionState = 'loading' | 'ready' | 'empty' | 'forbidden' | 'dependency' | 'runtime'
type OptionName = 'sites' | 'areas' | 'assets' | 'sources' | 'points'
type SelectName = 'site' | 'area' | 'asset' | 'source' | 'point'

function optionNameForSelect(select: SelectName): OptionName {
  return select === 'site' ? 'sites' : select === 'area' ? 'areas' : select === 'asset' ? 'assets' : select === 'source' ? 'sources' : 'points'
}
type ReviewState = {
  id: string
  sourceId: string
  sourceLabel: string
  draftVersion: number
  relationships: string[]
  excluded: string[]
  reviewed: boolean
  validated: boolean
  relationshipStale: boolean
  validationStale: boolean
}

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
  const label = name === 'sites' ? 'Địa điểm' : name === 'areas' ? 'Khu vực' : name === 'assets' ? 'Tài sản' : name === 'sources' ? 'Nguồn dữ liệu' : 'Điểm đo'
  if (state === 'forbidden') return `${label} không nằm trong phạm vi được cấp quyền.`
  if (state === 'dependency') return `Dịch vụ cung cấp ${label.toLocaleLowerCase('vi')} đang không sẵn sàng.`
  if (state === 'runtime') return `Không thể tải ${label.toLocaleLowerCase('vi')} do lỗi kết nối.`
  if (state === 'empty') return `Không có ${label.toLocaleLowerCase('vi')} hợp lệ trong phạm vi hiện tại.`
  return `Đang tải ${label.toLocaleLowerCase('vi')}…`
}

function idOf(item: ManagementItem): string {
  return textValue(item.id ?? item.configurationId)
}

function reviewFromItem(item: ManagementItem): ReviewState | null {
  const id = textValue(item.configurationId ?? item.id)
  const draftVersion = Number(item.draftConfigurationVersion ?? 0)
  if (!id || draftVersion <= Number(item.currentConfigurationVersion ?? 0)) return null
  const sourceId = textValue(item.sourceId)
  const sourceCode = textValue(item.sourceCode)
  const sourceName = textValue(item.sourceName)
  const sourceLabel = [sourceCode, sourceName].filter(Boolean).join(' – ') || sourceId
  const listValue = (value: unknown, fallback: string[]) => Array.isArray(value) ? value.map(String) : fallback
  return {
    id,
    sourceId,
    sourceLabel,
    draftVersion,
    relationships: listValue(item.reviewRelationships, ['Data Source']),
    excluded: listValue(item.excludedFields, []),
    reviewed: Boolean(item.relationshipReviewed) && !item.relationshipReceiptStale,
    validated: Boolean(item.validationRecorded) && !item.validationReceiptStale,
    relationshipStale: Boolean(item.relationshipReceiptStale),
    validationStale: Boolean(item.validationReceiptStale),
  }
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

function firstInvalidField(resource: string, mode: 'create' | 'edit', body: Record<string, unknown>): { key: string; message: string } | null {
  const missing = (key: string, message: string) => !String(body[key] ?? '').trim() ? { key, message } : null
  if (resource === 'sites' || resource === 'areas' || resource === 'assets' || resource === 'points' || resource === 'data-sources') {
    const name = missing('name', 'Tên là bắt buộc.')
    if (name) return name
  }
  if (mode === 'create' && (resource === 'areas' || resource === 'data-sources')) {
    const site = missing('siteId', 'Vui lòng chọn Địa điểm cha.')
    if (site) return site
  }
  if (mode === 'create' && resource === 'assets') {
    const area = missing('areaId', 'Vui lòng chọn Khu vực cha.')
    if (area) return area
  }
  if (mode === 'create' && resource === 'points') {
    const asset = missing('assetId', 'Vui lòng chọn Tài sản cha.')
    if (asset) return asset
  }
  if (mode === 'create' && resource === 'source-point-mappings') {
    const source = missing('sourceId', 'Vui lòng chọn Nguồn dữ liệu.')
    if (source) return source
    const point = missing('pointId', 'Vui lòng chọn Điểm đo.')
    if (point) return point
  }
  if (mode === 'create' && resource === 'simulator-configurations') {
    const source = missing('sourceId', 'Vui lòng chọn Nguồn dữ liệu.')
    if (source) return source
  }
  return null
}

export function ConfigurationManagementRoutes() {
  const gateways = useWebGateways()
  const [resource, setResource] = useState<string>('sites')
  const [filter, setFilter] = useState<ManagementFilter>(emptyFilter)
  const [state, setState] = useState<ManagementState>('loading')
  const [items, setItems] = useState<ManagementItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [sites, setSites] = useState<Array<{ id: string; label: string }>>([])
  const [areas, setAreas] = useState<Array<{ id: string; label: string }>>([])
  const [assets, setAssets] = useState<Array<{ id: string; label: string }>>([])
  const [sources, setSources] = useState<Array<{ id: string; label: string }>>([])
  const [points, setPoints] = useState<Array<{ id: string; label: string }>>([])
  const [optionStates, setOptionStates] = useState<Record<OptionName, OptionState>>({ sites: 'loading', areas: 'loading', assets: 'loading', sources: 'loading', points: 'loading' })
  const [optionRetryNonce, setOptionRetryNonce] = useState(0)
  const [busyItem, setBusyItem] = useState<string | null>(null)
  const [feedback, setFeedback] = useState<ManagementFeedback>(null)
  const [refreshNonce, setRefreshNonce] = useState(0)
  const [selected, setSelected] = useState<ManagementItem | null>(null)
  const [detail, setDetail] = useState<ManagementItem | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [editor, setEditor] = useState<'create' | 'edit' | null>(null)
  const [form, setForm] = useState<Record<string, string>>({})
  const [review, setReview] = useState<ReviewState | null>(null)
  const [duplicateSourceId, setDuplicateSourceId] = useState('')
  const [invalidField, setInvalidField] = useState<string | null>(null)
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
    setOptionStates(current => ({ ...current, sites: 'loading' }))
    void gateways.management.list('sites', { page: 1, pageSize: 200 })
      .then(page => {
        if (cancelled) return
        const next = page.items.map(value => ({ id: textValue(value.id), label: `${textValue(value.code)} – ${textValue(value.name)}` })).filter(value => value.id)
        setSites(next)
        setOptionStates(current => ({ ...current, sites: next.length ? 'ready' : 'empty' }))
      })
      .catch(error => { if (!cancelled) { setSites([]); setOptionStates(current => ({ ...current, sites: optionStateOf(error) })) } })
    return () => { cancelled = true }
  }, [gateways.management, refreshNonce, optionRetryNonce])

  useEffect(() => {
    const needsAreas = resource === 'assets'
    const needsAssets = resource === 'points'
    const needsSources = resource === 'simulator-configurations' || resource === 'source-point-mappings'
    const needsPoints = resource === 'source-point-mappings'
    if (!needsAreas && !needsAssets && !needsSources && !needsPoints) return
    let cancelled = false
    if (needsAreas) setOptionStates(current => ({ ...current, areas: 'loading' }))
    if (needsAssets) setOptionStates(current => ({ ...current, assets: 'loading' }))
    if (needsSources) setOptionStates(current => ({ ...current, sources: 'loading' }))
    if (needsPoints) setOptionStates(current => ({ ...current, points: 'loading' }))
    const requests: Array<Promise<{ items: Array<Record<string, unknown>> }>> = []
    if (needsAreas) requests.push(gateways.management.list('areas', { page: 1, pageSize: 200 }))
    if (needsAssets) requests.push(gateways.management.list('assets', { page: 1, pageSize: 200 }))
    if (needsSources) requests.push(gateways.management.list('data-sources', { page: 1, pageSize: 200, status: 'Active' }))
    if (needsPoints) requests.push(gateways.management.list('points', { page: 1, pageSize: 200 }))
    void Promise.allSettled(requests).then(results => {
      if (cancelled) return
      let resultIndex = 0
      if (needsAreas) {
        const result = results[resultIndex++]
        if (result?.status === 'fulfilled') {
          const next = result.value.items.map(value => ({ id: textValue(value.id), label: `${textValue(value.code)} – ${textValue(value.name)}` })).filter(value => value.id)
          setAreas(next)
          setOptionStates(current => ({ ...current, areas: next.length ? 'ready' : 'empty' }))
        } else { setAreas([]); setOptionStates(current => ({ ...current, areas: optionStateOf(result?.reason) })) }
      }
      if (needsAssets) {
        const result = results[resultIndex++]
        if (result?.status === 'fulfilled') {
          const next = result.value.items.map(value => ({ id: textValue(value.id), label: `${textValue(value.code)} – ${textValue(value.name)}` })).filter(value => value.id)
          setAssets(next)
          setOptionStates(current => ({ ...current, assets: next.length ? 'ready' : 'empty' }))
        } else { setAssets([]); setOptionStates(current => ({ ...current, assets: optionStateOf(result?.reason) })) }
      }
      if (needsSources) {
        const result = results[resultIndex++]
        if (result?.status === 'fulfilled') {
          const next = result.value.items.map(value => ({ id: textValue(value.id), label: `${textValue(value.code)} – ${textValue(value.name)}` })).filter(value => value.id)
          setSources(next)
          setOptionStates(current => ({ ...current, sources: next.length ? 'ready' : 'empty' }))
        } else { setSources([]); setOptionStates(current => ({ ...current, sources: optionStateOf(result?.reason) })) }
      }
      if (needsPoints) {
        const result = results[resultIndex]
        if (result?.status === 'fulfilled') {
          const next = result.value.items.map(value => ({ id: textValue(value.id), label: `${textValue(value.code)} – ${textValue(value.name)}` })).filter(value => value.id)
          setPoints(next)
          setOptionStates(current => ({ ...current, points: next.length ? 'ready' : 'empty' }))
        } else { setPoints([]); setOptionStates(current => ({ ...current, points: optionStateOf(result?.reason) })) }
      }
    })
    return () => { cancelled = true }
  }, [editor, resource, gateways.management, refreshNonce, optionRetryNonce])

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
      setReview(resource === 'simulator-configurations' ? reviewFromItem(loaded as ManagementItem) : null)
    } catch (error) { setState(statusOf(error)) }
    finally { setDetailLoading(false) }
  }

  function beginCreate() {
    setSelected(null)
    setDetail(null)
    setEditor('create')
    setForm(defaultForm(resource, filter.siteId))
    setInvalidField(null)
    setFeedback(null)
  }

  function beginEdit(item: ManagementItem) {
    setSelected(item)
    setDetail(item)
    setEditor('edit')
    setForm(formFromItem(resource, item))
    setInvalidField(null)
    setReview(resource === 'simulator-configurations' ? reviewFromItem(item) : null)
    setFeedback(null)
  }

  async function submitEditor() {
    const body = normalizedForm(resource, form)
    const invalid = editor ? firstInvalidField(resource, editor, body) : null
    if (invalid) {
      setState('validation')
      setInvalidField(invalid.key)
      setFeedback({ tone: 'warning', message: invalid.message })
      window.setTimeout(() => document.getElementById(`configuration-field-${invalid.key}`)?.focus(), 0)
      return
    }
    setInvalidField(null)
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
    if (resource === 'simulator-configurations' && !duplicateSourceId) {
      setState('validation')
      setFeedback({ tone: 'warning', message: 'Hãy chọn Nguồn đích nhân bản trước khi tạo bản nháp Simulator.' })
      return
    }
    if (resource === 'simulator-configurations' &&
      duplicateSourceId === textValue(item.sourceId)) {
      setState('validation')
      setFeedback({ tone: 'warning', message: 'Nguồn đích phải khác Source hiện tại của cấu hình.' })
      return
    }
    setBusyItem(id)
    let result
    try {
      result = await gateways.management.duplicate(resource, id,
        resource === 'simulator-configurations' ? duplicateSourceId : undefined)
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
    if (resource === 'simulator-configurations') {
      setReview({ id: newId, sourceId: textValue(result.body?.sourceId), sourceLabel: textValue(result.body?.sourceId), draftVersion: Number(result.body?.draftConfigurationVersion ?? 0), relationships, excluded, reviewed: false, validated: false, relationshipStale: false, validationStale: false })
      setFeedback({ tone: 'success', message: `Đã tạo bản nháp ${resourceLabel(resource)} mới (${newId}). Hãy xem xét quan hệ và kiểm tra trước khi kích hoạt.` })
    } else {
      setReview(null)
      setFeedback({ tone: 'success', message: `Đã tạo bản nháp ${resourceLabel(resource)} mới (${newId}).` })
    }
    if (newId) {
      try {
        const loaded = await gateways.management.detail(resource, newId)
        if (loaded) {
          setSelected(loaded as ManagementItem)
          setDetail(loaded as ManagementItem)
          if (resource === 'simulator-configurations') setReview(reviewFromItem(loaded as ManagementItem))
        }
      } catch {
        setState('runtime')
        setFeedback({ tone: 'warning', message: 'Đã nhân bản nhưng chưa tải được chi tiết bản nháp mới.' })
      }
    }
    reload()
  }

  async function reviewRelationships(target: ReviewState | null = review) {
    if (!target) return
    setReview(target)
    setBusyItem(target.id)
    try {
      const result = await gateways.management.reviewSimulatorConfiguration(target.id, target.draftVersion)
      setBusyItem(null)
      if (!result.ok) {
        setFeedback({ tone: 'error', message: messageFor(result, 'Xem xét quan hệ') })
        return
      }
      setFeedback({ tone: 'success', message: 'Đã lưu biên nhận xem xét quan hệ trên máy chủ.' })
      const loaded = await gateways.management.detail('simulator-configurations', target.id)
      if (loaded) {
        setSelected(loaded as ManagementItem)
        setDetail(loaded as ManagementItem)
        setReview(reviewFromItem(loaded as ManagementItem))
      }
      reload()
    } catch {
      setBusyItem(null)
      setState('runtime')
      setFeedback({ tone: 'error', message: 'Không thể lưu biên nhận xem xét quan hệ.' })
    }
  }

  async function validate(item: ManagementItem) {
    const id = idOf(item)
    const itemReview = resource === 'simulator-configurations' ? reviewFromItem(item) : null
    if (resource === 'simulator-configurations' && (!itemReview?.reviewed || itemReview.relationshipStale)) {
      setFeedback({ tone: 'warning', message: 'Cần xem xét quan hệ trên máy chủ trước khi kiểm tra bản nháp.' })
      return
    }
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
    setFeedback({ tone: 'success', message: `Kiểm tra ${resourceLabel(resource)} thành công. Có thể tiếp tục theo trạng thái hợp lệ.` })
    if (resource === 'simulator-configurations') {
      try {
        const loaded = await gateways.management.detail(resource, id)
        if (loaded) {
          setDetail(loaded as ManagementItem)
          setSelected(loaded as ManagementItem)
          setReview(reviewFromItem(loaded as ManagementItem))
        }
        reload()
      } catch {
        setState('runtime')
        setFeedback({ tone: 'warning', message: 'Đã kiểm tra nhưng chưa tải lại được trạng thái receipt từ máy chủ.' })
        reload()
      }
    }
  }

  async function lifecycle(item: ManagementItem, action: string) {
    const id = idOf(item)
    if (!id || !window.confirm(`Xác nhận ${action} ${resourceLabel(resource).toLocaleLowerCase('vi')} này?`)) return
    if (action === 'activate' && resource === 'simulator-configurations' && !hasDraftReady(item)) {
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
    const itemReview = reviewFromItem(item)
    if (!itemReview?.reviewed || !itemReview.validated || itemReview.relationshipStale || itemReview.validationStale) {
      setFeedback({ tone: 'warning', message: 'Cần xem xét quan hệ và kiểm tra bản nháp trước khi kích hoạt.' })
      return
    }
    setBusyItem(id)
    let result
    try {
      result = await gateways.management.activateSimulatorConfigurationVersion(id, headVersion, draftVersion)
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
    setReview(current => current?.id === id ? null : current)
  }

  const columns = columnsFor(resource)
  const hasDraftReady = (item: ManagementItem) => {
    const itemReview = reviewFromItem(item)
    return itemReview?.reviewed === true && itemReview.validated === true &&
      !itemReview.relationshipStale && !itemReview.validationStale
  }

  const createOptionNames: OptionName[] = resource === 'areas' || resource === 'data-sources'
    ? ['sites']
    : resource === 'assets'
      ? ['areas']
      : resource === 'points'
        ? ['assets']
        : resource === 'source-point-mappings'
          ? ['sources', 'points']
          : resource === 'simulator-configurations' ? ['sources'] : []
  const createUnavailable = createOptionNames.some(name => optionStates[name] !== 'ready')
  const createUnavailableMessage = createOptionNames.find(name => optionStates[name] !== 'ready')

  return (
    <section className="page" aria-labelledby="configuration-management-title">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Quản lý cấu hình</p>
          <h1 id="configuration-management-title">Cấu hình vận hành</h1>
          <p className="lede">Tìm kiếm, xem chi tiết, tạo, sửa, nhân bản, kiểm tra và chuyển trạng thái theo phạm vi được ủy quyền.</p>
        </div>
        <div className="actions-stack"><span className="badge badge-neutral">{resourceLabel(resource)}</span><ManagementActionButton label="Tạo mới" tone="primary" onClick={beginCreate} disabled={createUnavailable} title={createUnavailableMessage ? optionMessage(createUnavailableMessage, optionStates[createUnavailableMessage]) : undefined} /></div>
      </div>
      <nav className="tabs" aria-label="Loại cấu hình">
        {RESOURCE_KEYS.map(value => <button key={value} type="button" className={`tab ${resource === value ? 'tab-active' : ''}`} onClick={() => { setResource(value); setFilter(emptyFilter); setSelected(null); setDetail(null); setEditor(null); setReview(null); setDuplicateSourceId(''); setFeedback(null) }}>{resourceLabel(value)}</button>)}
      </nav>
      <ManagementFilterBar search={filter.search} onSearchChange={value => setFilter(current => ({ ...current, search: value, page: 1 }))} statuses={statusesFor(resource)} status={filter.status} onStatusChange={value => setFilter(current => ({ ...current, status: value || undefined, page: 1 }))} siteOptions={sites} siteId={filter.siteId} onSiteChange={value => setFilter(current => ({ ...current, siteId: value || undefined, page: 1 }))} busy={state === 'loading'} />
      {!editor && resource !== 'simulator-configurations' && createUnavailableMessage ? <OptionFailure name={createUnavailableMessage} state={optionStates[createUnavailableMessage]} onRetry={() => setOptionRetryNonce(value => value + 1)} /> : null}
      {resource === 'simulator-configurations' ? <div className="field"><label><span className="field-label">Nguồn đích nhân bản</span><select className="input" value={duplicateSourceId} disabled={optionStates.sources !== 'ready'} onChange={event => setDuplicateSourceId(event.target.value)}><option value="">-- Chọn nguồn đích --</option>{sources.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select></label><small className="muted">Bản nháp mới phải gắn với một Source khác được cấp quyền; không tự chọn phần tử đầu tiên.</small>{optionStates.sources !== 'ready' ? <OptionFailure name="sources" state={optionStates.sources} onRetry={() => setOptionRetryNonce(value => value + 1)} /> : null}</div> : null}
      <FeedbackBanner feedback={feedback} />
      {review ? <RelationshipReview review={review} onReviewed={() => void reviewRelationships()} /> : null}
      {editor ? <EditorPanel resource={resource} mode={editor} form={form} onFieldChange={(key, value) => { setForm(current => ({ ...current, [key]: value })); if (invalidField === key) setInvalidField(null) }} invalidField={invalidField} siteOptions={sites} areaOptions={areas} assetOptions={assets} sourceOptions={sources} pointOptions={points} optionStates={optionStates} onRetry={() => setOptionRetryNonce(value => value + 1)} busy={busyItem !== null} onSave={submitEditor} onCancel={() => setEditor(null)} /> : null}
      {detailLoading ? <p className="notice notice-info" role="status">Đang tải chi tiết…</p> : null}
      {detail ? <DetailPanel resource={resource} detail={detail} onClose={() => { setDetail(null); setSelected(null) }} supportedActions={supportedActions(resource, detail)} review={resource === 'simulator-configurations' ? reviewFromItem(detail) : null} onReview={target => void reviewRelationships(target)} /> : null}
      <div className="card form-card">
        <ManagementTable resource={resource} state={state} columns={columns} items={items} emptyMessage={`Chưa có ${resourceLabel(resource).toLocaleLowerCase('vi')} nào trong phạm vi hiện tại.`} renderActions={item => {
          const id = idOf(item)
          const actions = lifecycleActions(resource, textValue(item.status))
          return <span className="actions-stack">
             <ManagementActionButton label="Chi tiết" onClick={() => void openDetail(item)} />
            {resource === 'simulator-configurations' && reviewFromItem(item) && !reviewFromItem(item)?.reviewed ? <ManagementActionButton label="Xem xét quan hệ" onClick={() => void reviewRelationships(reviewFromItem(item))} disabled={busyItem === id} tone="primary" /> : null}
            <ManagementActionButton label="Sửa" onClick={() => beginEdit(item)} disabled={busyItem !== null || (resource === 'source-point-mappings' && textValue(item.status) === 'Active') || (resource === 'points' && textValue(item.status) === 'Active') || (resource === 'simulator-configurations' && Number(item.draftConfigurationVersion ?? 0) > Number(item.currentConfigurationVersion ?? 0))} title={resource === 'source-point-mappings' && textValue(item.status) === 'Active' ? 'Ánh xạ đang Active là bất biến; hãy tạo bản nháp thay thế.' : resource === 'points' && textValue(item.status) === 'Active' ? 'Điểm đo Active cần quy trình điều phối; chỉnh sửa hành vi đang bị tắt.' : resource === 'simulator-configurations' && Number(item.draftConfigurationVersion ?? 0) > Number(item.currentConfigurationVersion ?? 0) ? 'Cấu hình đã có bản nháp; hãy xem xét và kích hoạt bản nháp hiện tại trước khi sửa tiếp.' : undefined} />
            {(resource !== 'simulator-configurations' || Number(item.draftConfigurationVersion ?? 0) > Number(item.currentConfigurationVersion ?? 0)) ? <ManagementActionButton label="Kiểm tra" onClick={() => void validate(item)} disabled={busyItem === id || (resource === 'simulator-configurations' && !reviewFromItem(item)?.reviewed)} title={resource === 'simulator-configurations' && !reviewFromItem(item)?.reviewed ? 'Cần xem xét quan hệ trước khi kiểm tra' : undefined} /> : null}
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

function OptionFailure(props: { name: OptionName; state: OptionState; onRetry: () => void }) {
  if (props.state === 'ready') return null
  return <div className="notice notice-warning" role="alert"><span>{optionMessage(props.name, props.state)}</span>{props.state !== 'loading' ? <ManagementActionButton label="Thử lại" onClick={props.onRetry} tone="quiet" /> : null}</div>
}

function RelationshipReview(props: { review: ReviewState; onReviewed: () => void }) {
  const { review, onReviewed } = props
  return <div className="card form-card" aria-labelledby="relationship-review-title">
    <h2 id="relationship-review-title">Xem xét quan hệ bản nháp</h2>
    <p className="muted">Bản nháp <code>{review.id}</code> phiên bản {review.draftVersion}, Source {review.sourceLabel || review.sourceId}, chỉ được kích hoạt sau khi biên nhận quan hệ và biên nhận kiểm tra được lưu trên máy chủ.</p>
    <p><strong>Quan hệ được sao chép:</strong> {review.relationships.length ? review.relationships.join(', ') : 'Không có quan hệ tự động.'}</p>
    <p><strong>Trường bị loại trừ:</strong> {review.excluded.length ? review.excluded.join(', ') : 'Không có dữ liệu lịch sử hoặc bí mật nào được sao chép.'}</p>
    {review.relationshipStale ? <p className="notice notice-warning" role="alert">Quan hệ đã thay đổi; cần xem xét mới.</p> : null}
    {review.validationStale ? <p className="notice notice-warning" role="alert">Payload bản nháp đã thay đổi; cần kiểm tra mới.</p> : null}
    <p className="muted">Trạng thái máy chủ: quan hệ {review.reviewed ? 'đã xem xét' : 'chưa xem xét'}, kiểm tra {review.validated ? 'đã ghi nhận' : 'chưa ghi nhận'}.</p>
    <ManagementActionButton label={review.reviewed ? 'Đã xem xét' : 'Xem xét quan hệ'} onClick={onReviewed} disabled={review.reviewed} tone="primary" />
  </div>
}

function DetailPanel(props: { resource: string; detail: ManagementItem; onClose: () => void; supportedActions: string[]; review: ReviewState | null; onReview: (review: ReviewState) => void }) {
  return <div className="card form-card" role="dialog" aria-labelledby="configuration-detail-title">
    <div className="page-heading"><h2 id="configuration-detail-title">Chi tiết {resourceLabel(props.resource)}</h2><ManagementActionButton label="Đóng" onClick={props.onClose} tone="quiet" /></div>
    <dl className="detail-grid">{Object.entries(props.detail).filter(([key]) => !key.toLowerCase().includes('secret') && !key.toLowerCase().includes('token')).map(([key, value]) => <div key={key}><dt>{fieldLabel(key)}</dt><dd>{textValue(value) || '—'}</dd></div>)}</dl>
    {props.review && !props.review.reviewed ? <RelationshipReview review={props.review} onReviewed={() => props.onReview(props.review!)} /> : null}
    <p className="muted">Thao tác hợp lệ: {props.supportedActions.length ? props.supportedActions.join(', ') : 'Không có chuyển trạng thái trực tiếp.'}</p>
  </div>
}

function EditorPanel(props: { resource: string; mode: 'create' | 'edit'; form: Record<string, string>; onFieldChange: (key: string, value: string) => void; invalidField: string | null; siteOptions: Array<{ id: string; label: string }>; areaOptions: Array<{ id: string; label: string }>; assetOptions: Array<{ id: string; label: string }>; sourceOptions: Array<{ id: string; label: string }>; pointOptions: Array<{ id: string; label: string }>; optionStates: Record<OptionName, OptionState>; onRetry: () => void; busy: boolean; onSave: () => void; onCancel: () => void }) {
  const fields = editorFields(props.resource, props.mode)
  const selectOptions = { site: props.siteOptions, area: props.areaOptions, asset: props.assetOptions, source: props.sourceOptions, point: props.pointOptions } satisfies Record<SelectName, Array<{ id: string; label: string }>>
  const unavailable = fields.filter(field => field.select).map(field => optionNameForSelect(field.select!)).find(name => props.optionStates[name] !== 'ready') as OptionName | undefined
  return <div className="card form-card" role="dialog" aria-labelledby="configuration-editor-title"><h2 id="configuration-editor-title">{props.mode === 'create' ? 'Tạo mới' : 'Chỉnh sửa'} {resourceLabel(props.resource)}</h2>{unavailable ? <OptionFailure name={unavailable} state={props.optionStates[unavailable]} onRetry={props.onRetry} /> : null}<div className="filter-bar">{fields.map(field => <label className="field" key={field.key}><span className="field-label">{field.label}</span>{field.select ? <select id={`configuration-field-${field.key}`} className="input" disabled={props.optionStates[optionNameForSelect(field.select)] !== 'ready'} aria-invalid={props.invalidField === field.key || undefined} value={props.form[field.key] ?? ''} onChange={event => props.onFieldChange(field.key, event.target.value)}><option value="">-- Chọn {field.label.toLocaleLowerCase('vi')} --</option>{selectOptions[field.select].map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select> : <input id={`configuration-field-${field.key}`} className="input" type={field.type ?? 'text'} value={props.form[field.key] ?? ''} readOnly={field.readOnly} aria-readonly={field.readOnly || undefined} aria-invalid={props.invalidField === field.key || undefined} onChange={event => props.onFieldChange(field.key, event.target.value)} />}{field.help ? <small className="muted">{field.help}</small> : null}</label>)}</div><div className="actions-stack"><ManagementActionButton label={props.busy ? 'Đang lưu…' : 'Lưu'} tone="primary" disabled={props.busy || Boolean(unavailable)} title={unavailable ? optionMessage(unavailable, props.optionStates[unavailable]) : undefined} onClick={props.onSave} /><ManagementActionButton label="Hủy" onClick={props.onCancel} /></div></div>
}

function editorFields(resource: string, mode: 'create' | 'edit'): Array<{ key: string; label: string; type?: string; readOnly?: boolean; help?: string; select?: SelectName }> {
  const common = [{ key: 'name', label: 'Tên' }]
  const immutable = (label: string) => ({ label, readOnly: true, help: 'Trường quan hệ do miền sở hữu quản lý; không thể đổi trên bản ghi này.' })
  switch (resource) {
    case 'areas': return mode === 'create' ? [...common, { key: 'siteId', label: 'Địa điểm cha', select: 'site' as const }] : common
    case 'assets': return mode === 'create' ? [...common, { key: 'areaId', label: 'Khu vực cha', select: 'area' as const }] : common
    case 'points': return mode === 'create' ? [...common, { key: 'description', label: 'Mô tả' }, { key: 'assetId', label: 'Tài sản cha', select: 'asset' as const }, { key: 'metricId', label: 'Mã chỉ số' }, { key: 'unitId', label: 'Mã đơn vị' }, { key: 'dataOwnerUserId', label: 'Mã chủ dữ liệu' }, { key: 'expectedIntervalSeconds', label: 'Chu kỳ (giây)', type: 'number' }, { key: 'noDataAfterSeconds', label: 'No Data sau (giây)', type: 'number' }] : [...common, { key: 'description', label: 'Mô tả' }, { key: 'metricId', label: 'Mã chỉ số' }, { key: 'unitId', label: 'Mã đơn vị' }, { key: 'dataOwnerUserId', label: 'Mã chủ dữ liệu' }, { key: 'expectedIntervalSeconds', label: 'Chu kỳ (giây)', type: 'number' }, { key: 'noDataAfterSeconds', label: 'No Data sau (giây)', type: 'number' }]
    case 'data-sources': return mode === 'create' ? [...common, { key: 'siteId', label: 'Địa điểm', select: 'site' as const }] : common
    case 'source-point-mappings': return [{ key: 'sourceId', ...(mode === 'create' ? { label: 'Nguồn dữ liệu', select: 'source' as const } : immutable('Mã nguồn dữ liệu')) }, { key: 'pointId', ...(mode === 'create' ? { label: 'Điểm đo', select: 'point' as const } : immutable('Mã điểm đo')) }, { key: 'effectiveFromUtc', label: 'Hiệu lực từ', type: 'datetime-local' }, { key: 'effectiveToUtc', label: 'Hiệu lực đến', type: 'datetime-local' }]
    case 'simulator-configurations': return [{ key: 'sourceId', ...(mode === 'create' ? { label: 'Nguồn dữ liệu', select: 'source' as const } : immutable('Mã nguồn dữ liệu')) }, { key: 'scenarioType', label: 'Kịch bản' }, { key: 'minimumValue', label: 'Giá trị nhỏ nhất', type: 'number' }, { key: 'maximumValue', label: 'Giá trị lớn nhất', type: 'number' }, { key: 'intervalSeconds', label: 'Chu kỳ (giây)', type: 'number' }, { key: 'deterministicSeed', label: 'Hạt giống xác định', type: 'number' }]
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
    sourceCode: 'Mã nguồn', sourceName: 'Tên nguồn', sourceStatus: 'Trạng thái nguồn', sourceVersion: 'Phiên bản nguồn',
    reviewRelationships: 'Quan hệ cần xem xét', excludedFields: 'Trường bị loại trừ', relationshipReviewed: 'Đã xem xét quan hệ',
    validationRecorded: 'Đã ghi nhận kiểm tra', relationshipReceiptStale: 'Biên nhận quan hệ cũ', validationReceiptStale: 'Biên nhận kiểm tra cũ',
  } as Record<string, string>)[key] ?? key
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
  const hasSimulatorDraft = Number(detail.draftConfigurationVersion ?? 0) > Number(detail.currentConfigurationVersion ?? 0)
  const reviewNeeded = resource === 'simulator-configurations' && hasSimulatorDraft && !detail.relationshipReviewed
  return [...lifecycleActions(resource, textValue(detail.status)), ...(canDelete(resource, textValue(detail.status)) ? ['delete-draft'] : []), ...(reviewNeeded ? ['review'] : []), ...(resource !== 'simulator-configurations' || hasSimulatorDraft ? ['validate'] : []), 'duplicate']
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
