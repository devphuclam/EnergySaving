import { useEffect, useState, type ReactNode } from 'react'
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
import { FeedbackBanner } from '../../components/feedback/FeedbackBanner'
import { useWebGateways } from '../../gateways/GatewayContext'
import { dashboardRuntimePresentation, type AuthSession, type OperationalDashboardSnapshot } from '../../gateways/webGateways'

type DashboardRoute = 'setup' | 'configuration' | 'simulator' | 'telemetry' | 'audit'

export type DashboardException = {
  key: string
  title: string
  observed: string
  evidence: string
  nextAction: string
  tone: 'warning' | 'danger' | 'info'
}

const emptySnapshot: OperationalDashboardSnapshot = {
  state: 'loading', roleMode: undefined,
  sites: { count: 0, items: [] }, sources: { count: 0, items: [] }, points: { count: 0, items: [] },
  runs: { count: 0, items: [] }, latest: { count: 0, items: [] }, health: { count: 0, items: [] },
  incompleteSetup: { count: 0 }, recentAudit: { items: [] }, runtime: { status: 'Unavailable', simulatorRunning: false },
  dependency: { status: 'Unavailable' },
}

function valueOf(item: Record<string, unknown>, ...keys: string[]): unknown {
  for (const key of keys) if (item[key] !== undefined && item[key] !== null) return item[key]
  return undefined
}

function textOf(item: Record<string, unknown>, ...keys: string[]): string | undefined {
  const value = valueOf(item, ...keys)
  return value === undefined ? undefined : String(value)
}

function itemLabel(item: Record<string, unknown>): string {
  return textOf(item, 'name', 'code', 'description', 'status', 'id', 'pointId') ?? 'Không xác định'
}

function qualityOf(value: unknown): DataQuality | undefined {
  return value === 'Good' || value === 'Uncertain' || value === 'Bad' || value === 'Missing' ? value : undefined
}

export function dashboardExceptionItems(snapshot: OperationalDashboardSnapshot): DashboardException[] {
  if (snapshot.state !== 'ready') return []
  const items: DashboardException[] = []
  if (snapshot.incompleteSetup.count > 0) items.push({
    key: 'incomplete-setup', title: 'Chuỗi cấu hình chưa hoàn tất',
    observed: `${snapshot.incompleteSetup.count} chuỗi còn bước chưa hoàn tất.`,
    evidence: snapshot.incompleteSetup.nextStep ? `Bước tiếp theo: ${snapshot.incompleteSetup.nextStep}.` : 'Endpoint không trả bước tiếp theo.',
    nextAction: 'Mở Setup để xem bước được phép tiếp theo.', tone: 'warning',
  })
  for (const [index, item] of snapshot.health.items.slice(0, 8).entries()) {
    const status = textOf(item, 'status', 'Status') ?? 'Unavailable'
    if (!['Online', 'Good', 'Available'].includes(status)) items.push({
      key: `health-${index}`, title: `Sức khỏe nguồn: ${itemLabel(item)}`,
      observed: `Nguồn đang ở trạng thái ${status}.`,
      evidence: textOf(item, 'lastReceivedAtUtc', 'LastReceivedAtUtc') ? `Lần nhận cuối: ${textOf(item, 'lastReceivedAtUtc', 'LastReceivedAtUtc')}.` : 'Hợp đồng không trả lần nhận cuối cho bản ghi này.',
      nextAction: 'Mở Measurement để kiểm tra nguồn và dữ liệu hiện tại.', tone: status === 'Decommissioned' ? 'danger' : 'warning',
    })
  }
  for (const [index, item] of snapshot.latest.items.slice(0, 8).entries()) {
    const quality = qualityOf(valueOf(item, 'quality', 'Quality'))
    if (quality === 'Bad' || quality === 'Uncertain' || quality === 'Missing') items.push({
      key: `quality-${index}`, title: `Chất lượng dữ liệu: ${itemLabel(item)}`,
      observed: `Bản ghi hiện tại có trạng thái ${quality}.`,
      evidence: 'Quality được lấy trực tiếp từ Operational Dashboard response; lý do chi tiết không có trong contract này.',
      nextAction: 'Mở Measurement để xem timestamp, nguồn và chi tiết chất lượng.', tone: quality === 'Bad' ? 'danger' : 'warning',
    })
  }
  if (snapshot.points.count > snapshot.latest.count) items.push({
    key: 'missing-latest', title: 'Một số điểm chưa có giá trị mới nhất',
    observed: `${snapshot.points.count - snapshot.latest.count} điểm không xuất hiện trong tập latest của response.`,
    evidence: 'Không suy diễn nguyên nhân hoặc chuyển Missing thành zero.',
    nextAction: 'Mở Measurement và chọn điểm cần kiểm tra.', tone: 'warning',
  })
  return items
}

export function dashboardFreshness(snapshot: OperationalDashboardSnapshot): Freshness {
  const statuses = snapshot.health.items.map(item => textOf(item, 'status', 'Status'))
  if (statuses.some(status => status === 'Stale')) return 'Stale'
  if (statuses.some(status => ['NoData', 'Suspended', 'Decommissioned'].includes(status ?? ''))) return 'Degraded'
  return 'Unavailable'
}

function DashboardSummary({ snapshot, onNavigate }: { snapshot: OperationalDashboardSnapshot; onNavigate: (route: DashboardRoute) => void }) {
  const entries: Array<{ label: string; value: number; route: DashboardRoute; detail: string }> = [
    { label: 'Địa điểm', value: snapshot.sites.count, route: 'configuration', detail: 'phạm vi được trả về' },
    { label: 'Nguồn dữ liệu', value: snapshot.sources.count, route: 'configuration', detail: 'nguồn trong phạm vi' },
    { label: 'Điểm đo', value: snapshot.points.count, route: 'telemetry', detail: 'điểm đang quan sát' },
    { label: 'Giá trị mới nhất', value: snapshot.latest.count, route: 'telemetry', detail: 'bản ghi có trong response' },
    { label: 'Nguồn có health', value: snapshot.health.count, route: 'telemetry', detail: 'bản ghi health' },
    { label: 'Lượt chạy', value: snapshot.runs.count, route: 'simulator', detail: 'lượt chạy được trả về' },
  ]
  return <section className="dashboard-summary" aria-labelledby="dashboard-summary-title"><div className="section-heading"><div><p className="card-kicker">Tóm tắt có kiểm chứng</p><h2 id="dashboard-summary-title">Phạm vi và bằng chứng hiện tại</h2></div><span className="metadata">Không phải KPI dự báo</span></div><ul className="dashboard-summary-list">{entries.map(entry => <li key={entry.label}><div><strong>{entry.label}</strong><span>{entry.detail}</span></div><strong className="summary-value">{entry.value}</strong><button className="button button-quiet" type="button" onClick={() => onNavigate(entry.route)}>Mở</button></li>)}</ul></section>
}

function ExceptionList({ items, onNavigate }: { items: DashboardException[]; onNavigate: (route: DashboardRoute) => void }) {
  if (items.length === 0) return <EmptyState title="Không có exception trong response" message="Không có mục cần chú ý nào được trả về trong phạm vi hiện tại. Điều này không khẳng định dữ liệu hoàn chỉnh hoặc có coverage đầy đủ." />
  return <section className="dashboard-exceptions" aria-labelledby="dashboard-attention-title"><div className="section-heading"><div><p className="card-kicker">Ưu tiên kiểm tra</p><h2 id="dashboard-attention-title">Mục cần chú ý</h2></div><span className="badge badge-warning">{items.length} mục</span></div><ol className="exception-list">{items.map(item => <li className={`exception-item exception-${item.tone}`} key={item.key}><OperationalStatusBadge status={item.tone === 'danger' ? 'Bad' : 'Uncertain'} detail={item.title} /><p><strong>Quan sát:</strong> {item.observed}</p><p><strong>Bằng chứng:</strong> {item.evidence}</p><div className="dashboard-action-row"><span className="metadata"><strong>Bước tiếp theo:</strong> {item.nextAction}</span><button className="button button-secondary" type="button" onClick={() => onNavigate(item.nextAction.includes('Setup') ? 'setup' : 'telemetry')}>Mở chi tiết</button></div></li>)}</ol></section>
}

function DashboardReady({ snapshot, onNewSetup, onContinueSetup, onNavigate }: { snapshot: OperationalDashboardSnapshot; onNewSetup: () => void; onContinueSetup: () => void; onNavigate: (route: DashboardRoute) => void }) {
  const exceptions = dashboardExceptionItems(snapshot)
  const freshness = dashboardFreshness(snapshot)
  const runtime = dashboardRuntimePresentation(snapshot)
  const runtimeStatus = snapshot.runtime.status === 'Available' ? 'Available' : 'Unavailable'
  return <>
    <section className="dashboard-scope-strip" aria-label="Bằng chứng ngữ cảnh dashboard"><div><span className="context-label">Scope</span><strong>Phạm vi được cấp</strong></div><div><span className="context-label">Múi giờ</span><strong>Asia/Ho_Chi_Minh</strong></div><div><span className="context-label">Cutoff</span><strong>Chưa có cutoff trong endpoint</strong></div><div><span className="context-label">Freshness</span><FreshnessIndicator freshness={freshness} cutoff="Chưa có cutoff" /></div></section>
    <ExceptionList items={exceptions} onNavigate={onNavigate} />
    <DashboardSummary snapshot={snapshot} onNavigate={onNavigate} />
    <div className="dashboard-evidence-grid"><section className="evidence-panel" aria-labelledby="dashboard-health-title"><div className="section-heading"><div><p className="card-kicker">Source health</p><h2 id="dashboard-health-title">Tình trạng nguồn</h2></div><button className="button button-secondary" type="button" onClick={() => onNavigate('telemetry')}>Mở Measurement</button></div>{snapshot.health.items.length === 0 ? <EmptyState title="Chưa có source health" message="Response hiện tại không trả bản ghi source health trong phạm vi này." /> : <ul className="evidence-list">{snapshot.health.items.slice(0, 8).map((item, index) => <li key={`${itemLabel(item)}-${index}`}><strong>{itemLabel(item)}</strong><OperationalStatusBadge status={textOf(item, 'status', 'Status') === 'Online' ? 'Good' : 'Unavailable'} detail={textOf(item, 'status', 'Status') ?? 'Unavailable'} /><span className="metadata">{textOf(item, 'lastReceivedAtUtc', 'LastReceivedAtUtc') ?? 'Lần nhận cuối: chưa có'}</span></li>)}</ul>}</section>
      <section className="evidence-panel" aria-labelledby="dashboard-quality-title"><div className="section-heading"><div><p className="card-kicker">Data quality</p><h2 id="dashboard-quality-title">Chất lượng giá trị mới nhất</h2></div><span className="metadata">Lý do chi tiết chỉ có ở Measurement</span></div>{snapshot.latest.items.length === 0 ? <EmptyState title="Chưa có giá trị mới nhất" message="Không có bản ghi hiện tại trong response; đây không phải giá trị zero." /> : <ul className="evidence-list">{snapshot.latest.items.slice(0, 8).map((item, index) => { const quality = qualityOf(valueOf(item, 'quality', 'Quality')); return <li key={`${itemLabel(item)}-${index}`}><strong>{itemLabel(item)}</strong>{quality ? <DataQualityIndicator quality={quality} reason="Chi tiết reason không có trong dashboard contract" /> : <OperationalStatusBadge status="Unavailable" detail="Quality chưa có" />}<span className="numeric">{String(valueOf(item, 'value', 'Value') ?? 'Missing')} {String(valueOf(item, 'unit', 'Unit') ?? '')}</span></li> })}</ul>}</section></div>
    <section className="card setup-runtime-panel" aria-labelledby="dashboard-setup-title"><div><p className="card-kicker">Thiết lập và runtime</p><h2 id="dashboard-setup-title">Mức sẵn sàng kích hoạt</h2><p className="muted">{snapshot.incompleteSetup.count > 0 ? `${snapshot.incompleteSetup.count} chuỗi chưa hoàn tất; bước tiếp theo ${snapshot.incompleteSetup.nextStep ?? 'chưa có trong response'}.` : 'Không có chuỗi chưa hoàn tất trong response.'}</p></div><div className="dashboard-action-row">{snapshot.incompleteSetup.count > 0 && <button className="button button-primary" type="button" onClick={onContinueSetup}>Tiếp tục Setup</button>}{snapshot.incompleteSetup.count === 0 && snapshot.roleMode === 'Administrator' && <button className="button button-secondary" type="button" onClick={onNewSetup}>Tạo cấu hình</button>}<OperationalStatusBadge status={runtimeStatus === 'Available' ? 'Available' : 'Unavailable'} detail={`Simulator ${runtime.label}`} /></div></section>
    <ChartContainer title="Xu hướng lịch sử" description="Chỉ hiển thị khi endpoint cung cấp historical series có timestamp." points={[]} metadata={{ timezone: 'Asia/Ho_Chi_Minh', coverage: 'Coverage: chưa có trong contract' }} unavailableReason="Operational Dashboard response hiện chỉ có summary/latest; không có historical series. Không dựng trend từ một giá trị hiện tại." />
  </>
}

export function OperationalDashboard({ onNewSetup, onContinueSetup, onNavigate }: { session: AuthSession; onNewSetup: () => void; onContinueSetup: () => void; onNavigate: (route: DashboardRoute) => void }) {
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
  const retry = <button className="button button-secondary" type="button" onClick={() => setReloadToken(value => value + 1)}>Thử lại</button>
  let content: ReactNode
  if (state === 'loading') content = <LoadingState message="Đang tải bằng chứng dashboard…" />
  else if (state === 'forbidden' || state === 'no-scope') content = <ForbiddenState title="Không có phạm vi dashboard" message="Dashboard không trả dữ liệu ngoài phạm vi được cấp. Hãy liên hệ quản trị viên để được cấp Site hoặc Area." action={retry} />
  else if (state === 'dependency') content = <BlockedState message="Dashboard phụ thuộc vào dịch vụ dữ liệu chưa sẵn sàng; chưa có kết luận vận hành." nextAction={retry} />
  else if (state === 'runtime-error' || state === 'error') content = <ErrorState message="Không thể tải dashboard từ endpoint hiện tại." action={retry} />
  else if (state === 'no-data') content = <EmptyState title="Chưa có dữ liệu dashboard" message="Không có dữ liệu hợp lệ trong response hiện tại; không hiển thị zero thay thế." action={retry} />
  else content = <DashboardReady snapshot={snapshot} onNewSetup={onNewSetup} onContinueSetup={onContinueSetup} onNavigate={onNavigate} />

  return <section className="page" aria-labelledby="dashboard-title"><PageHeader eyebrow="Không gian vận hành" title="Bảng vận hành" description="Exception, source health và dữ liệu hiện tại trong phạm vi được cấp; không tự động chẩn đoán hoặc điều khiển thiết bị." /><FeedbackBanner tone="info" title="Bằng chứng có giới hạn" message={`Dashboard response: ${state}. Coverage, cutoff và historical series chỉ được hiển thị khi contract cung cấp trực tiếp.`} live={false} />{content}</section>
}
