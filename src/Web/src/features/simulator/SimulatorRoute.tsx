import { useEffect, useMemo, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { SimulatorSelection, SimulatorSelectionOption, SimulatorSnapshot } from '../../gateways/webGateways'

const empty: SimulatorSnapshot = {
  state: 'loading', status: 'Stopped', generated: 0, accepted: 0, rejected: 0,
  options: [], history: [], historyTotal: 0,
}

function selectionKey(selection?: SimulatorSelection): string {
  if (!selection) return ''
  return [selection.siteId, selection.areaId ?? '', selection.assetId ?? '', selection.sourceId,
    selection.configurationId, selection.configurationVersion].join('|')
}

function optionSelection(option: SimulatorSelectionOption): SimulatorSelection {
  return {
    siteId: option.siteId,
    areaId: option.areaId,
    assetId: option.assetId,
    sourceId: option.sourceId,
    configurationId: option.configurationId,
    configurationVersion: option.configurationVersion,
  }
}

function formatTime(value?: string | null): string {
  if (!value) return 'Chưa có'
  return new Date(value).toLocaleString('vi-VN')
}

export function SimulatorRoute() {
  const gateways = useWebGateways()
  const [snapshot, setSnapshot] = useState(empty)
  const [selection, setSelection] = useState<SimulatorSelection | undefined>()
  const [feedback, setFeedback] = useState<string | undefined>()
  const [submitting, setSubmitting] = useState(false)
  const [lastOperation, setLastOperation] = useState<'start' | 'pause' | 'resume' | 'stop' | undefined>()

  useEffect(() => {
    let active = true
    void gateways.simulator.getSnapshot().then(value => { if (active) setSnapshot(value) })
    return () => { active = false }
  }, [gateways.simulator])

  const options = snapshot.options ?? empty.options!
  const selectedKey = selectionKey(selection)
  const selectedOption = useMemo(
    () => options.find(option => selectionKey(optionSelection(option)) === selectedKey),
    [options, selectedKey],
  )
  const running = snapshot.status === 'Running'
  const paused = snapshot.status === 'Paused'
  const selected = Boolean(selectedOption && selection)

  function choose(value: string) {
    const option = options.find(candidate => selectionKey(optionSelection(candidate)) === value)
    setSelection(option ? optionSelection(option) : undefined)
    setFeedback(undefined)
    if (option) void gateways.simulator.getSnapshot(optionSelection(option)).then(setSnapshot)
  }

  async function mutate(operation: 'start' | 'pause' | 'resume' | 'stop') {
    if (!selection) {
      setFeedback('Hãy chọn Site, Area, Asset, Source và cấu hình trước khi điều khiển Simulator.')
      return
    }
    setLastOperation(operation)
    setSubmitting(true)
    try {
      const next = await gateways.simulator.mutate(operation, selection)
      setSnapshot(next)
      setFeedback(next.isReplay ? 'Yêu cầu đã được phát lại an toàn theo Idempotency-Key.' : next.state === 'success' ? 'Thao tác đã được máy chủ xác nhận.' : undefined)
    } finally { setSubmitting(false) }
  }

  const notice = snapshot.state === 'loading'
    ? 'Đang tải các lựa chọn được cấp quyền…'
    : snapshot.state === 'no-selection'
      ? 'Chưa chọn ngữ cảnh vận hành. Chọn một cấu hình để xem Run và bật điều khiển.'
      : snapshot.state === 'no-data'
        ? 'Không có Source/configuration đủ điều kiện trong phạm vi của bạn.'
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
                  : snapshot.state !== 'ready' && snapshot.state !== 'success'
                    ? `Không thể tải workspace Simulator (${snapshot.errorCode ?? snapshot.state}).`
          : undefined

  return (
    <section className="page" aria-labelledby="simulator-title">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Thu nhận dữ liệu</p>
          <h1 id="simulator-title">Điều khiển Simulator</h1>
          <p className="lede">Chọn rõ Site, Area, Asset, Source và phiên bản cấu hình trước khi vận hành.</p>
        </div>
        <span className={`badge ${running ? 'badge-success' : 'badge-neutral'}`}>{snapshot.state === 'ready' ? snapshot.status : snapshot.state}</span>
      </div>

      <div className="card simulator-card">
        <label className="field-label" htmlFor="simulator-selection">Ngữ cảnh Site / Area / Asset / Source / cấu hình</label>
        <select id="simulator-selection" value={selectedKey} onChange={event => choose(event.target.value)}>
          <option value="">— Chọn ngữ cảnh rõ ràng —</option>
          {options.map(option => {
            const value = selectionKey(optionSelection(option))
            return <option key={value} value={value} disabled={!option.isEligible}>
              {option.siteCode} / {option.areaCode ?? '—'} / {option.assetCode ?? '—'} / {option.sourceCode} / cấu hình v{option.configurationVersion}
            </option>
          })}
        </select>
        {selectedOption && <p className="muted">Đã chọn {selectedOption.siteName} · {selectedOption.sourceName} · cấu hình v{selectedOption.configurationVersion} · chu kỳ {selectedOption.intervalSeconds}s</p>}
      </div>

      {notice && <div className="notice notice-warning" role="status">{notice}</div>}
      {submitting && <div className="notice notice-info" role="status">Đang gửi thao tác đến máy chủ…</div>}
      {feedback && <div className="notice notice-info" role="status">{feedback}</div>}
      {(snapshot.state === 'runtime-error' || snapshot.state === 'dependency') && lastOperation && selection && <button className="button button-secondary" type="button" disabled={submitting} onClick={() => void mutate(lastOperation)}>Thử lại</button>}

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
          <button className="button button-primary" type="button" disabled={submitting || (running || paused)} onClick={() => void mutate('start')}>Bắt đầu</button>
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

      <div className="notice notice-info">Không có Run nào tự khởi động. Mọi thay đổi dùng Idempotency-Key và phiên bản lạc quan do máy chủ kiểm tra.</div>
    </section>
  )
}
