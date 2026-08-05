import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ChartContainer } from '../../components/charts/ChartContainer'
import { PageHeader } from '../../components/context/PageHeader'
import { DataQualityIndicator, type DataQuality } from '../../components/status/DataQualityIndicator'
import { FreshnessIndicator, type Freshness } from '../../components/status/FreshnessIndicator'
import { OperationalStatusBadge } from '../../components/status/OperationalStatusBadge'
import { BlockedState } from '../../components/feedback/BlockedState'
import { EmptyState } from '../../components/feedback/EmptyState'
import { ErrorState } from '../../components/feedback/ErrorState'
import { ForbiddenState } from '../../components/feedback/ForbiddenState'
import { ConflictState } from '../../components/feedback/ConflictState'
import { LoadingState } from '../../components/feedback/LoadingState'
import { RetryState } from '../../components/feedback/RetryState'
import { FeedbackBanner } from '../../components/feedback/FeedbackBanner'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { GatewayState, LatestSnapshot, TelemetryOptionSnapshot, TelemetrySelection } from '../../gateways/webGateways'
import { LatestRefreshCoordinator, mergeSelectedPointOption, type RefreshRequestContext } from './telemetryRefreshCoordinator'

type RequestedSelection = Partial<TelemetrySelection>
type CompleteTelemetrySelection = TelemetrySelection & { areaId: string; assetId: string }
type OptionLevel = 'sites' | 'areas' | 'assets' | 'points'

const noSelectionSnapshot: LatestSnapshot = { state: 'no-selection', value: null, health: 'Chưa chọn điểm', dataState: 'NoSelection' }
const loadingSnapshot: LatestSnapshot = { state: 'loading', value: null, health: 'Đang tải dữ liệu hiện tại' }

function readSelection(): RequestedSelection {
  const params = new URLSearchParams(window.location.search)
  return { siteId: params.get('siteId') ?? undefined, areaId: params.get('areaId') ?? undefined, assetId: params.get('assetId') ?? undefined, pointId: params.get('pointId') ?? undefined }
}

function writeSelection(selection: RequestedSelection) {
  const params = new URLSearchParams()
  if (selection.siteId) params.set('siteId', selection.siteId)
  if (selection.areaId) params.set('areaId', selection.areaId)
  if (selection.assetId) params.set('assetId', selection.assetId)
  if (selection.pointId) params.set('pointId', selection.pointId)
  const query = params.toString()
  window.history.replaceState({}, '', `${window.location.pathname}${query ? `?${query}` : ''}`)
}

function selectionKey(selection: RequestedSelection) { return [selection.siteId, selection.areaId, selection.assetId, selection.pointId].join('|') }

function pointOptionFromSnapshot(selection: CompleteTelemetrySelection, snapshot: LatestSnapshot) {
  if (snapshot.pointId !== selection.pointId || !snapshot.pointCode) return undefined
  return {
    pointId: selection.pointId,
    siteId: selection.siteId,
    areaId: selection.areaId,
    assetId: selection.assetId,
    code: snapshot.pointCode,
    name: snapshot.pointName ?? snapshot.pointCode,
    metric: snapshot.metric ?? snapshot.pointCode,
    unit: snapshot.unit ?? '',
  }
}

function failureMessage(state: LatestSnapshot['state']) {
  if (state === 'dependency') return 'Dịch vụ dữ liệu đo tạm thời không sẵn sàng.'
  if (state === 'runtime-error') return 'Không thể kết nối đến dịch vụ dữ liệu đo.'
  if (state === 'forbidden') return 'Bạn không có quyền xem lựa chọn này.'
  if (state === 'not-found') return 'Không tìm thấy lựa chọn hợp lệ.'
  if (state === 'expired') return 'Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để tiếp tục.'
  if (state === 'validation' || state === 'conflict') return 'Hierarchy đã thay đổi hoặc không còn hợp lệ.'
  return 'Không thể tải dữ liệu.'
}

export function qualityOf(value: string | undefined): DataQuality {
  if (value === 'Good' || value === 'Uncertain' || value === 'Bad' || value === 'Missing') return value
  return 'Missing'
}

function freshnessOf(snapshot: LatestSnapshot): Freshness {
  const sourceHealth = snapshot.health.toLowerCase()
  if (sourceHealth === 'online' || sourceHealth === 'available' || sourceHealth === 'good') return 'Live'
  if (sourceHealth === 'stale') return 'Stale'
  if (['nodata', 'no data', 'suspended', 'decommissioned'].includes(sourceHealth)) return 'Degraded'
  return 'Unavailable'
}

function operationalStatusOf(health: string): 'Available' | 'Unavailable' | 'Stale' | 'Uncertain' | 'Missing' | 'Blocked' {
  const value = health.toLowerCase()
  if (value === 'online' || value === 'available' || value === 'good') return 'Available'
  if (value === 'stale') return 'Stale'
  if (value === 'nodata' || value === 'no data') return 'Missing'
  if (value === 'suspended') return 'Blocked'
  if (value === 'uncertain' || value === 'degraded') return 'Uncertain'
  return 'Unavailable'
}

export type TelemetryPresentation = 'no-selection' | 'not-configured' | 'no-data' | 'data' | 'conflict' | 'forbidden' | 'not-found' | 'dependency' | 'runtime-error' | 'expired' | 'retryable-stale' | 'loading'

export function isExpiredSessionState(state: GatewayState): boolean {
  return state === 'expired'
}

export function hasNumericTelemetryData(snapshot: LatestSnapshot, expectedPointId?: string): boolean {
  return snapshot.state === 'ready'
    && snapshot.dataState === 'Data'
    && Boolean(snapshot.pointId)
    && (!expectedPointId || snapshot.pointId === expectedPointId)
    && typeof snapshot.value === 'number'
    && Number.isFinite(snapshot.value)
}

export function isRetainableTelemetrySnapshot(snapshot: LatestSnapshot, expectedPointId?: string): boolean {
  if (!snapshot.pointId || (expectedPointId && snapshot.pointId !== expectedPointId)) return false
  if (snapshot.dataState === 'Data') return hasNumericTelemetryData(snapshot, expectedPointId)
  if (snapshot.dataState === 'NoData') {
    const health = snapshot.health.trim().toLowerCase()
    return snapshot.state === 'no-data' && health.length > 0 && !['unavailable', 'unknown', 'đang tải dữ liệu hiện tại'].includes(health)
  }
  return false
}

export type TelemetryClassificationInput = {
  gatewayState: GatewayState
  dataState?: LatestSnapshot['dataState']
  snapshot?: LatestSnapshot
  previousSnapshot?: LatestSnapshot
  selectedPointId?: string
  requestPending?: boolean
  retryableRefresh?: boolean
  /** Retained for source compatibility; it is never trusted for new retry decisions. */
  hasUsableSnapshot?: boolean
}

export function classifyTelemetryState({ gatewayState, dataState, snapshot, previousSnapshot, selectedPointId, requestPending = false, retryableRefresh = false }: TelemetryClassificationInput): TelemetryPresentation {
  const resolvedDataState = dataState ?? snapshot?.dataState
  if (gatewayState === 'no-selection' || resolvedDataState === 'NoSelection') return 'no-selection'
  if (requestPending || gatewayState === 'loading') return 'loading'
  if (gatewayState === 'expired') return 'expired'
  if (gatewayState === 'forbidden') return 'forbidden'
  if (gatewayState === 'not-found') return 'not-found'
  if (gatewayState === 'conflict' || gatewayState === 'validation' || resolvedDataState === 'Ambiguous' || resolvedDataState === 'HierarchyConflict') return 'conflict'
  if (resolvedDataState === 'NotConfigured') return 'not-configured'
  if (resolvedDataState === 'NoData' || gatewayState === 'no-data') return 'no-data'
  if (retryableRefresh && ['dependency', 'runtime-error', 'error'].includes(gatewayState) &&
    isRetainableTelemetrySnapshot(previousSnapshot ?? snapshot ?? noSelectionSnapshot, selectedPointId)) return 'retryable-stale'
  if (gatewayState === 'dependency') return 'dependency'
  if (gatewayState === 'runtime-error' || gatewayState === 'error') return 'runtime-error'
  if (gatewayState === 'ready' && resolvedDataState === 'Data' && hasNumericTelemetryData(snapshot ?? noSelectionSnapshot, selectedPointId)) return 'data'
  return 'runtime-error'
}

export function formatIntervalSeconds(value?: number): string {
  return typeof value === 'number' && Number.isFinite(value) ? `${value}s` : 'Chưa có'
}

export function PointCurrentRoute({ onSessionRecovery }: { onSessionRecovery?: () => void } = {}) {
  const gateways = useWebGateways()
  const [selection, setSelection] = useState<RequestedSelection>(readSelection)
  const [options, setOptions] = useState<TelemetryOptionSnapshot>({ state: 'loading', sites: [], areas: [], assets: [], points: [] })
  const [pointPage, setPointPage] = useState(1)
  const [pointSearch, setPointSearch] = useState('')
  const [pointSearchDraft, setPointSearchDraft] = useState('')
  const [snapshot, setSnapshot] = useState<LatestSnapshot>(noSelectionSnapshot)
  const snapshotRef = useRef<LatestSnapshot>(noSelectionSnapshot)
  const [autoRefresh, setAutoRefresh] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [lastError, setLastError] = useState<LatestSnapshot['state'] | undefined>()
  const [sessionExpired, setSessionExpired] = useState(false)
  const optionRequestSequences = useRef<Record<OptionLevel, number>>({ sites: 0, areas: 0, assets: 0, points: 0 })
  const selectedPointOption = useRef<TelemetryOptionSnapshot['points'][number] | undefined>(undefined)
  const refreshCoordinator = useRef<LatestRefreshCoordinator | null>(null)
  if (refreshCoordinator.current === null) {
    refreshCoordinator.current = new LatestRefreshCoordinator(
      (callback, delayMs) => window.setTimeout(callback, delayMs),
      handle => window.clearTimeout(handle),
      10_000,
      event => setRefreshing(event.type === 'started'),
    )
  }

  const stopForExpiredSession = useCallback(() => {
    refreshCoordinator.current?.clear()
    setAutoRefresh(false)
    setRefreshing(false)
    setLastError('expired')
    setSessionExpired(true)
  }, [])

  const loadLevel = useCallback(async (
    level: OptionLevel,
    request: RequestedSelection,
    page = 1,
    search = '',
  ): Promise<boolean> => {
    if (sessionExpired) return false
    if (!gateways.latest.getOptions) { setOptions(previous => ({ ...previous, state: 'ready' })); return true }
    const sequence = ++optionRequestSequences.current[level]
    if (level === 'sites') setOptions(previous => ({ ...previous, state: 'loading' }))
    const next = await gateways.latest.getOptions({
      level, siteId: request.siteId, areaId: request.areaId, assetId: request.assetId,
      page: level === 'points' ? page : undefined,
      pageSize: level === 'points' ? 100 : undefined,
      search: level === 'points' ? search : undefined,
    })
    if (optionRequestSequences.current[level] !== sequence) return false
    if (isExpiredSessionState(next.state)) {
      stopForExpiredSession()
      setOptions(previous => ({ ...previous, state: next.state, errorCode: next.errorCode }))
      return false
    }
    setOptions(previous => {
      if (next.state !== 'ready') return { ...previous, state: next.state, errorCode: next.errorCode }
      if (level === 'sites') return { ...previous, state: 'ready', sites: next.sites, errorCode: undefined }
      if (level === 'areas') return { ...previous, state: 'ready', areas: next.areas, errorCode: undefined }
      if (level === 'assets') return { ...previous, state: 'ready', assets: next.assets, errorCode: undefined }
      const preserved = request.pointId === selectedPointOption.current?.pointId ? selectedPointOption.current : undefined
      return { ...previous, state: 'ready', points: mergeSelectedPointOption(next.points, preserved), scopedCount: next.scopedCount, page: next.page, pageSize: next.pageSize, errorCode: undefined }
    })
    return true
  }, [gateways.latest, sessionExpired, stopForExpiredSession])

  const loadOptions = useCallback(async () => {
    if (!await loadLevel('sites', {})) return
    if (selection.siteId && !await loadLevel('areas', selection)) return
    if (selection.siteId && selection.areaId && !await loadLevel('assets', selection)) return
    if (selection.siteId && selection.areaId && selection.assetId)
      await loadLevel('points', selection, pointPage, pointSearch)
  }, [loadLevel, selection, pointPage, pointSearch])

  useEffect(() => { void loadLevel('sites', {}) }, [loadLevel])
  useEffect(() => {
    if (!selection.siteId) {
      optionRequestSequences.current.areas++
      setOptions(previous => ({ ...previous, areas: [], assets: [], points: [] }))
      return
    }
    void loadLevel('areas', { siteId: selection.siteId })
  }, [selection.siteId, loadLevel])
  useEffect(() => {
    if (!selection.siteId || !selection.areaId) {
      optionRequestSequences.current.assets++
      setOptions(previous => ({ ...previous, assets: [], points: [] }))
      return
    }
    void loadLevel('assets', { siteId: selection.siteId, areaId: selection.areaId })
  }, [selection.siteId, selection.areaId, loadLevel])
  useEffect(() => {
    if (!selection.siteId || !selection.areaId || !selection.assetId) {
      optionRequestSequences.current.points++
      setOptions(previous => ({ ...previous, points: [], scopedCount: 0 }))
      return
    }
    void loadLevel('points', {
      siteId: selection.siteId,
      areaId: selection.areaId,
      assetId: selection.assetId,
      pointId: selection.pointId,
    }, pointPage, pointSearch)
  }, [selection.siteId, selection.areaId, selection.assetId, selection.pointId, pointPage, pointSearch, loadLevel])

  const selected = useMemo<CompleteTelemetrySelection | undefined>(() => {
    if (!selection.siteId || !selection.areaId || !selection.assetId || !selection.pointId) return undefined
    return { siteId: selection.siteId, areaId: selection.areaId, assetId: selection.assetId, pointId: selection.pointId }
  }, [selection])
  const key = selectionKey(selection)
  const totalPointPages = Math.max(1, Math.ceil((options.scopedCount ?? 0) / (options.pageSize ?? 100)))
  const commitSnapshot = useCallback((next: LatestSnapshot) => {
    snapshotRef.current = next
    setSnapshot(next)
  }, [])

  const applySnapshot = useCallback((currentSelection: CompleteTelemetrySelection, next: LatestSnapshot) => {
    const selectedPoint = pointOptionFromSnapshot(currentSelection, next)
    if (selectedPoint) {
      selectedPointOption.current = selectedPoint
      setOptions(previous => ({ ...previous, points: mergeSelectedPointOption(previous.points, selectedPoint) }))
    }
    const malformedData = next.dataState === 'Data' && !hasNumericTelemetryData(next, currentSelection.pointId)
    const normalized = malformedData ? { ...next, state: 'runtime-error' as const, value: null, errorCode: 'MALFORMED_DATA' } : next
    const failed = ['dependency', 'runtime-error', 'forbidden', 'expired', 'not-found', 'validation', 'conflict', 'error'].includes(normalized.state)
    if (failed) {
      setLastError(normalized.state)
      if (isExpiredSessionState(normalized.state)) {
        stopForExpiredSession()
      }
      const canRetain = !malformedData && ['dependency', 'runtime-error', 'error'].includes(normalized.state) &&
        isRetainableTelemetrySnapshot(snapshotRef.current, currentSelection.pointId)
      if (!canRetain) commitSnapshot({ ...normalized, value: null, health: 'Unavailable' })
    } else {
      setLastError(undefined)
      commitSnapshot(normalized)
    }
  }, [commitSnapshot, stopForExpiredSession])

  const requestSnapshot = useCallback(async (currentSelection: CompleteTelemetrySelection, context: RefreshRequestContext) => {
    const next = await gateways.latest.getSnapshot(currentSelection, context.signal)
    if (context.isCurrent()) applySnapshot(currentSelection, next)
  }, [applySnapshot, gateways.latest])

  useEffect(() => {
    if (!selected) {
      refreshCoordinator.current?.clear()
      selectedPointOption.current = undefined
      commitSnapshot(noSelectionSnapshot)
      setLastError(undefined)
      setRefreshing(false)
      return
    }
    if (sessionExpired) {
      refreshCoordinator.current?.clear()
      setRefreshing(false)
      return
    }
    selectedPointOption.current = undefined
    commitSnapshot(loadingSnapshot)
    setLastError(undefined)
    refreshCoordinator.current?.select(key, context => requestSnapshot(selected, context))
    return () => refreshCoordinator.current?.clear()
  }, [commitSnapshot, key, requestSnapshot, selected, selection, sessionExpired])

  useEffect(() => { refreshCoordinator.current?.setAutoRefresh(autoRefresh && !sessionExpired) }, [autoRefresh, sessionExpired])

  const clearSelectionSnapshot = () => {
    commitSnapshot(noSelectionSnapshot)
    setLastError(undefined)
    setRefreshing(false)
  }
  const resetPoints = () => { selectedPointOption.current = undefined; setPointPage(1); setPointSearch(''); setPointSearchDraft(''); setOptions(previous => ({ ...previous, points: [], scopedCount: 0 })) }
  const changeSite = (siteId: string) => { const next = { siteId: siteId || undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next); setOptions(previous => ({ ...previous, areas: [], assets: [] })); resetPoints() }
  const changeArea = (areaId: string) => { const next = { ...selection, areaId: areaId || undefined, assetId: undefined, pointId: undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next); setOptions(previous => ({ ...previous, assets: [] })); resetPoints() }
  const changeAsset = (assetId: string) => { const next = { ...selection, assetId: assetId || undefined, pointId: undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next); resetPoints() }
  const changePoint = (pointId: string) => { selectedPointOption.current = undefined; const next = { ...selection, pointId: pointId || undefined }; commitSnapshot(pointId ? loadingSnapshot : noSelectionSnapshot); setLastError(undefined); setRefreshing(false); setSelection(next); writeSelection(next) }

  const hasData = hasNumericTelemetryData(snapshot, selected?.pointId)
  const currentGatewayState = lastError ?? snapshot.state
  const presentation = classifyTelemetryState({
    gatewayState: currentGatewayState,
    dataState: lastError ? undefined : snapshot.dataState,
    snapshot,
    previousSnapshot: snapshotRef.current,
    selectedPointId: selected?.pointId,
    requestPending: Boolean(selected && snapshot.state === 'loading' && !lastError),
    retryableRefresh: Boolean(lastError),
  })
  const retry = <button type="button" className="button button-secondary" disabled={refreshing} onClick={() => refreshCoordinator.current?.refresh()}>Thử lại</button>

  const renderError = (state: LatestSnapshot['state']) => {
    if (state === 'forbidden' || state === 'not-found') return <ForbiddenState message={failureMessage(state)} action={retry} />
    if (state === 'expired') return <FeedbackBanner tone="warning" title="Phiên đăng nhập hết hạn" message={failureMessage(state)} action={<button type="button" className="button button-secondary" onClick={() => onSessionRecovery?.()}>Tải lại phiên đăng nhập</button>} live />
    if (state === 'conflict' || state === 'validation') return <ConflictState message={failureMessage(state)} action={<button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Tải lại hierarchy</button>} />
    if (state === 'dependency') return <BlockedState message={failureMessage(state)} nextAction={retry} />
    return <ErrorState message={failureMessage(state)} action={retry} />
  }

  const renderOptionsState = () => {
    if (options.state === 'forbidden' || options.state === 'not-found') return <ForbiddenState message={failureMessage(options.state)} action={<button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Tải lại hierarchy</button>} />
    if (options.state === 'expired') return <FeedbackBanner tone="warning" title="Phiên đăng nhập hết hạn" message={failureMessage(options.state)} action={<button type="button" className="button button-secondary" onClick={() => onSessionRecovery?.()}>Tải lại phiên đăng nhập</button>} live />
    if (options.state === 'dependency') return <BlockedState message="Dịch vụ hierarchy tạm thời chưa sẵn sàng." nextAction={<button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Thử lại</button>} />
    if (options.state === 'conflict' || options.state === 'validation') return <ConflictState message="Hierarchy đã thay đổi; hãy tải lại rồi chọn lại phạm vi." action={<button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Tải lại hierarchy</button>} />
    return <ErrorState message="Không thể tải hierarchy được cấp quyền." action={<button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Thử lại</button>} />
  }

  return (
    <section className="page" aria-labelledby="telemetry-title">
      <PageHeader titleId="telemetry-title" eyebrow="Giám sát đo lường" title="Dữ liệu mới nhất & sức khỏe nguồn" description="Chọn rõ Site, Area, Asset và điểm đo để xem dữ liệu được cấp quyền." />
       <article className="card telemetry-selector" aria-label="Bộ chọn phân cấp đo lường">
         <div className="card-header"><div><p className="card-kicker">Phạm vi được cấp quyền</p><h2>Chọn phân cấp</h2></div>{!sessionExpired && <button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Tải lại lựa chọn</button>}</div>
        {options.state === 'loading' && <LoadingState message="Đang tải hierarchy được cấp quyền…" />}
        {options.state !== 'loading' && options.state !== 'ready' && renderOptionsState()}
        {options.state === 'ready' && options.sites.length === 0 && <EmptyState title="Chưa có hierarchy" message="Không có Site nào trong phạm vi được cấp quyền." />}
        {options.state === 'ready' && options.sites.length > 0 && <div className="selector-grid">
          <label>Site<select value={selection.siteId ?? ''} onChange={event => changeSite(event.target.value)}><option value="">Chọn Site</option>{options.sites.map(site => <option key={site.siteId} value={site.siteId}>{site.code} — {site.name}</option>)}</select></label>
          <label>Area<select value={selection.areaId ?? ''} disabled={!selection.siteId} onChange={event => changeArea(event.target.value)}><option value="">{selection.siteId ? 'Chọn Area' : 'Chọn Site trước'}</option>{options.areas.map(area => <option key={area.areaId} value={area.areaId}>{area.code} — {area.name}</option>)}</select></label>
          <label>Asset<select value={selection.assetId ?? ''} disabled={!selection.areaId} onChange={event => changeAsset(event.target.value)}><option value="">{selection.areaId ? 'Chọn Asset' : 'Chọn Area trước'}</option>{options.assets.map(asset => <option key={asset.assetId} value={asset.assetId}>{asset.code} — {asset.name}</option>)}</select></label>
          <label>Điểm đo<select value={selection.pointId ?? ''} disabled={!selection.assetId} onChange={event => changePoint(event.target.value)}><option value="">{selection.assetId ? 'Chọn điểm đo' : 'Chọn Asset trước'}</option>{options.points.map(point => <option key={point.pointId} value={point.pointId}>{point.code} — {point.name}</option>)}</select></label>
        </div>}
        {options.state === 'ready' && selection.assetId && <div className="toolbar" role="group" aria-label="Tìm và phân trang điểm đo"><label>Tìm điểm đo<input value={pointSearchDraft} maxLength={100} onChange={event => setPointSearchDraft(event.target.value)} /></label><button type="button" className="button button-secondary" onClick={() => { setPointPage(1); setPointSearch(pointSearchDraft.trim()) }}>Tìm</button><button type="button" className="button button-secondary" disabled={pointPage <= 1} onClick={() => setPointPage(value => Math.max(1, value - 1))}>Trang trước</button><span>{`Trang ${pointPage} / ${totalPointPages} · ${options.scopedCount ?? 0} điểm`}</span><button type="button" className="button button-secondary" disabled={pointPage >= totalPointPages} onClick={() => setPointPage(value => value + 1)}>Trang sau</button></div>}
      </article>
      {selected && !sessionExpired && <div className="telemetry-refresh" role="group" aria-label="Bộ điều khiển làm mới"><label><input type="checkbox" checked={autoRefresh} onChange={event => setAutoRefresh(event.target.checked)} /> Tự động làm mới mỗi 10 giây</label><button type="button" className="button button-secondary" disabled={refreshing} onClick={() => refreshCoordinator.current?.refresh()}>Làm mới ngay</button>{refreshing && <span role="status">Đang làm mới…</span>}</div>}
      {presentation === 'retryable-stale' && <RetryState message="Đang hiển thị bằng chứng nhận được gần nhất; lần làm mới mới nhất chưa thành công." onRetry={() => refreshCoordinator.current?.refresh()} />}
      {['forbidden', 'not-found', 'expired', 'conflict', 'dependency', 'runtime-error'].includes(presentation) && renderError(currentGatewayState)}
      {!selected && <EmptyState title="Chưa chọn điểm đo" message="Chọn đầy đủ Site, Area, Asset và điểm đo để xem dữ liệu mới nhất." />}
      {selected && presentation === 'loading' && <LoadingState message="Đang tải dữ liệu mới nhất và sức khỏe nguồn…" />}
      {selected && presentation === 'not-configured' && <EmptyState title="Chưa cấu hình điểm đo" message="Điểm đo hoặc cấu hình tương ứng chưa sẵn sàng để nhận Measurement." action={<button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Tải lại hierarchy</button>} />}
      {selected && presentation === 'no-data' && <FeedbackBanner tone="warning" title="No Data" message="Cấu hình có thể tồn tại nhưng chưa có Measurement được chấp nhận trong khoảng theo dõi hiện tại." live={false} />}
      {selected && (presentation === 'data' || presentation === 'no-data' || presentation === 'retryable-stale') && <>
        <div className="telemetry-evidence-grid">
          <article className="card latest-card"><div className="card-header"><div><p className="card-kicker">Điểm đo {snapshot.pointCode ?? selected.pointId}</p><h2>{snapshot.pointName ?? 'Quan sát mới nhất'}</h2></div><OperationalStatusBadge status={hasData ? 'Available' : 'Missing'} /></div>
            <p className="muted">Chỉ số: {snapshot.metric ?? 'Chưa có trong contract'} · Đơn vị: {snapshot.unit ?? '—'}</p>
            <div className="latest-value"><strong>{hasData ? snapshot.value : 'No Data'}</strong><span>{hasData ? snapshot.unit ?? 'value' : ''}</span></div>
            <div className="evidence-status-row"><DataQualityIndicator quality={qualityOf(snapshot.quality)} reason={snapshot.reason} /><FreshnessIndicator freshness={freshnessOf(snapshot)} lastRefresh={snapshot.lastRefreshAt} /></div>
            <dl className="readiness-list"><div><dt>Thời điểm nguồn</dt><dd>{snapshot.sourceTimestamp ?? 'Chưa có'}</dd></div><div><dt>Thời điểm nhận</dt><dd>{snapshot.receivedTimestamp ?? 'Chưa có'}</dd></div><div><dt>Lần truy vấn</dt><dd>{snapshot.lastRefreshAt ?? 'Chưa có'}</dd></div></dl>
          </article>
          <article className="card"><p className="card-kicker">Sức khỏe nguồn / lượt chạy</p><div className="evidence-status-row"><OperationalStatusBadge status={operationalStatusOf(snapshot.health)} detail={snapshot.health} /><span className="metadata">Nguồn: {snapshot.source?.name ?? 'Chưa xác định'}</span></div><dl className="readiness-list"><div><dt>Mã lượt chạy</dt><dd>{snapshot.runId ?? '—'}</dd></div><div><dt>Trạng thái lượt chạy</dt><dd>{snapshot.runStatus ?? '—'}</dd></div><div><dt>Đã tạo / chấp nhận / từ chối</dt><dd>{snapshot.generated ?? '—'} / {snapshot.accepted ?? '—'} / {snapshot.rejected ?? '—'}</dd></div><div><dt>Lần sản xuất cuối</dt><dd>{snapshot.lastProductionAtUtc ?? '—'}</dd></div><div><dt>Ngưỡng không có dữ liệu</dt><dd>{formatIntervalSeconds(snapshot.noDataAfterSeconds)}</dd></div></dl></article>
        </div>
        <FeedbackBanner tone="info" title="Phạm vi bằng chứng" message="Coverage và chuỗi lịch sử chưa được cung cấp bởi contract hiện tại; không suy diễn thành 0 hoặc dữ liệu lịch sử." live={false} />
        <ChartContainer title="Lịch sử điểm đo" description="Chỉ hiển thị khi contract cung cấp chuỗi thời gian có timestamp." points={[]} metadata={{ metric: snapshot.metric, unit: snapshot.unit, timezone: 'Asia/Ho_Chi_Minh' }} unavailableReason="Historical series chưa có trong contract hiện tại." />
      </>}
      {selected && (presentation === 'no-data' || presentation === 'data' || presentation === 'retryable-stale') && <p className="muted">Khoảng kỳ vọng: {formatIntervalSeconds(snapshot.expectedIntervalSeconds)} · Không có dữ liệu sau: {formatIntervalSeconds(snapshot.noDataAfterSeconds)}</p>}
    </section>
  )
}
