import { useEffect, useMemo, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { SimulatorSelection, SimulatorSelectionOption, SimulatorSnapshot } from '../../gateways/webGateways'

type SelectionDraft = {
  siteId?: string
  areaId?: string
  assetId?: string
  sourceId?: string
  configurationId?: string
  configurationVersion?: number
}

const empty: SimulatorSnapshot = {
  state: 'loading', status: 'Stopped', generated: 0, accepted: 0, rejected: 0,
  options: [], history: [], historyTotal: 0,
}

function completeSelection(draft: SelectionDraft): SimulatorSelection | undefined {
  if (!draft.siteId || !draft.sourceId || !draft.configurationId || !draft.configurationVersion) return undefined
  return {
    siteId: draft.siteId,
    areaId: draft.areaId,
    assetId: draft.assetId,
    sourceId: draft.sourceId,
    configurationId: draft.configurationId,
    configurationVersion: draft.configurationVersion,
  }
}

function selectionFromUrl(search: string): SimulatorSelection | undefined {
  const query = new URLSearchParams(search)
  const configurationVersion = Number(query.get('configurationVersion'))
  const draft: SelectionDraft = {
    siteId: query.get('siteId') ?? undefined,
    areaId: query.get('areaId') ?? undefined,
    assetId: query.get('assetId') ?? undefined,
    sourceId: query.get('sourceId') ?? undefined,
    configurationId: query.get('configurationId') ?? undefined,
    configurationVersion: Number.isInteger(configurationVersion) && configurationVersion > 0
      ? configurationVersion : undefined,
  }
  return completeSelection(draft)
}

function writeSelectionUrl(selection?: SimulatorSelection): void {
  const url = new URL(window.location.href)
  for (const field of ['siteId', 'areaId', 'assetId', 'sourceId', 'configurationId', 'configurationVersion'])
    url.searchParams.delete(field)
  if (selection) {
    url.searchParams.set('siteId', selection.siteId)
    if (selection.areaId) url.searchParams.set('areaId', selection.areaId)
    if (selection.assetId) url.searchParams.set('assetId', selection.assetId)
    url.searchParams.set('sourceId', selection.sourceId)
    url.searchParams.set('configurationId', selection.configurationId)
    url.searchParams.set('configurationVersion', String(selection.configurationVersion))
  }
  window.history.replaceState(window.history.state, '', `${url.pathname}${url.search}${url.hash}`)
}

function uniqueBy<T>(items: T[], key: (item: T) => string): T[] {
  const seen = new Set<string>()
  return items.filter(item => {
    const value = key(item)
    if (seen.has(value)) return false
    seen.add(value)
    return true
  })
}

function formatTime(value?: string | null): string {
  if (!value) return 'Chưa có'
  return new Date(value).toLocaleString('vi-VN')
}

function optionMatchesHierarchy(option: SimulatorSelectionOption, draft: SelectionDraft): boolean {
  return (!draft.siteId || option.siteId === draft.siteId) &&
    (!draft.areaId || option.areaId === draft.areaId) &&
    (!draft.assetId || option.assetId === draft.assetId)
}

export function SimulatorRoute() {
  const gateways = useWebGateways()
  const [snapshot, setSnapshot] = useState<SimulatorSnapshot>(empty)
  const [draft, setDraft] = useState<SelectionDraft>(() => selectionFromUrl(window.location.search) ?? {})
  const [selection, setSelection] = useState<SimulatorSelection | undefined>(() => selectionFromUrl(window.location.search))
  const [feedback, setFeedback] = useState<string | undefined>()
  const [submitting, setSubmitting] = useState(false)
  const [lastOperation, setLastOperation] = useState<'start' | 'pause' | 'resume' | 'stop' | undefined>()

  async function loadSnapshot(requested?: SimulatorSelection) {
    setSnapshot(previous => ({ ...empty, options: previous.options, state: 'loading' }))
    const next = await gateways.simulator.getSnapshot(requested)
    setSnapshot(next)
    return next
  }

  useEffect(() => {
    let active = true
    const requested = selectionFromUrl(window.location.search)
    void gateways.simulator.getSnapshot(requested).then(value => { if (active) setSnapshot(value) })
    const onPopState = () => {
      const next = selectionFromUrl(window.location.search)
      setDraft(next ? { ...next, areaId: next.areaId ?? undefined, assetId: next.assetId ?? undefined } : {})
      setSelection(next)
      gateways.simulator.clearPendingMutation()
      void gateways.simulator.getSnapshot(next).then(value => { if (active) setSnapshot(value) })
    }
    window.addEventListener('popstate', onPopState)
    return () => { active = false; window.removeEventListener('popstate', onPopState) }
  }, [gateways.simulator])

  const options = snapshot.options ?? empty.options!
  const siteOptions = useMemo(() => uniqueBy(options, option => option.siteId), [options])
  const areaOptions = useMemo(() => uniqueBy(
    options.filter(option => !draft.siteId || option.siteId === draft.siteId)
      .filter(option => option.areaId), option => option.areaId ?? ''), [options, draft.siteId])
  const assetOptions = useMemo(() => uniqueBy(
    options.filter(option => optionMatchesHierarchy(option, { siteId: draft.siteId, areaId: draft.areaId }))
      .filter(option => option.assetId), option => option.assetId ?? ''), [options, draft.siteId, draft.areaId])
  const sourceOptions = useMemo(() => uniqueBy(
    options.filter(option => optionMatchesHierarchy(option, draft)), option => option.sourceId), [options, draft])
  const configurationOptions = useMemo(() => uniqueBy(
    sourceOptions.filter(option => option.sourceId === draft.sourceId),
    option => `${option.configurationId}|${option.configurationVersion}`), [sourceOptions, draft.sourceId])
  const selectedOption = useMemo(
    () => selection && options.find(option => option.siteId === selection.siteId &&
      (!selection.areaId || option.areaId === selection.areaId) &&
      (!selection.assetId || option.assetId === selection.assetId) &&
      option.sourceId === selection.sourceId && option.configurationId === selection.configurationId &&
      option.configurationVersion === selection.configurationVersion), [options, selection],
  )
  const selected = Boolean(selection && selectedOption && (snapshot.state === 'ready' || snapshot.state === 'success'))
  const areaRequired = areaOptions.length > 0
  const assetRequired = assetOptions.length > 0
  const running = snapshot.status === 'Running'
  const paused = snapshot.status === 'Paused'

  function updateDraft(next: SelectionDraft) {
    const nextSelection = completeSelection(next)
    setDraft(next)
    setSelection(nextSelection)
    gateways.simulator.clearPendingMutation()
    writeSelectionUrl(nextSelection)
    setFeedback(undefined)
    if (nextSelection) void loadSnapshot(nextSelection)
    else setSnapshot(previous => ({ ...previous, state: 'no-selection', selection: undefined }))
  }

  function chooseSite(value: string) {
    updateDraft({ siteId: value || undefined })
  }

  function chooseArea(value: string) {
    updateDraft({ siteId: draft.siteId, areaId: value || undefined })
  }

  function chooseAsset(value: string) {
    updateDraft({ siteId: draft.siteId, areaId: draft.areaId, assetId: value || undefined })
  }

  function chooseSource(value: string) {
    updateDraft({ siteId: draft.siteId, areaId: draft.areaId, assetId: draft.assetId, sourceId: value || undefined })
  }

  function chooseConfiguration(value: string) {
    const option = configurationOptions.find(candidate =>
      `${candidate.configurationId}|${candidate.configurationVersion}` === value)
    updateDraft({
      siteId: draft.siteId, areaId: draft.areaId, assetId: draft.assetId,
      sourceId: draft.sourceId, configurationId: option?.configurationId,
      configurationVersion: option?.configurationVersion,
    })
  }

  async function mutate(operation: 'start' | 'pause' | 'resume' | 'stop') {
    if (!selection) {
      setFeedback('Hãy chọn đầy đủ Site, Area, Asset, Source và cấu hình trước khi điều khiển Simulator.')
      return
    }
    setLastOperation(operation)
    setSubmitting(true)
    try {
      const next = await gateways.simulator.mutate(operation, selection)
      setSnapshot(next)
      setFeedback(next.isReplay
        ? 'Yêu cầu đã được phát lại an toàn theo cùng Idempotency-Key.'
        : next.state === 'success' ? 'Thao tác đã được máy chủ xác nhận.' : undefined)
    } finally { setSubmitting(false) }
  }

  const notice = snapshot.state === 'loading'
    ? 'Đang tải các lựa chọn được cấp quyền…'
    : snapshot.state === 'no-selection'
      ? 'Chưa chọn ngữ cảnh vận hành. Hãy chọn từng cấp Site, Area, Asset, Source và cấu hình.'
      : snapshot.state === 'no-data'
        ? 'Không có Source/cấu hình đủ điều kiện trong phạm vi của bạn.'
        : snapshot.state === 'forbidden'
          ? 'Bạn không có quyền với ngữ cảnh Simulator này.'
          : snapshot.state === 'not-found'
            ? 'Ngữ cảnh hoặc Run không còn tồn tại trong phạm vi được cấp quyền.'
            : snapshot.state === 'validation'
              ? `Lựa chọn chưa hợp lệ (${snapshot.errorCode ?? 'VALIDATION_FAILED'}).`
              : snapshot.state === 'conflict'
                ? `Có xung đột phiên bản (${snapshot.errorCode ?? 'VERSION_CONFLICT'}). Hãy tải lại Run trước khi thử lại.`
                : snapshot.state === 'runtime-error' || snapshot.state === 'dependency'
                  ? `Không thể kết nối dịch vụ Simulator (${snapshot.errorCode ?? snapshot.state}).`
                  : undefined

  const retryLoading = snapshot.state === 'runtime-error' || snapshot.state === 'dependency' || snapshot.state === 'error'

  return (
    <section className="page" aria-labelledby="simulator-title">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Thu nhận dữ liệu</p>
          <h1 id="simulator-title">Điều khiển Simulator</h1>
          <p className="lede">Chọn tuần tự Site, Area, Asset, Source và phiên bản cấu hình trước khi vận hành.</p>
        </div>
        <span className={`badge ${running ? 'badge-success' : 'badge-neutral'}`}>{snapshot.state === 'ready' ? snapshot.status : snapshot.state}</span>
      </div>

      <div className="card simulator-card">
        <p className="field-label">Ngữ cảnh vận hành được máy chủ xác nhận</p>
        <div className="control-grid">
          <label className="field-label" htmlFor="simulator-site">Site
            <select id="simulator-site" value={draft.siteId ?? ''} onChange={event => chooseSite(event.target.value)} disabled={snapshot.state === 'loading'}>
              <option value="">— Chọn Site —</option>
              {siteOptions.map(option => <option key={option.siteId} value={option.siteId}>{option.siteCode} / {option.siteName}</option>)}
            </select>
          </label>
          <label className="field-label" htmlFor="simulator-area">Area
            <select id="simulator-area" value={draft.areaId ?? ''} onChange={event => chooseArea(event.target.value)} disabled={!draft.siteId || !areaRequired}>
              <option value="">{areaRequired ? '— Chọn Area —' : '— Không có Area —'}</option>
              {areaOptions.map(option => <option key={option.areaId} value={option.areaId ?? ''}>{option.areaCode} / {option.areaName}</option>)}
            </select>
          </label>
          <label className="field-label" htmlFor="simulator-asset">Asset
            <select id="simulator-asset" value={draft.assetId ?? ''} onChange={event => chooseAsset(event.target.value)} disabled={!draft.siteId || (areaRequired && !draft.areaId) || !assetRequired}>
              <option value="">{assetRequired ? '— Chọn Asset —' : '— Không có Asset —'}</option>
              {assetOptions.map(option => <option key={option.assetId} value={option.assetId ?? ''}>{option.assetCode} / {option.assetName}</option>)}
            </select>
          </label>
          <label className="field-label" htmlFor="simulator-source">Source
            <select id="simulator-source" value={draft.sourceId ?? ''} onChange={event => chooseSource(event.target.value)} disabled={!draft.siteId || (areaRequired && !draft.areaId) || (assetRequired && !draft.assetId)}>
              <option value="">— Chọn Source —</option>
              {sourceOptions.map(option => <option key={option.sourceId} value={option.sourceId}>{option.sourceCode} / {option.sourceName}</option>)}
            </select>
          </label>
          <label className="field-label" htmlFor="simulator-configuration">Cấu hình đang hoạt động
            <select id="simulator-configuration" value={draft.configurationId && draft.configurationVersion ? `${draft.configurationId}|${draft.configurationVersion}` : ''} onChange={event => chooseConfiguration(event.target.value)} disabled={!draft.sourceId}>
              <option value="">— Chọn phiên bản cấu hình —</option>
              {configurationOptions.map(option => <option key={`${option.configurationId}|${option.configurationVersion}`} value={`${option.configurationId}|${option.configurationVersion}`} disabled={!option.isEligible}>v{option.configurationVersion} · {option.intervalSeconds}s</option>)}
            </select>
          </label>
        </div>
        {selectedOption && <p className="muted">Đã chọn {selectedOption.siteName} · {selectedOption.sourceName} · cấu hình v{selectedOption.configurationVersion} · chu kỳ {selectedOption.intervalSeconds}s</p>}
      </div>

      {notice && <div className="notice notice-warning" role="status">{notice}</div>}
      {submitting && <div className="notice notice-info" role="status">Đang gửi thao tác đến máy chủ…</div>}
      {feedback && <div className="notice notice-info" role="status">{feedback}</div>}
      {retryLoading && <button className="button button-secondary" type="button" disabled={submitting} onClick={() => void loadSnapshot(selection)}>Thử tải lại workspace</button>}
      {(snapshot.state === 'runtime-error' || snapshot.state === 'dependency') && lastOperation && selection && <button className="button button-secondary" type="button" disabled={submitting} onClick={() => void mutate(lastOperation)}>Thử lại thao tác</button>}

      {selected && <div className="card simulator-card">
        <div className="simulator-status">
          <span className={`status-dot ${running ? 'online' : ''}`} aria-hidden="true" />
          <div>
            <p className="card-kicker">Trạng thái Run</p>
            <h2>{snapshot.status}</h2>
            <p className="muted">Run {snapshot.runId ?? '—'} · phiên bản {snapshot.version ?? '—'}</p>
          </div>
        </div>
        <div className="control-row">
          <button className="button button-primary" type="button" disabled={submitting || running || paused} onClick={() => void mutate('start')}>Bắt đầu</button>
          <button className="button button-secondary" type="button" disabled={submitting || !running} onClick={() => void mutate('pause')}>Tạm dừng</button>
          <button className="button button-secondary" type="button" disabled={submitting || !paused} onClick={() => void mutate('resume')}>Tiếp tục</button>
          <button className="button button-danger" type="button" disabled={submitting || (!running && !paused)} onClick={() => void mutate('stop')}>Dừng</button>
        </div>
      </div>}

      {selected && <div className="card-grid three-up">
        <article className="metric-card"><span>Đã tạo</span><strong>{snapshot.generated}</strong><small>lượt mô phỏng</small></article>
        <article className="metric-card"><span>Đã chấp nhận</span><strong>{snapshot.accepted}</strong><small>kết quả cuối</small></article>
        <article className="metric-card"><span>Bị từ chối</span><strong>{snapshot.rejected}</strong><small>kết quả cuối</small></article>
      </div>}

      {selected && <div className="card">
        <div className="page-heading"><div><p className="eyebrow">Lịch sử gần đây</p><h2>Run của ngữ cảnh đã chọn</h2></div><span className="muted">Tổng {snapshot.historyTotal ?? 0}</span></div>
        {(snapshot.history ?? []).length === 0 ? <p className="muted">Chưa có Run trong ngữ cảnh này.</p> : <div className="table-wrap"><table><thead><tr><th>Run ID</th><th>Trạng thái</th><th>Phiên bản</th><th>Bộ đếm</th><th>Sản xuất cuối</th><th>Chu kỳ</th></tr></thead><tbody>
          {(snapshot.history ?? []).map(item => <tr key={item.runId}><td>{item.runId}</td><td>{item.status}</td><td>{item.version}</td><td>{item.generated} / {item.accepted} / {item.rejected}</td><td>{formatTime(item.lastProductionAtUtc)}</td><td>{item.intervalSeconds}s</td></tr>)}
        </tbody></table></div>}
      </div>}

      <div className="notice notice-info">Không có Run nào tự khởi động. Mọi thay đổi dùng cùng Idempotency-Key khi cần retry và phiên bản lạc quan do máy chủ kiểm tra.</div>
    </section>
  )
}
