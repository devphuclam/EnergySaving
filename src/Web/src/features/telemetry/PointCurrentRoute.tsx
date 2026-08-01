import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { LatestSnapshot, TelemetryOptionSnapshot, TelemetrySelection } from '../../gateways/webGateways'

type RequestedSelection = Partial<TelemetrySelection>
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

function failureMessage(state: LatestSnapshot['state']) {
  if (state === 'dependency') return 'Dịch vụ dữ liệu đo tạm thời không sẵn sàng.'
  if (state === 'runtime-error') return 'Không thể kết nối đến dịch vụ dữ liệu đo.'
  if (state === 'forbidden') return 'Bạn không có quyền xem lựa chọn này.'
  if (state === 'not-found') return 'Không tìm thấy lựa chọn hợp lệ.'
  if (state === 'validation' || state === 'conflict') return 'Hierarchy đã chọn không hợp lệ.'
  return 'Không thể tải dữ liệu.'
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
  const [refreshNonce, setRefreshNonce] = useState(0)
  const [refreshing, setRefreshing] = useState(false)
  const [lastError, setLastError] = useState<LatestSnapshot['state'] | undefined>()
  const requestSequence = useRef(0)
  const optionRequestSequences = useRef<Record<OptionLevel, number>>({ sites: 0, areas: 0, assets: 0, points: 0 })

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
      return { ...previous, state: 'ready', points: next.points, scopedCount: next.scopedCount, page: next.page, pageSize: next.pageSize, errorCode: undefined }
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
    }, pointPage, pointSearch)
  }, [selection.siteId, selection.areaId, selection.assetId, pointPage, pointSearch, loadLevel])

  const selected = useMemo(() => selection.siteId && selection.areaId && selection.assetId && selection.pointId ? selection as TelemetrySelection : undefined, [selection])
  const key = selectionKey(selection)
  const totalPointPages = Math.max(1, Math.ceil((options.scopedCount ?? 0) / (options.pageSize ?? 100)))

  useEffect(() => {
    if (!selected) { setSnapshot(emptySnapshot); setLastError(undefined); setRefreshing(false); return }
    let active = true
    const sequence = ++requestSequence.current
    let timer: number | undefined
    const refresh = async () => {
      setRefreshing(true)
      try {
        const next = await gateways.latest.getSnapshot(selected)
        if (active && requestSequence.current === sequence) {
          const failed = ['dependency', 'runtime-error', 'forbidden', 'expired', 'not-found', 'validation', 'conflict', 'error'].includes(next.state)
          if (failed) { setLastError(next.state); if (!['dependency', 'runtime-error', 'error'].includes(next.state)) setSnapshot(emptySnapshot) }
          else { setLastError(undefined); setSnapshot(next) }
          if (autoRefresh) timer = window.setTimeout(() => { void refresh() }, 10_000)
        }
      } catch {
        if (active && requestSequence.current === sequence) { setLastError('runtime-error'); if (autoRefresh) timer = window.setTimeout(() => { void refresh() }, 10_000) }
      } finally { if (active && requestSequence.current === sequence) setRefreshing(false) }
    }
    void refresh()
    return () => { active = false; setRefreshing(false); if (timer !== undefined) window.clearTimeout(timer) }
  }, [gateways.latest, selected, key, autoRefresh, refreshNonce])

  const clearSelectionSnapshot = () => {
    requestSequence.current++
    setSnapshot(emptySnapshot)
    setLastError(undefined)
    setRefreshing(false)
  }
  const resetPoints = () => { setPointPage(1); setPointSearch(''); setPointSearchDraft(''); setOptions(previous => ({ ...previous, points: [], scopedCount: 0 })) }
  const changeSite = (siteId: string) => { const next = { siteId: siteId || undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next); setOptions(previous => ({ ...previous, areas: [], assets: [] })); resetPoints() }
  const changeArea = (areaId: string) => { const next = { ...selection, areaId: areaId || undefined, assetId: undefined, pointId: undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next); setOptions(previous => ({ ...previous, assets: [] })); resetPoints() }
  const changeAsset = (assetId: string) => { const next = { ...selection, assetId: assetId || undefined, pointId: undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next); resetPoints() }
  const changePoint = (pointId: string) => { const next = { ...selection, pointId: pointId || undefined }; clearSelectionSnapshot(); setSelection(next); writeSelection(next) }

  const hasData = snapshot.state === 'ready' && snapshot.dataState === 'Data' && snapshot.value !== null
  const hasUsableSnapshot = snapshot.state === 'ready' || snapshot.state === 'no-data'
  const errorState = lastError ?? (!hasUsableSnapshot && snapshot.state !== 'no-selection' ? snapshot.state : undefined)
  const showingStaleSnapshot = hasUsableSnapshot && Boolean(lastError)

  return (
    <section className="page" aria-labelledby="telemetry-title">
      <div className="page-heading"><div><p className="eyebrow">Giám sát đo lường</p><h1 id="telemetry-title">Dữ liệu mới nhất & sức khỏe nguồn</h1><p className="lede">Chọn rõ Site, Area, Asset và điểm đo để xem dữ liệu.</p></div><span className="badge badge-neutral">{autoRefresh ? 'Tự động 10 giây' : 'Tự động đã tắt'}</span></div>
      <article className="card" aria-label="Bộ chọn phân cấp đo lường"><div className="card-header"><div><p className="card-kicker">Phạm vi được cấp quyền</p><h2>Chọn phân cấp</h2></div><button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Tải lại lựa chọn</button></div>
        {options.state === 'loading' && <p role="status">Đang tải lựa chọn…</p>}
        {options.state !== 'loading' && options.state !== 'ready' && <div className="feedback feedback-error" role="alert"><p>{failureMessage(options.state)}</p><button type="button" className="button button-secondary" onClick={() => void loadOptions()}>Thử lại</button></div>}
        {options.state === 'ready' && <div className="selector-grid">
          <label>Site<select value={selection.siteId ?? ''} onChange={event => changeSite(event.target.value)}><option value="">Chọn Site</option>{options.sites.map(site => <option key={site.siteId} value={site.siteId}>{site.code} — {site.name}</option>)}</select></label>
          <label>Area<select value={selection.areaId ?? ''} disabled={!selection.siteId} onChange={event => changeArea(event.target.value)}><option value="">{selection.siteId ? 'Chọn Area' : 'Chọn Site trước'}</option>{options.areas.map(area => <option key={area.areaId} value={area.areaId}>{area.code} — {area.name}</option>)}</select></label>
          <label>Asset<select value={selection.assetId ?? ''} disabled={!selection.areaId} onChange={event => changeAsset(event.target.value)}><option value="">{selection.areaId ? 'Chọn Asset' : 'Chọn Area trước'}</option>{options.assets.map(asset => <option key={asset.assetId} value={asset.assetId}>{asset.code} — {asset.name}</option>)}</select></label>
          <label>Điểm đo<select value={selection.pointId ?? ''} disabled={!selection.assetId} onChange={event => changePoint(event.target.value)}><option value="">{selection.assetId ? 'Chọn điểm đo' : 'Chọn Asset trước'}</option>{options.points.map(point => <option key={point.pointId} value={point.pointId}>{point.code} — {point.name}</option>)}</select></label>
        </div>}
        {options.state === 'ready' && selection.assetId && <div className="toolbar" role="group" aria-label="Tìm và phân trang điểm đo"><label>Tìm điểm đo<input value={pointSearchDraft} maxLength={100} onChange={event => setPointSearchDraft(event.target.value)} /></label><button type="button" className="button button-secondary" onClick={() => { setPointPage(1); setPointSearch(pointSearchDraft.trim()) }}>Tìm</button><button type="button" className="button button-secondary" disabled={pointPage <= 1} onClick={() => setPointPage(value => Math.max(1, value - 1))}>Trang trước</button><span>{`Trang ${pointPage} / ${totalPointPages} · ${options.scopedCount ?? 0} điểm`}</span><button type="button" className="button button-secondary" disabled={pointPage >= totalPointPages} onClick={() => setPointPage(value => value + 1)}>Trang sau</button></div>}
        {options.state === 'ready' && options.sites.length === 0 && <p className="muted">Chưa có hierarchy được cấp quyền.</p>}
      </article>
      {selected && <div className="toolbar" role="group" aria-label="Bộ điều khiển làm mới"><label><input type="checkbox" checked={autoRefresh} onChange={event => setAutoRefresh(event.target.checked)} /> Tự động làm mới mỗi 10 giây</label><button type="button" className="button button-secondary" disabled={refreshing} onClick={() => setRefreshNonce(value => value + 1)}>Làm mới ngay</button>{refreshing && <span role="status">Đang làm mới…</span>}</div>}
      {errorState && <div className="feedback feedback-error" role="alert"><p>{failureMessage(errorState)}</p><button type="button" className="button button-secondary" disabled={refreshing} onClick={() => setRefreshNonce(value => value + 1)}>Thử lại</button></div>}
      {showingStaleSnapshot && <p className="feedback feedback-info" role="status">Dữ liệu lần cuối vẫn được giữ trong khi chờ kết nối phục hồi.</p>}
      {!selected && <div className="feedback feedback-info" role="status"><p>Chưa chọn điểm đo.</p><p className="muted">Không có điểm đo nào được tự động chọn.</p></div>}
      {selected && !hasUsableSnapshot && !errorState && <p role="status">Đang tải dữ liệu mới nhất và sức khỏe nguồn…</p>}
      {selected && hasUsableSnapshot && <div className="card-grid two-up"><article className="card latest-card"><div className="card-header"><div><p className="card-kicker">Điểm đo {snapshot.pointCode ?? selected.pointId}</p><h2>{snapshot.pointName ?? 'Quan sát mới nhất'}</h2></div><span className="badge badge-neutral">{snapshot.dataState ?? snapshot.state}</span></div><p className="muted">Chỉ số: {snapshot.metric ?? '—'} · Đơn vị: {snapshot.unit ?? '—'}</p><div className="latest-value"><strong>{hasData ? snapshot.value : 'Chưa có dữ liệu'}</strong><span>{hasData ? snapshot.unit ?? 'value' : ''}</span></div><p className="muted">Chất lượng: {snapshot.quality ?? '—'} · {snapshot.reason ?? (hasData ? 'Accepted' : 'NO_DATA')}</p><dl className="readiness-list"><div><dt>Thời điểm nguồn</dt><dd>{snapshot.sourceTimestamp ?? '—'}</dd></div><div><dt>Thời điểm nhận</dt><dd>{snapshot.receivedTimestamp ?? '—'}</dd></div><div><dt>Lần làm mới gần nhất</dt><dd>{snapshot.lastRefreshAt ?? '—'}</dd></div></dl></article><article className="card"><p className="card-kicker">Sức khỏe nguồn / Lượt chạy</p><h2>{snapshot.health}</h2><p className="muted">Nguồn: {snapshot.source?.name ?? '—'}</p><dl className="readiness-list"><div><dt>Mã lượt chạy</dt><dd>{snapshot.runId ?? '—'}</dd></div><div><dt>Trạng thái lượt chạy</dt><dd>{snapshot.runStatus ?? '—'}</dd></div><div><dt>Đã tạo</dt><dd>{snapshot.generated ?? '—'}</dd></div><div><dt>Đã chấp nhận / Từ chối</dt><dd>{snapshot.accepted ?? '—'} / {snapshot.rejected ?? '—'}</dd></div><div><dt>Lần sản xuất cuối</dt><dd>{snapshot.lastProductionAtUtc ?? '—'}</dd></div></dl></article></div>}
      {selected && hasUsableSnapshot && <p className="muted">Khoảng thời gian: {snapshot.expectedIntervalSeconds ?? '—'}s · Không có dữ liệu sau: {snapshot.noDataAfterSeconds ?? '—'}s</p>}
    </section>
  )
}
