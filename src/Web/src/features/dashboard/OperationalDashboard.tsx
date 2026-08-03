import { useEffect, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { AuthSession, GatewayState, OperationalDashboardSnapshot } from '../../gateways/webGateways'

type DashboardRoute = 'setup' | 'configuration' | 'simulator' | 'telemetry' | 'audit'

const emptySnapshot: OperationalDashboardSnapshot = {
  state: 'loading',
  sites: { count: 0, items: [] },
  sources: { count: 0, items: [] },
  points: { count: 0, items: [] },
  runs: { count: 0, items: [] },
  latest: { count: 0, items: [] },
  health: { count: 0, items: [] },
  incompleteSetup: { count: 0 },
  recentAudit: { items: [] },
  runtime: { status: 'Unavailable', simulatorRunning: false },
  dependency: { status: 'Unavailable' },
}

const stateMessage: Record<GatewayState, string> = {
  loading: 'Đang tải bảng vận hành…', submitting: 'Đang xử lý…', success: 'Đã hoàn tất.', ready: 'Bảng vận hành đã sẵn sàng.',
  'invalid-credentials': 'Thông tin đăng nhập không hợp lệ.', forbidden: 'Bạn không có quyền xem dashboard này.', expired: 'Phiên làm việc đã hết hạn. Hãy đăng nhập lại.',
  'no-data': 'Chưa có dữ liệu hợp lệ.', 'no-selection': 'Chưa chọn đối tượng.', 'no-scope': 'Tài khoản chưa được cấp phạm vi Site hoặc Area.',
  validation: 'Bộ lọc hoặc dữ liệu chưa hợp lệ.', conflict: 'Dữ liệu đã thay đổi. Hãy tải lại.', 'not-found': 'Không tìm thấy dữ liệu.',
  dependency: 'Không thể truy cập thành phần phụ thuộc cần thiết.', 'runtime-error': 'Không thể tải bảng vận hành do lỗi runtime.', error: 'Không thể tải bảng vận hành.',
}

function itemLabel(item: Record<string, unknown>): string {
  return String(item.name ?? item.code ?? item.description ?? item.status ?? item.id ?? '—')
}

function SummaryCard({ title, count, items, route, onNavigate }: {
  title: string; count: number; items: Array<Record<string, unknown>>; route: DashboardRoute; onNavigate: (route: DashboardRoute) => void
}) {
  return <article className="metric-card" aria-label={`${title}: ${count}`}>
    <span>{title}</span><strong>{count}</strong>
    <small>{items.length > 0 ? items.slice(0, 3).map(itemLabel).join(' · ') : 'Chưa có bản ghi trong phạm vi'}</small>
    <button className="button button-secondary" type="button" onClick={() => onNavigate(route)}>Mở chi tiết</button>
  </article>
}

export function OperationalDashboard({ session, onNewSetup, onContinueSetup, onNavigate }: {
  session: AuthSession; onNewSetup: () => void; onContinueSetup: () => void; onNavigate: (route: DashboardRoute) => void
}) {
  const gateways = useWebGateways()
  const [snapshot, setSnapshot] = useState<OperationalDashboardSnapshot>(emptySnapshot)
  const [reloadToken, setReloadToken] = useState(0)

  useEffect(() => {
    let active = true
    setSnapshot(emptySnapshot)
    void gateways.dashboard.getSnapshot().then(value => { if (active) setSnapshot(value) })
    return () => { active = false }
  }, [gateways.dashboard, reloadToken])

  const state = snapshot.state
  const loading = state === 'loading'
  const noScope = state === 'no-scope'
  const showError = !loading && !noScope && state !== 'ready'

  const runtimeStatus = snapshot.runtime.status === 'Available' ? 'Sẵn sàng' : 'Không khả dụng'
  return <section className="page" aria-labelledby="dashboard-title">
    <div className="page-heading"><div><p className="eyebrow">Không gian vận hành</p><h1 id="dashboard-title">Bảng vận hành</h1><p className="lede">Tóm tắt có phạm vi của cấu hình, nguồn dữ liệu, điểm đo, lần chạy và tình trạng mới nhất.</p></div><span className="badge badge-neutral">{session.isAdministrator ? 'Quản trị viên' : 'Phạm vi được cấp'}</span></div>
    {loading && <div className="notice notice-info" role="status">{stateMessage.loading}</div>}
    {noScope && <div className="notice notice-warning" role="alert"><strong>Chưa có phạm vi được cấp.</strong><span>Bảng vận hành không hiển thị số liệu toàn cục. Hãy liên hệ Quản trị viên để được gán địa điểm hoặc khu vực.</span></div>}
    {showError && <div className="notice notice-warning" role="alert"><strong>{state === 'dependency' && snapshot.dependency.errorCode ? `Mã lỗi: ${snapshot.dependency.errorCode}` : 'Không thể hiển thị bảng vận hành'}</strong><span>{stateMessage[state]}</span><button className="button button-secondary" type="button" onClick={() => setReloadToken(value => value + 1)}>Thử lại</button></div>}
    {!loading && !noScope && !showError && <>
      <div className="card-grid three-up">
        <SummaryCard title="Địa điểm" count={snapshot.sites.count} items={snapshot.sites.items} route="configuration" onNavigate={onNavigate} />
        <SummaryCard title="Nguồn dữ liệu" count={snapshot.sources.count} items={snapshot.sources.items} route="configuration" onNavigate={onNavigate} />
        <SummaryCard title="Điểm đo" count={snapshot.points.count} items={snapshot.points.items} route="telemetry" onNavigate={onNavigate} />
        <SummaryCard title="Lần chạy đang hoạt động" count={snapshot.runs.count} items={snapshot.runs.items} route="simulator" onNavigate={onNavigate} />
        <SummaryCard title="Giá trị mới nhất" count={snapshot.latest.count} items={snapshot.latest.items} route="telemetry" onNavigate={onNavigate} />
        <SummaryCard title="Tình trạng nguồn" count={snapshot.health.count} items={snapshot.health.items} route="telemetry" onNavigate={onNavigate} />
      </div>
      <div className="card-grid two-up">
        <section className="card" aria-labelledby="dashboard-setup-title"><div className="card-header"><div><p className="card-kicker">Thiết lập</p><h2 id="dashboard-setup-title">Mức sẵn sàng kích hoạt</h2></div><span className="badge badge-neutral">{snapshot.incompleteSetup.count} chưa hoàn tất</span></div><p className="muted">{snapshot.incompleteSetup.count > 0 ? `Bước tiếp theo: ${snapshot.incompleteSetup.nextStep ?? 'Tiếp tục thiết lập'}.` : 'Mọi chuỗi trong phạm vi đã sẵn sàng hoặc chưa có dữ liệu.'}</p><div className="control-row">{snapshot.incompleteSetup.count > 0 && <button className="button button-primary" type="button" onClick={onContinueSetup}>Tiếp tục thiết lập</button>}{session.isAdministrator && <button className="button button-secondary" type="button" onClick={onNewSetup}>Tạo chuỗi cấu hình mới</button>}</div></section>
        <section className="card" aria-labelledby="dashboard-runtime-title"><div className="card-header"><div><p className="card-kicker">Môi trường chạy</p><h2 id="dashboard-runtime-title">Trạng thái vận hành</h2></div><span className={`badge ${snapshot.runtime.status === 'Available' ? 'badge-success' : 'badge-warning'}`}>{runtimeStatus}</span></div><p className="muted">Trình mô phỏng {snapshot.runtime.simulatorRunning ? 'đang chạy theo trạng thái đã tồn tại.' : 'không chạy tự động từ bảng vận hành.'}</p><div className="control-row"><button className="button button-secondary" type="button" onClick={() => onNavigate('audit')}>Xem nhật ký gần đây</button><button className="button button-secondary" type="button" onClick={() => onNavigate('simulator')}>Mở trình mô phỏng</button></div></section>
      </div>
      <section className="card" aria-labelledby="dashboard-audit-title"><div className="card-header"><div><p className="card-kicker">Nhật ký gần đây</p><h2 id="dashboard-audit-title">{snapshot.recentAudit.items?.length ?? 0} hoạt động trong trang</h2></div><button className="button button-secondary" type="button" onClick={() => onNavigate('audit')}>Mở toàn bộ nhật ký</button></div>{(snapshot.recentAudit.items ?? []).length === 0 ? <p className="muted">Chưa có hoạt động nhật ký trong phạm vi.</p> : <ul className="muted">{(snapshot.recentAudit.items ?? []).slice(0, 5).map((item, index) => <li key={`${item?.time ?? 'audit'}-${index}`}>{item?.time ?? '—'} · {item?.actor ?? '—'} · {item?.action ?? '—'} · {item?.summary ?? '—'}</li>)}</ul>}</section>
    </>}
  </section>
}
