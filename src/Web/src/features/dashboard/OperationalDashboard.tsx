import { useEffect, useState, type ReactNode } from 'react'
import { ChartContainer } from '../../components/charts/ChartContainer'
import { PageHeader } from '../../components/context/PageHeader'
import { DataQualityIndicator, type DataQuality } from '../../components/status/DataQualityIndicator'
import { FreshnessIndicator, type Freshness } from '../../components/status/FreshnessIndicator'
import { OperationalStatusBadge, type OperationalStatus } from '../../components/status/OperationalStatusBadge'
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
  kind: 'health' | 'quality' | 'incomplete-setup' | 'missing-latest'
  priority: number
  status: OperationalStatus
  title: string
  observed: string
  evidence: string
  nextAction: string
  tone: 'warning' | 'danger' | 'info'
}

export type DashboardExceptionPresentation = {
  all: DashboardException[]
  visible: DashboardException[]
  totalCount: number
  hiddenCount: number
  limit: number
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

function pointIdOf(item: Record<string, unknown>): string | undefined {
  return textOf(item, 'pointId', 'PointId', 'id', 'Id')
}

export const DASHBOARD_QUALITY_REASON_UNAVAILABLE = 'Dashboard contract không cung cấp quality reason.'
export const DASHBOARD_QUALITY_UNRECOGNIZED = 'Dashboard contract did not provide a recognized quality.'

function pointLabel(snapshot: OperationalDashboardSnapshot, item: Record<string, unknown>): string {
  const pointId = pointIdOf(item)
  const point = snapshot.points.items.find(candidate => pointId !== undefined && pointIdOf(candidate) === pointId)
  return textOf(point ?? {}, 'code', 'description', 'name')
    ?? textOf(item, 'code', 'description', 'name')
    ?? pointId
    ?? 'Không xác định'
}

function qualityOf(value: unknown): DataQuality | undefined {
  return value === 'Good' || value === 'Uncertain' || value === 'Bad' || value === 'Missing' ? value : undefined
}

export type DashboardQualityPresentation = {
  quality?: DataQuality
  status: OperationalStatus
  isException: boolean
  priority: number
  reasonAvailability: 'authoritative' | 'absent'
}

export function dashboardQualityPresentation(value: unknown): DashboardQualityPresentation {
  const quality = qualityOf(value)
  if (!quality) return { status: 'Unavailable', isException: true, priority: 6, reasonAvailability: 'absent' }
  if (quality === 'Good') return { quality, status: 'Good', isException: false, priority: 0, reasonAvailability: 'authoritative' }
  const priority = quality === 'Bad' ? 1 : quality === 'Missing' ? 3 : 5
  return { quality, status: quality, isException: true, priority, reasonAvailability: 'authoritative' }
}

function dashboardRecordTotal(record: { count: number; items: unknown[] }): number {
  return Math.max(record.count, record.items.length)
}

export type DashboardHealthPresentation = {
  status: OperationalStatus
  freshness: Freshness
}

export function dashboardHealthPresentation(value: unknown): DashboardHealthPresentation {
  switch (value) {
    case 'Online': return { status: 'Good', freshness: 'Live' }
    case 'Stale': return { status: 'Stale', freshness: 'Stale' }
    case 'NoData': return { status: 'Missing', freshness: 'Degraded' }
    case 'Missing': return { status: 'Missing', freshness: 'Degraded' }
    case 'Suspended': return { status: 'Blocked', freshness: 'Degraded' }
    case 'Decommissioned': return { status: 'Unavailable', freshness: 'Degraded' }
    case 'Bad': return { status: 'Bad', freshness: 'Degraded' }
    case 'Uncertain': return { status: 'Uncertain', freshness: 'Degraded' }
    default: return { status: 'Unavailable', freshness: 'Unavailable' }
  }
}

function dashboardExceptionPriority(kind: DashboardException['kind'], value?: string): number {
  if (kind === 'incomplete-setup') return 7
  if (kind === 'missing-latest') return 3
  if (kind === 'quality') return value === 'Bad' ? 1 : value === 'Missing' ? 3 : value === 'Uncertain' ? 5 : 6
  switch (value) {
    case 'Bad':
    case 'Decommissioned': return 1
    case 'Suspended':
    case 'Blocked': return 2
    case 'NoData':
    case 'Missing': return 3
    case 'Stale': return 4
    case 'Uncertain': return 5
    default: return 6
  }
}

export function rankDashboardException(exception: DashboardException): number {
  return exception.priority
}

function stableExceptionIdentity(snapshot: OperationalDashboardSnapshot, item: Record<string, unknown>): string {
  return pointIdOf(item)
    ?? textOf(item, 'code', 'description', 'name')
    ?? pointLabel(snapshot, item)
}

export function collectDashboardExceptions(snapshot: OperationalDashboardSnapshot): DashboardException[] {
  if (snapshot.state !== 'ready') return []
  const items: DashboardException[] = []
  if (snapshot.incompleteSetup.count > 0) items.push({
    key: 'incomplete-setup', kind: 'incomplete-setup', priority: dashboardExceptionPriority('incomplete-setup'), status: 'Blocked', title: 'Chuỗi cấu hình chưa hoàn tất',
    observed: `${snapshot.incompleteSetup.count} chuỗi còn bước chưa hoàn tất.`,
    evidence: snapshot.incompleteSetup.nextStep ? `Bước tiếp theo: ${snapshot.incompleteSetup.nextStep}.` : 'Endpoint không trả bước tiếp theo.',
    nextAction: 'Mở Setup để xem bước được phép tiếp theo.', tone: 'warning',
  })
  for (const item of snapshot.health.items) {
    const status = textOf(item, 'status', 'Status')
    const presentation = dashboardHealthPresentation(status)
    if (status !== 'Online') items.push({
      key: `health:${stableExceptionIdentity(snapshot, item)}:${status ?? 'Unknown'}`, kind: 'health', priority: dashboardExceptionPriority('health', status), status: presentation.status, title: `Sức khỏe nguồn: ${pointLabel(snapshot, item)}`,
      observed: `Nguồn đang ở trạng thái ${status ?? 'Unknown'}.`,
      evidence: textOf(item, 'lastReceivedAtUtc', 'LastReceivedAtUtc') ? `Lần nhận cuối: ${textOf(item, 'lastReceivedAtUtc', 'LastReceivedAtUtc')}.` : 'Hợp đồng không trả lần nhận cuối cho bản ghi này.',
      nextAction: 'Mở Measurement để kiểm tra nguồn và dữ liệu hiện tại.', tone: presentation.status === 'Unavailable' ? 'danger' : 'warning',
    })
  }
  for (const item of snapshot.latest.items) {
    const rawQuality = valueOf(item, 'quality', 'Quality')
    const presentation = dashboardQualityPresentation(rawQuality)
    if (presentation.isException) items.push({
      key: `quality:${stableExceptionIdentity(snapshot, item)}:${rawQuality === undefined ? 'absent' : String(rawQuality)}`, kind: 'quality', priority: presentation.priority, status: presentation.status, title: `Chất lượng dữ liệu: ${pointLabel(snapshot, item)}`,
      observed: presentation.reasonAvailability === 'authoritative' ? `Bản ghi hiện tại có trạng thái ${presentation.quality}.` : DASHBOARD_QUALITY_UNRECOGNIZED,
      evidence: presentation.reasonAvailability === 'authoritative' ? 'Quality được lấy trực tiếp từ Operational Dashboard response; lý do chi tiết không có trong contract này.' : DASHBOARD_QUALITY_UNRECOGNIZED,
      nextAction: 'Mở Measurement để xem timestamp, nguồn và chi tiết chất lượng.', tone: presentation.status === 'Bad' ? 'danger' : 'warning',
    })
  }
  if (snapshot.points.count > snapshot.latest.count) items.push({
    key: 'missing-latest', kind: 'missing-latest', priority: dashboardExceptionPriority('missing-latest'), status: 'Missing', title: 'Một số điểm chưa có giá trị mới nhất',
    observed: `${snapshot.points.count - snapshot.latest.count} điểm không xuất hiện trong tập latest của response.`,
    evidence: 'Không suy diễn nguyên nhân hoặc chuyển Missing thành zero.',
    nextAction: 'Mở Measurement và chọn điểm cần kiểm tra.', tone: 'warning',
  })
  return items.sort((left, right) => rankDashboardException(left) - rankDashboardException(right) || left.key.localeCompare(right.key))
}

export function dashboardExceptionItems(snapshot: OperationalDashboardSnapshot): DashboardException[] {
  return collectDashboardExceptions(snapshot)
}

export function dashboardExceptionPresentation(snapshot: OperationalDashboardSnapshot, limit = 8): DashboardExceptionPresentation {
  const all = collectDashboardExceptions(snapshot)
  const safeLimit = Number.isFinite(limit) && limit >= 0 ? Math.floor(limit) : all.length
  const visible = all.slice(0, safeLimit)
  return { all, visible, totalCount: all.length, hiddenCount: Math.max(0, all.length - visible.length), limit: safeLimit }
}

export function dashboardFreshness(snapshot: OperationalDashboardSnapshot): Freshness {
  if (snapshot.health.items.length === 0) return 'Unavailable'
  const presentations = snapshot.health.items.map(item => dashboardHealthPresentation(textOf(item, 'status', 'Status')))
  if (presentations.some(presentation => presentation.freshness === 'Degraded')) return 'Degraded'
  if (presentations.some(presentation => presentation.freshness === 'Stale')) return 'Stale'
  if (presentations.every(presentation => presentation.freshness === 'Live')) return 'Live'
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

function ExceptionList({ presentation, onNavigate }: { presentation: DashboardExceptionPresentation; onNavigate: (route: DashboardRoute) => void }) {
  if (presentation.totalCount === 0) return <EmptyState title="Không có exception trong response" message="Không có mục cần chú ý nào được trả về trong phạm vi hiện tại. Điều này không khẳng định dữ liệu hoàn chỉnh hoặc có coverage đầy đủ." />
  return <section className="dashboard-exceptions" aria-labelledby="dashboard-attention-title"><div className="section-heading"><div><p className="card-kicker">Ưu tiên kiểm tra</p><h2 id="dashboard-attention-title">Mục cần chú ý</h2></div><span className="badge badge-warning">{presentation.totalCount} mục</span></div>{presentation.hiddenCount > 0 && <p className="metadata">Đang hiển thị {presentation.visible.length} trong tổng số {presentation.totalCount} mục cần chú ý.</p>}<ol className="exception-list">{presentation.visible.map(item => <li className={`exception-item exception-${item.tone}`} key={item.key}><OperationalStatusBadge status={item.status} detail={item.title} /><p><strong>Quan sát:</strong> {item.observed}</p><p><strong>Bằng chứng:</strong> {item.evidence}</p><div className="dashboard-action-row"><span className="metadata"><strong>Bước tiếp theo:</strong> {item.nextAction}</span><button className="button button-secondary" type="button" onClick={() => onNavigate(item.nextAction.includes('Setup') ? 'setup' : 'telemetry')}>Mở chi tiết</button></div></li>)}</ol>{presentation.hiddenCount > 0 && <div className="dashboard-action-row"><span className="metadata">Một số mục chưa hiển thị để giữ danh sách dễ quét.</span><button className="button button-secondary" type="button" onClick={() => onNavigate('telemetry')}>Mở Measurement</button></div>}</section>
}

function DashboardReady({ snapshot, onNewSetup, onContinueSetup, onNavigate }: { snapshot: OperationalDashboardSnapshot; onNewSetup: () => void; onContinueSetup: () => void; onNavigate: (route: DashboardRoute) => void }) {
  const exceptions = dashboardExceptionPresentation(snapshot, 8)
  const freshness = dashboardFreshness(snapshot)
  const runtime = dashboardRuntimePresentation(snapshot)
  const runtimeStatus = snapshot.runtime.status === 'Available' ? 'Available' : 'Unavailable'
  return <>
    <section className="dashboard-scope-strip" aria-label="Bằng chứng ngữ cảnh dashboard"><div><span className="context-label">Scope</span><strong>Phạm vi được cấp</strong></div><div><span className="context-label">Múi giờ</span><strong>Asia/Ho_Chi_Minh</strong></div><div><span className="context-label">Cutoff</span><strong>Chưa có cutoff trong contract</strong></div><div><span className="context-label">Freshness</span><FreshnessIndicator freshness={freshness} /><span className="metadata">Cutoff chưa có trong contract</span></div></section>
    <ExceptionList presentation={exceptions} onNavigate={onNavigate} />
    <DashboardSummary snapshot={snapshot} onNavigate={onNavigate} />
    <div className="dashboard-evidence-grid"><section className="evidence-panel" aria-labelledby="dashboard-health-title"><div className="section-heading"><div><p className="card-kicker">Source health</p><h2 id="dashboard-health-title">Tình trạng nguồn</h2></div><button className="button button-secondary" type="button" onClick={() => onNavigate('telemetry')}>Mở Measurement</button></div>{snapshot.health.items.length === 0 ? <EmptyState title="Chưa có source health" message="Response hiện tại không trả bản ghi source health trong phạm vi này." /> : <><p className="metadata">Hiển thị {Math.min(8, snapshot.health.items.length)} trong tổng số {dashboardRecordTotal(snapshot.health)} bản ghi source health.</p><ul className="evidence-list">{snapshot.health.items.slice(0, 8).map((item, index) => { const status = textOf(item, 'status', 'Status'); const presentation = dashboardHealthPresentation(status); const received = textOf(item, 'lastReceivedAtUtc', 'LastReceivedAtUtc'); return <li key={`${pointLabel(snapshot, item)}-${index}`}><strong>{pointLabel(snapshot, item)}</strong><OperationalStatusBadge status={presentation.status} detail={status ?? 'Unknown'} /><span className="metadata">{received ? `Lần nhận cuối: ${received}` : 'Lần nhận cuối: chưa có'}</span></li> })}</ul></>}</section>
      <section className="evidence-panel" aria-labelledby="dashboard-quality-title"><div className="section-heading"><div><p className="card-kicker">Data quality</p><h2 id="dashboard-quality-title">Chất lượng giá trị mới nhất</h2></div><span className="metadata">{DASHBOARD_QUALITY_REASON_UNAVAILABLE}</span></div>{snapshot.latest.items.length === 0 ? <EmptyState title="Chưa có giá trị mới nhất" message="Không có bản ghi hiện tại trong response; đây không phải giá trị zero." /> : <><p className="metadata">Hiển thị {Math.min(8, snapshot.latest.items.length)} trong tổng số {dashboardRecordTotal(snapshot.latest)} bản ghi latest.</p><ul className="evidence-list">{snapshot.latest.items.slice(0, 8).map((item, index) => { const quality = dashboardQualityPresentation(valueOf(item, 'quality', 'Quality')); return <li key={`${pointLabel(snapshot, item)}-${index}`}><strong>{pointLabel(snapshot, item)}</strong>{quality.quality ? <DataQualityIndicator quality={quality.quality} /> : <OperationalStatusBadge status={quality.status} detail={DASHBOARD_QUALITY_UNRECOGNIZED} />}<span className="numeric">{String(valueOf(item, 'value', 'Value') ?? 'Missing')} {String(valueOf(item, 'unit', 'Unit') ?? '')}</span></li> })}</ul></>}</section></div>
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

  return <section className="page" aria-labelledby="dashboard-title"><PageHeader titleId="dashboard-title" eyebrow="Không gian vận hành" title="Bảng vận hành" description="Exception, source health và dữ liệu hiện tại trong phạm vi được cấp; không tự động chẩn đoán hoặc điều khiển thiết bị." /><FeedbackBanner tone="info" title="Bằng chứng có giới hạn" message={`Dashboard response: ${state}. Coverage, cutoff và historical series chỉ được hiển thị khi contract cung cấp trực tiếp.`} live={false} />{content}</section>
}
