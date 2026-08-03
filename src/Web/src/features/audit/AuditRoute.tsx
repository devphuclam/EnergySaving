import { useEffect, useState, type FormEvent } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { AuditQueryFilters, AuditSnapshot } from '../../gateways/webGateways'

const empty: AuditSnapshot = { state: 'loading', eventCount: 0, records: [] }
const initialFilters: AuditQueryFilters = {}

function safeAuditValue(value: unknown): string {
  if (value === undefined || value === null) return '—'
  const serialized = JSON.stringify(value, (key, nested) =>
    /password|secret|token|credential|connectionstring|privatekey/i.test(key) ? '[REDACTED]' : nested)
  return (serialized ?? '—').replace(/(password|secret|token|credential|connectionstring|privatekey)\s*[:=]\s*[^,;"\s]+/gi, '$1=[REDACTED]')
}

function isValidDateRange(filters: AuditQueryFilters): boolean {
  if (!filters.fromUtc && !filters.toUtc) return true
  const from = filters.fromUtc ? Date.parse(filters.fromUtc) : Number.NEGATIVE_INFINITY
  const to = filters.toUtc ? Date.parse(filters.toUtc) : Number.POSITIVE_INFINITY
  return Number.isFinite(from) || Number.isFinite(to) ? from <= to : false
}

export function AuditRoute() {
  const gateways = useWebGateways()
  const [snapshot, setSnapshot] = useState(empty)
  const [draft, setDraft] = useState<AuditQueryFilters>(initialFilters)
  const [filters, setFilters] = useState<AuditQueryFilters>(initialFilters)
  const [validation, setValidation] = useState('')
  const [reloadToken, setReloadToken] = useState(0)
  const [loadingNext, setLoadingNext] = useState(false)

  useEffect(() => {
    let active = true
    setSnapshot(current => ({ ...current, state: 'loading' }))
    void gateways.audit.getSnapshot(filters).then(value => { if (active) setSnapshot(value) })
    return () => { active = false }
  }, [gateways.audit, filters, reloadToken])

  function update(key: keyof AuditQueryFilters, value: string) {
    setDraft(current => ({ ...current, [key]: value || undefined }))
  }

  function applyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!isValidDateRange(draft)) {
      setValidation('Khoảng thời gian không hợp lệ: From phải nhỏ hơn hoặc bằng To.')
      return
    }
    setValidation('')
    setFilters({ ...draft })
  }

  async function nextPage() {
    if (!snapshot.nextCursor || loadingNext) return
    setLoadingNext(true)
    try { setSnapshot(await gateways.audit.getSnapshot(filters, snapshot.nextCursor)) }
    finally { setLoadingNext(false) }
  }

  const records = snapshot.records ?? []
  const stateMessage = snapshot.state === 'ready'
    ? records.length === 0 ? 'Không có Audit trong phạm vi hoặc bộ lọc hiện tại.' : ''
    : snapshot.state === 'forbidden' ? 'Audit bị giới hạn theo quyền và phạm vi được cấp.'
      : snapshot.state === 'expired' ? 'Phiên làm việc đã hết hạn. Hãy đăng nhập lại.'
        : snapshot.state === 'dependency' ? 'Audit dependency không khả dụng. Không hiển thị dữ liệu giả.'
          : snapshot.state === 'runtime-error' || snapshot.state === 'error' ? 'Không thể tải Audit do lỗi runtime.'
            : snapshot.state === 'no-scope' ? 'Tài khoản chưa có phạm vi Audit được cấp.' : 'Đang tải Audit…'

  return <section className="page" aria-labelledby="audit-title">
    <div className="page-heading"><div><p className="eyebrow">Bằng chứng</p><h1 id="audit-title">Rà soát nhật ký</h1><p className="lede">Nhật ký bất biến cho các thay đổi cấu hình và hoạt động vận hành trong phạm vi được cấp.</p></div><span className="badge badge-neutral">AUDIT_READ</span></div>
    <section className="card" aria-labelledby="audit-filter-title">
      <div className="card-header"><div><p className="card-kicker">Bộ lọc phía máy chủ</p><h2 id="audit-filter-title">Tìm kiếm nhật ký</h2></div><span className="muted">Keyset · mới nhất trước · không OFFSET</span></div>
      <form className="filter-bar" onSubmit={applyFilters}>
        <label className="field">Từ UTC<input className="input" type="datetime-local" value={draft.fromUtc ?? ''} onChange={event => update('fromUtc', event.target.value)} /></label>
        <label className="field">Đến UTC<input className="input" type="datetime-local" value={draft.toUtc ?? ''} onChange={event => update('toUtc', event.target.value)} /></label>
        <label className="field">Người thực hiện<input className="input" value={draft.actorId ?? ''} onChange={event => update('actorId', event.target.value)} /></label>
        <label className="field">Hành động<input className="input" value={draft.action ?? ''} onChange={event => update('action', event.target.value)} /></label>
        <label className="field">Loại đối tượng<input className="input" value={draft.entityType ?? ''} onChange={event => update('entityType', event.target.value)} /></label>
        <label className="field">Mã đối tượng<input className="input" value={draft.entityId ?? ''} onChange={event => update('entityId', event.target.value)} /></label>
        <label className="field">Site<input className="input" value={draft.siteId ?? ''} onChange={event => update('siteId', event.target.value)} /></label>
        <label className="field">Area<input className="input" value={draft.areaId ?? ''} onChange={event => update('areaId', event.target.value)} /></label>
        <button className="button button-primary" type="submit">Áp dụng</button>
      </form>
      {validation && <div className="notice notice-warning" role="alert">{validation}</div>}
    </section>

    {snapshot.state !== 'ready' && snapshot.state !== 'loading' && <div className="notice notice-warning" role="alert">{stateMessage}<button className="button button-secondary" type="button" onClick={() => setReloadToken(value => value + 1)}>Thử lại</button></div>}
    {snapshot.state === 'loading' && <div className="notice notice-info" role="status">{stateMessage}</div>}
    {snapshot.state === 'ready' && <section className="card" aria-label="Audit results">
      <div className="card-header"><div><p className="card-kicker">Hoạt động gần đây</p><h2>{snapshot.eventCount} bản ghi trong trang</h2></div><span className="muted">Phạm vi trước trang · mã tương quan chỉ dành cho Quản trị viên</span></div>
      {records.length === 0 ? <div className="empty-state"><span className="empty-icon" aria-hidden="true">✓</span><p>{stateMessage}</p><small>Nhật ký xuất hiện sau khi dispatch, khử trùng lặp và append hoàn tất.</small></div> : <div className="table-scroll"><table className="data-table"><thead><tr><th>Người thực hiện</th><th>Thời gian</th><th>Đối tượng</th><th>Hành động</th><th>Tóm tắt</th><th>Trước</th><th>Sau</th><th>Mã tương quan</th></tr></thead><tbody>{records.map((record, index) => <tr key={`${record.time ?? 'event'}-${record.entityId ?? index}`}><td>{record.actor || '—'}</td><td>{record.time || '—'}</td><td>{record.objectType || record.object || '—'}<br /><small>{record.entityId || '—'}</small></td><td>{record.action || '—'}</td><td>{record.summary || '—'}</td><td>{safeAuditValue(record.before)}</td><td>{safeAuditValue(record.after)}</td><td>{record.correlationId || 'Ẩn theo quyền'}</td></tr>)}</tbody></table></div>}
      {snapshot.nextCursor && <div className="pagination"><button className="button button-secondary" type="button" disabled={loadingNext} onClick={() => void nextPage()}>{loadingNext ? 'Đang tải…' : 'Trang kế tiếp'}</button></div>}
    </section>}
  </section>
}
