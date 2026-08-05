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
import { LoadingState } from '../../components/feedback/LoadingState'
import { RetryState } from '../../components/feedback/RetryState'
import { FeedbackBanner } from '../../components/feedback/FeedbackBanner'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { LatestSnapshot, TelemetryOptionSnapshot, TelemetrySelection } from '../../gateways/webGateways'
import { LatestRefreshCoordinator, mergeSelectedPointOption, type RefreshRequestContext } from './telemetryRefreshCoordinator'

type RequestedSelection = Partial<TelemetrySelection>
type CompleteTelemetrySelection = TelemetrySelection & { areaId: string; assetId: string }
type OptionLevel = 'sites' | 'areas' | 'assets' | 'points'

const emptySnapshot: LatestSnapshot = { state: 'no-selection', value: null, health: 'Chưa chọn điểm', dataState: 'NoSelection' }

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
  if (state === 'validation' || state === 'conflict') return 'Hierarchy đã chọn không hợp lệ.'
  return 'Không thể tải dữ liệu.'
}

function qualityOf(value: string | undefined, hasData: boolean): DataQuality | undefined {
  if (value === 'Good' || value === 'Uncertain' || value === 'Bad' || value === 'Missing') return value
  return hasData ? undefined : 'Missing'
}

function freshnessOf(snapshot: LatestSnapshot): Freshness {
  const sourceHealth = snapshot.health.toLowerCase()
  if (sourceHealth === 'online' || sourceHealth === 'available' || sourceHealth === 'good') return 'Live'
  if (sourceHealth === 'stale') return 'Stale'
  if (['nodata', 'no data', 'suspended', 'decommissioned'].includes(sourceHealth)) return 'Degraded'
  return 'Unavailable'
}

function operationalStatusOf(health: string): 'Available' | 'Unavailable' | 'Stale' | 'Uncertain' {
  const value = health.toLowerCase()
  if (value === 'online' || value === 'available' || value === 'good') return 'Available'
  if (value === 'stale') return 'Stale'
  if (value === 'uncertain' || value === 'degraded') return 'Uncertain'
  return 'Unavailable'
}

export function PointCurrentRoute() {
  const gateways = useWebGateways()
  const [selection, setSelection] = useState<RequestedSelection>(readSelection)
  const [options, setOptions] = useState<TelemetryOptionSnapshot>({ state: 'loading', sites: [], areas: [], assets: [], points: [] })
  const [pointPage, setPointPage] = useState(1)
  const [pointSearch, setPointSearch] = useState('')
  const [pointSearchDraft, setPointSearchDraft] = useState('')
  const [snapshot, setSnapshot] = useState<LatestSnapshot>(emptySnapshot)
  const [autoRefresh, setAutoRefresh] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [lastError, setLastError] = useState<LatestSnapshot['state'] | undefined>()
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

  const loadLevel = useCallback(async (
    level: OptionLevel,
    request: RequestedSelection,
    page = 1,
    search = '',
  ) => {
    if (!gateways.latest.getOptions) { setOptions(previous => ({ ...previous, state: 'ready' })); return }
    const sequence = ++optionRequestSequences.current[level]
    if (level === 'sites') setOptions(previous => ({ ...previous, state: 'loading' }))
    const next = await gateways.latest.getOptions({
      level, siteId: request.siteId, areaId: request.areaId, assetId: request.assetId,
      page: level === 'points' ? page : undefined,
      pageSize: level === 'points' ? 100 : undefined,
      search: level === 'points' ? search : undefined,
    })
    if (optionRequestSequences.current[level] !== sequence) return
    setOptions(previous => {
      if (next.state !== 'ready') return { ...previous, state: next.state, errorCode: next.errorCode }
      if (level === 'sites') return { ...previous, state: 'ready', sites: next.sites, errorCode: undefined }
      if (level === 'areas') return { ...previous, state: 'ready', areas: next.areas, errorCode: undefined }
      if (level === 'assets') return { ...previous, state: 'ready', assets: next.assets, errorCode: undefined }
      const preserved = request.pointId === selectedPointOption.current?.pointId ? selectedPointOption.current : undefined
      return { ...previous, state: 'ready', points: mergeSelectedPointOption(next.points, preserved), scopedCount: next.scopedCount, page: next.page, pageSize: next.pageSize, errorCode: undefined }
    })
  }, [gateways.latest])

  const loadOptions = useCallback(async () => {
    await loadLevel('sites', {})
    if (selection.siteId) await loadLevel('areas', selection)
    if (selection.siteId && selection.areaId) await loadLevel('assets', selection)
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

  const applySnapshot = useCallback((currentSelection: CompleteTelemetrySelection, next: LatestSnapshot) => {
    const selectedPoint = pointOptionFromSnapshot(currentSelection, next)
    if (selectedPoint) {
      selectedPointOption.current = selectedPoint
      setOptions(previous => ({ ...previous, points: mergeSelectedPointOption(previous.points, selectedPoint) }))
    }
    const failed = ['dependency', 'runtime-error', 'forbidden', 'expired', 'not-found', 'validation', 'conflict', 'error'].includes(next.state)
    if (failed) {
      setLastError(next.state)
      if (!['dependency', 'runtime-error', 'error'].includes(next.state)) setSnapshot(emptySnapshot)
    } else {
      setLastError(undefined)
      setSnapshot(next)
    }
  }, [])

  const requestSnapshot = useCallback(async (currentSelection: CompleteTelemetrySelection, context: RefreshRequestContext) => {
    const next = await gateways.latest.getSnapshot(currentSelection, context.signal)
    if (context.isCurrent()) applySnapshot(currentSelection, next)
  }, [applySnapshot, gateways.latest])

  useEffect(() => {
    if (!selected) {
      refreshCoordinator.current?.clear()
      selectedPointOption.current = undefined
      setSnapshot(emptySnapshot)
      setLastError(undefined)
      setRefreshing(false)
      return
    }
    selectedPointOption.current = undefined
    setSnapshot(emptySnapshot)
    setLastError(undefined)
    refreshCoordinator.current?.select(key, context => requestSnapshot(selected, context))
    return () => refreshCoordinator.current?.clear()
  }, [key, requestSnapshot, selected, selection])

  useEffect(() => { refreshCoordinator.current?.setAutoRefresh(autoRefresh) }, [autoRefresh])

  const clearSelectionSnapshot = () => {
    setSnapshot(emptySnapshot)
    setLastError(undefined)
    setRefreshing(false)
  }
  const resetPoints = () => { selectedPointOption.current = undefined; setPointPage(1); setPointSearch(''); setPointSearchDraft(''); setOptions(previous => ({ ...previous, points: [], scopedCount: 0 })) }
  const changeSite = (siteId: string) => { const next = { siteId: siteId || undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next); setOptions(previous => ({ ...previous, areas: [], assets: [] })); resetPoints() }
  const changeArea = (areaId: string) => { const next = { ...selection, areaId: areaId || undefined, assetId: undefined, pointId: undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next); setOptions(previous => ({ ...previous, assets: [] })); resetPoints() }
  const changeAsset = (assetId: string) => { const next = { ...selection, assetId: assetId || undefined, pointId: undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next); resetPoints() }
  const changePoint = (pointId: string) => { selectedPointOption.current = undefined; const next = { ...selection, pointId: pointId || undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next) }

  const hasData = snapshot.state === 'ready' && snapshot.dataState === 'Data' && snapshot.value !== null
  const hasUsableSnapshot = snapshot.state === 'ready' || snapshot.state === 'no-data'
  const errorState = lastError ?? (!hasUsableSnapshot && snapshot.state !== 'no-selection' ? snapshot.state : undefined)
  const showingStaleSnapshot = hasUsableSnapshot && Boolean(lastError)
  const retry = <button type="button" className="button button-secondary" disabled={refreshing} onClick={() => refreshCoordinator.current?.refresh()}>Thử lại</button>

  const renderError = (state: LatestSnapshot['state']) => {
    if (state === 'forbidden' || state === 'not-found') return <ForbiddenState message={failureMessage(state)} action={retry} />
    if (state === 'dependency') return <BlockedState message={failureMessage(state)} nextAction={retry} />
    return <ErrorState message={failureMessage(state)} action={retry} />
  }

  return (
    <section className="page" aria-labelledby="telemetry-title">
      <PageHeader eyebrow="Giám sát đo lường" title="Dữ liệu mới nhất & sức khỏe nguồn" description="Chọn rõ Site, Area, Asset và điểm đo để xem dữ liệu được cấp quyền." />
      <article className="card telemetry-selector" aria-label="Bộ chọn phân cấp đo lường">
        <div className="card-header"><div><p className="card-kicker">Phạm vi được cấp quyền</p><h2>Chọn phân cấp</h2></div><button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Tải lại lựa chọn</button></div>
        {options.state === 'loading' && <LoadingState message="Đang tải hierarchy được cấp quyền…" />}
        {options.state !== 'loading' && options.state !== 'ready' && <ErrorState message={failureMessage(options.state)} action={<button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Thử lại</button>} />}
        {options.state === 'ready' && options.sites.length === 0 && <EmptyState title="Chưa có hierarchy" message="Không có Site nào trong phạm vi được cấp quyền." />}
        {options.state === 'ready' && options.sites.length > 0 && <div className="selector-grid">
          <label>Site<select value={selection.siteId ?? ''} onChange={event => changeSite(event.target.value)}><option value="">Chọn Site</option>{options.sites.map(site => <option key={site.siteId} value={site.siteId}>{site.code} — {site.name}</option>)}</select></label>
          <label>Area<select value={selection.areaId ?? ''} disabled={!selection.siteId} onChange={event => changeArea(event.target.value)}><option value="">{selection.siteId ? 'Chọn Area' : 'Chọn Site trước'}</option>{options.areas.map(area => <option key={area.areaId} value={area.areaId}>{area.code} — {area.name}</option>)}</select></label>
          <label>Asset<select value={selection.assetId ?? ''} disabled={!selection.areaId} onChange={event => changeAsset(event.target.value)}><option value="">{selection.areaId ? 'Chọn Asset' : 'Chọn Area trước'}</option>{options.assets.map(asset => <option key={asset.assetId} value={asset.assetId}>{asset.code} — {asset.name}</option>)}</select></label>
          <label>Điểm đo<select value={selection.pointId ?? ''} disabled={!selection.assetId} onChange={event => changePoint(event.target.value)}><option value="">{selection.assetId ? 'Chọn điểm đo' : 'Chọn Asset trước'}</option>{options.points.map(point => <option key={point.pointId} value={point.pointId}>{point.code} — {point.name}</option>)}</select></label>
        </div>}
        {options.state === 'ready' && selection.assetId && <div className="toolbar" role="group" aria-label="Tìm và phân trang điểm đo"><label>Tìm điểm đo<input value={pointSearchDraft} maxLength={100} onChange={event => setPointSearchDraft(event.target.value)} /></label><button type="button" className="button button-secondary" onClick={() => { setPointPage(1); setPointSearch(pointSearchDraft.trim()) }}>Tìm</button><button type="button" className="button button-secondary" disabled={pointPage <= 1} onClick={() => setPointPage(value => Math.max(1, value - 1))}>Trang trước</button><span>{`Trang ${pointPage} / ${totalPointPages} · ${options.scopedCount ?? 0} điểm`}</span><button type="button" className="button button-secondary" disabled={pointPage >= totalPointPages} onClick={() => setPointPage(value => value + 1)}>Trang sau</button></div>}
      </article>
      {selected && <div className="telemetry-refresh" role="group" aria-label="Bộ điều khiển làm mới"><label><input type="checkbox" checked={autoRefresh} onChange={event => setAutoRefresh(event.target.checked)} /> Tự động làm mới mỗi 10 giây</label><button type="button" className="button button-secondary" disabled={refreshing} onClick={() => refreshCoordinator.current?.refresh()}>Làm mới ngay</button>{refreshing && <span role="status">Đang làm mới…</span>}</div>}
      {errorState && renderError(errorState)}
      {showingStaleSnapshot && <RetryState message="Đang hiển thị bằng chứng nhận được gần nhất; lần làm mới mới nhất chưa thành công." onRetry={() => refreshCoordinator.current?.refresh()} />}
      {!selected && <EmptyState title="Chưa chọn điểm đo" message="Chọn đầy đủ Site, Area, Asset và điểm đo để xem dữ liệu mới nhất." />}
      {selected && !hasUsableSnapshot && !errorState && <LoadingState message="Đang tải dữ liệu mới nhất và sức khỏe nguồn…" />}
      {selected && hasUsableSnapshot && <>
        <div className="telemetry-evidence-grid">
          <article className="card latest-card"><div className="card-header"><div><p className="card-kicker">Điểm đo {snapshot.pointCode ?? selected.pointId}</p><h2>{snapshot.pointName ?? 'Quan sát mới nhất'}</h2></div><OperationalStatusBadge status={hasData ? 'Available' : 'Missing'} /></div>
            <p className="muted">Chỉ số: {snapshot.metric ?? 'Chưa có trong contract'} · Đơn vị: {snapshot.unit ?? '—'}</p>
            <div className="latest-value"><strong>{hasData ? snapshot.value : 'No Data'}</strong><span>{hasData ? snapshot.unit ?? 'value' : ''}</span></div>
            <div className="evidence-status-row"><DataQualityIndicator quality={qualityOf(snapshot.quality, hasData) ?? 'Missing'} reason={snapshot.reason} /><FreshnessIndicator freshness={freshnessOf(snapshot)} lastRefresh={snapshot.lastRefreshAt} /></div>
            <dl className="readiness-list"><div><dt>Thời điểm nguồn</dt><dd>{snapshot.sourceTimestamp ?? 'Chưa có'}</dd></div><div><dt>Thời điểm nhận</dt><dd>{snapshot.receivedTimestamp ?? 'Chưa có'}</dd></div><div><dt>Lần truy vấn</dt><dd>{snapshot.lastRefreshAt ?? 'Chưa có'}</dd></div></dl>
          </article>
          <article className="card"><p className="card-kicker">Sức khỏe nguồn / lượt chạy</p><div className="evidence-status-row"><OperationalStatusBadge status={operationalStatusOf(snapshot.health)} detail={snapshot.health} /><span className="metadata">Nguồn: {snapshot.source?.name ?? 'Chưa xác định'}</span></div><dl className="readiness-list"><div><dt>Mã lượt chạy</dt><dd>{snapshot.runId ?? '—'}</dd></div><div><dt>Trạng thái lượt chạy</dt><dd>{snapshot.runStatus ?? '—'}</dd></div><div><dt>Đã tạo / chấp nhận / từ chối</dt><dd>{snapshot.generated ?? '—'} / {snapshot.accepted ?? '—'} / {snapshot.rejected ?? '—'}</dd></div><div><dt>Lần sản xuất cuối</dt><dd>{snapshot.lastProductionAtUtc ?? '—'}</dd></div><div><dt>Ngưỡng không có dữ liệu</dt><dd>{snapshot.noDataAfterSeconds ?? 'Chưa có' }s</dd></div></dl></article>
        </div>
        <FeedbackBanner tone="info" title="Phạm vi bằng chứng" message="Coverage và chuỗi lịch sử chưa được cung cấp bởi contract hiện tại; không suy diễn thành 0 hoặc dữ liệu lịch sử." live={false} />
        <ChartContainer title="Lịch sử điểm đo" description="Chỉ hiển thị khi contract cung cấp chuỗi thời gian có timestamp." points={[]} unavailableReason="Historical series chưa có trong contract hiện tại." />
      </>}
    </section>
  )
}
