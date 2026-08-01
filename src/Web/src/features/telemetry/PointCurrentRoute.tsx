import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { LatestSnapshot, TelemetryOptionSnapshot, TelemetrySelection } from '../../gateways/webGateways'

type RequestedSelection = Partial<TelemetrySelection>

const emptySnapshot: LatestSnapshot = { state: 'no-selection', value: null, health: '\u0043h\u01b0a ch\u1ecd\u006e \u0111i\u1ec3m', dataState: 'NoSelection' }

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
  if (state === 'dependency') return '\u0044\u1ecbch v\u1ee5 d\u1eef li\u1ec7u \u0111o t\u1ea1m th\u1eddi kh\u00f4ng s\u1eb5n s\u00e0ng.'
  if (state === 'runtime-error') return '\u004b h\u00f4ng th\u1ec3 k\u1ebft n\u1ed1i \u0111\u1ebfn d\u1ecbch v\u1ee5 d\u1eef li\u1ec7u \u0111o.'.replace(/^\u004b /, '\u004b')
  if (state === 'forbidden') return 'B\u1ea1n kh\u00f4ng c\u00f3 quy\u1ec1n xem l\u1ef1a ch\u1ecdn n\u00e0y.'
  if (state === 'not-found') return 'Kh\u00f4ng t\u00ecm th\u1ea5y l\u1ef1a ch\u1ecdn h\u1ee3p l\u1ec7.'
  if (state === 'validation' || state === 'conflict') return 'Hierarchy \u0111\u00e3 ch\u1ecdn kh\u00f4ng h\u1ee3p l\u1ec7.'
  return 'Kh\u00f4ng th\u1ec3 t\u1ea3i d\u1eef li\u1ec7u.'
}

export function PointCurrentRoute() {
  const gateways = useWebGateways()
  const [selection, setSelection] = useState<RequestedSelection>(readSelection)
  const [options, setOptions] = useState<TelemetryOptionSnapshot>({ state: 'loading', sites: [], areas: [], assets: [], points: [] })
  const [snapshot, setSnapshot] = useState<LatestSnapshot>(emptySnapshot)
  const [autoRefresh, setAutoRefresh] = useState(true)
  const [refreshNonce, setRefreshNonce] = useState(0)
  const [refreshing, setRefreshing] = useState(false)
  const [lastError, setLastError] = useState<LatestSnapshot['state'] | undefined>()
  const requestSequence = useRef(0)

  const loadOptions = useCallback(() => {
    if (!gateways.latest.getOptions) { setOptions(previous => ({ ...previous, state: 'ready' })); return Promise.resolve() }
    setOptions(previous => ({ ...previous, state: 'loading' }))
    return gateways.latest.getOptions().then(setOptions)
  }, [gateways.latest])
  useEffect(() => { void loadOptions() }, [loadOptions])

  const selected = useMemo(() => selection.siteId && selection.areaId && selection.assetId && selection.pointId ? selection as TelemetrySelection : undefined, [selection])
  const key = selectionKey(selection)
  const areas = useMemo(() => options.areas.filter(area => area.siteId === selection.siteId), [options.areas, selection.siteId])
  const assets = useMemo(() => options.assets.filter(asset => asset.areaId === selection.areaId), [options.assets, selection.areaId])
  const points = useMemo(() => options.points.filter(point => point.assetId === selection.assetId), [options.points, selection.assetId])

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
          if (failed) {
            setLastError(next.state)
            if (!['dependency', 'runtime-error', 'error'].includes(next.state)) setSnapshot(emptySnapshot)
          }
          else { setLastError(undefined); setSnapshot(next) }
          if (autoRefresh) timer = window.setTimeout(() => { void refresh() }, 10_000)
        }
      } catch {
        if (active && requestSequence.current === sequence) {
          setLastError('runtime-error')
          if (autoRefresh) timer = window.setTimeout(() => { void refresh() }, 10_000)
        }
      } finally {
        if (active && requestSequence.current === sequence) setRefreshing(false)
      }
    }
    void refresh()
    return () => { active = false; setRefreshing(false); if (timer !== undefined) window.clearTimeout(timer) }
  }, [gateways.latest, selected, key, autoRefresh, refreshNonce])

  const changeSite = (siteId: string) => { const next = { siteId: siteId || undefined }; setSelection(next); writeSelection(next); setSnapshot(emptySnapshot); setLastError(undefined) }
  const changeArea = (areaId: string) => { const next = { ...selection, areaId: areaId || undefined, assetId: undefined, pointId: undefined }; setSelection(next); writeSelection(next); setSnapshot(emptySnapshot); setLastError(undefined) }
  const changeAsset = (assetId: string) => { const next = { ...selection, assetId: assetId || undefined, pointId: undefined }; setSelection(next); writeSelection(next); setSnapshot(emptySnapshot); setLastError(undefined) }
  const changePoint = (pointId: string) => { const next = { ...selection, pointId: pointId || undefined }; setSelection(next); writeSelection(next) }

  const hasData = snapshot.state === 'ready' && snapshot.dataState === 'Data' && snapshot.value !== null
  const hasUsableSnapshot = snapshot.state === 'ready' || snapshot.state === 'no-data'
  const errorState = lastError ?? (!hasUsableSnapshot && snapshot.state !== 'no-selection' ? snapshot.state : undefined)
  const showingStaleSnapshot = hasUsableSnapshot && Boolean(lastError)

  return (
    <section className="page" aria-labelledby="telemetry-title">
      <div className="page-heading"><div><p className="eyebrow">{'Gi\u00e1m s\u00e1t \u0111o l\u01b0\u1eddng'}</p><h1 id="telemetry-title">{'D\u1eef li\u1ec7u m\u1edbi nh\u1ea5t & s\u1ee9c kh\u1ecfe ngu\u1ed3n'}</h1><p className="lede">{'Ch\u1ecdn r\u00f5 Site, Area, Asset v\u00e0 \u0111i\u1ec3m \u0111o \u0111\u1ec3 xem d\u1eef li\u1ec7u.'}</p></div><span className="badge badge-neutral">{autoRefresh ? 'T\u1ef1 \u0111\u1ed9ng 10 gi\u00e2y' : 'T\u1ef1 \u0111\u1ed9ng \u0111\u00e3 t\u1eaft'}</span></div>
      <article className="card" aria-label="B\u1ed9 ch\u1ecdn ph\u00e2n c\u1ea5p \u0111o l\u01b0\u1eddng"><div className="card-header"><div><p className="card-kicker">{'Ph\u1ea1m vi \u0111\u01b0\u1ee3c c\u1ea5p quy\u1ec1n'}</p><h2>{'Ch\u1ecdn ph\u00e2n c\u1ea5p'}</h2></div><button type="button" className="button button-secondary" onClick={() => void loadOptions()}>{'T\u1ea3i l\u1ea1i l\u1ef1a ch\u1ecdn'}</button></div>
        {options.state === 'loading' && <p role="status">{'\u0110ang t\u1ea3i l\u1ef1a ch\u1ecdn\u2026'}</p>}
        {options.state !== 'loading' && options.state !== 'ready' && <div className="feedback feedback-error" role="alert"><p>{failureMessage(options.state)}</p><button type="button" className="button button-secondary" onClick={() => void loadOptions()}>{'Th\u1eed l\u1ea1i'}</button></div>}
        {options.state === 'ready' && <div className="selector-grid">
          <label>{'Site'}<select value={selection.siteId ?? ''} onChange={event => changeSite(event.target.value)}><option value="">{'Ch\u1ecdn Site'}</option>{options.sites.map(site => <option key={site.siteId} value={site.siteId}>{site.code} {'\u2014'} {site.name}</option>)}</select></label>
          <label>{'Area'}<select value={selection.areaId ?? ''} disabled={!selection.siteId} onChange={event => changeArea(event.target.value)}><option value="">{selection.siteId ? 'Ch\u1ecdn Area' : 'Ch\u1ecdn Site tr\u01b0\u1edbc'}</option>{areas.map(area => <option key={area.areaId} value={area.areaId}>{area.code} {'\u2014'} {area.name}</option>)}</select></label>
          <label>{'Asset'}<select value={selection.assetId ?? ''} disabled={!selection.areaId} onChange={event => changeAsset(event.target.value)}><option value="">{selection.areaId ? 'Ch\u1ecdn Asset' : 'Ch\u1ecdn Area tr\u01b0\u1edbc'}</option>{assets.map(asset => <option key={asset.assetId} value={asset.assetId}>{asset.code} {'\u2014'} {asset.name}</option>)}</select></label>
          <label>{'\u0110i\u1ec3m \u0111o'}<select value={selection.pointId ?? ''} disabled={!selection.assetId} onChange={event => changePoint(event.target.value)}><option value="">{selection.assetId ? 'Ch\u1ecdn \u0111i\u1ec3m \u0111o' : 'Ch\u1ecdn Asset tr\u01b0\u1edbc'}</option>{points.map(point => <option key={point.pointId} value={point.pointId}>{point.code} {'\u2014'} {point.name}</option>)}</select></label>
        </div>}
        {options.state === 'ready' && options.sites.length === 0 && <p className="muted">{'Ch\u01b0a c\u00f3 hierarchy \u0111\u01b0\u1ee3c c\u1ea5p quy\u1ec1n.'}</p>}
      </article>
      {selected && <div className="toolbar" role="group" aria-label="B\u1ed9 \u0111i\u1ec1u khi\u1ec3n l\u00e0m m\u1edbi"><label><input type="checkbox" checked={autoRefresh} onChange={event => setAutoRefresh(event.target.checked)} /> {'T\u1ef1 \u0111\u1ed9ng l\u00e0m m\u1edbi m\u1ed7i 10 gi\u00e2y'}</label><button type="button" className="button button-secondary" disabled={refreshing} onClick={() => setRefreshNonce(value => value + 1)}>{'L\u00e0m m\u1edbi ngay'}</button>{refreshing && <span role="status">{'\u0110ang l\u00e0m m\u1edbi\u2026'}</span>}</div>}
      {errorState && <div className="feedback feedback-error" role="alert"><p>{failureMessage(errorState)}</p><button type="button" className="button button-secondary" disabled={refreshing} onClick={() => setRefreshNonce(value => value + 1)}>{'Th\u1eed l\u1ea1i'}</button></div>}
      {showingStaleSnapshot && <p className="feedback feedback-info" role="status">{'D\u1eef li\u1ec7u l\u1ea7n cu\u1ed1i v\u1eabn \u0111\u01b0\u1ee3c gi\u1eef trong khi ch\u1edd k\u1ebft n\u1ed1i ph\u1ee5c h\u1ed3i.'}</p>}
      {!selected && <div className="feedback feedback-info" role="status"><p>{'Ch\u01b0a ch\u1ecdn \u0111i\u1ec3m \u0111o.'}</p><p className="muted">{'Kh\u00f4ng c\u00f3 \u0111i\u1ec3m \u0111o n\u00e0o \u0111\u01b0\u1ee3c t\u1ef1 \u0111\u1ed9ng ch\u1ecdn.'}</p></div>}
      {selected && !hasUsableSnapshot && !errorState && <p role="status">{'\u0110ang t\u1ea3i d\u1eef li\u1ec7u m\u1edbi nh\u1ea5t v\u00e0 s\u1ee9c kh\u1ecfe ngu\u1ed3n\u2026'}</p>}
      {selected && hasUsableSnapshot && <div className="card-grid two-up"><article className="card latest-card"><div className="card-header"><div><p className="card-kicker">{'\u0110i\u1ec3m \u0111o'} {snapshot.pointCode ?? selected.pointId}</p><h2>{snapshot.pointName ?? 'Quan s\u00e1t m\u1edbi nh\u1ea5t'}</h2></div><span className="badge badge-neutral">{snapshot.dataState ?? snapshot.state}</span></div><p className="muted">{'Ch\u1ec9 s\u1ed1: '}{snapshot.metric ?? '\u2014'} {'\u00b7'} {'\u0110\u01a1n v\u1ecb: '}{snapshot.unit ?? '\u2014'}</p><div className="latest-value"><strong>{hasData ? snapshot.value : 'Ch\u01b0a c\u00f3 d\u1eef li\u1ec7u'}</strong><span>{hasData ? snapshot.unit ?? 'value' : ''}</span></div><p className="muted">{'Ch\u1ea5t l\u01b0\u1ee3ng: '}{snapshot.quality ?? '\u2014'} {'\u00b7'} {snapshot.reason ?? (hasData ? 'Accepted' : 'NO_DATA')}</p><dl className="readiness-list"><div><dt>{'Th\u1eddi \u0111i\u1ec3m ngu\u1ed3n'}</dt><dd>{snapshot.sourceTimestamp ?? '\u2014'}</dd></div><div><dt>{'Th\u1eddi \u0111i\u1ec3m nh\u1eadn'}</dt><dd>{snapshot.receivedTimestamp ?? '\u2014'}</dd></div><div><dt>{'L\u1ea7n l\u00e0m m\u1edbi g\u1ea7n nh\u1ea5t'}</dt><dd>{snapshot.lastRefreshAt ?? '\u2014'}</dd></div></dl></article><article className="card"><p className="card-kicker">{'S\u1ee9c kh\u1ecfe ngu\u1ed3n / L\u01b0\u1ee3t ch\u1ea1y'}</p><h2>{snapshot.health}</h2><p className="muted">{'Ngu\u1ed3n: '}{snapshot.source?.name ?? '\u2014'}</p><dl className="readiness-list"><div><dt>{'M\u00e3 l\u01b0\u1ee3t ch\u1ea1y'}</dt><dd>{snapshot.runId ?? '\u2014'}</dd></div><div><dt>{'Tr\u1ea1ng th\u00e1i l\u01b0\u1ee3t ch\u1ea1y'}</dt><dd>{snapshot.runStatus ?? '\u2014'}</dd></div><div><dt>{'\u0110\u00e3 t\u1ea1o'}</dt><dd>{snapshot.generated ?? '\u2014'}</dd></div><div><dt>{'\u0110\u00e3 ch\u1ea5p nh\u1eadn / T\u1eeb ch\u1ed1i'}</dt><dd>{snapshot.accepted ?? '\u2014'} / {snapshot.rejected ?? '\u2014'}</dd></div><div><dt>{'L\u1ea7n s\u1ea3n xu\u1ea5t cu\u1ed1i'}</dt><dd>{snapshot.lastProductionAtUtc ?? '\u2014'}</dd></div></dl></article></div>}
      {selected && hasUsableSnapshot && <p className="muted">{'Kho\u1ea3ng th\u1eddi gian: '}{snapshot.expectedIntervalSeconds ?? '\u2014'}s {'\u00b7'} {'Kh\u00f4ng c\u00f3 d\u1eef li\u1ec7u sau: '}{snapshot.noDataAfterSeconds ?? '\u2014'}s</p>}
    </section>
  )
}
