import type { AuthSession, GatewayState } from '../../gateways/webGateways'

export function ContextBar({ session, freshness = 'Chưa tải', cutoff = 'Chưa có cutoff' }: {
  session: AuthSession
  freshness?: 'Live' | 'Stale' | 'Degraded' | 'Chưa tải'
  cutoff?: string
}) {
  const role = session.isAdministrator ? 'Quản trị viên' : 'Người dùng được cấp quyền'
  const status: GatewayState = session.state
  return <div className="context-bar" aria-label="Ngữ cảnh vận hành">
    <div className="context-item"><span className="context-label">Phạm vi</span><strong>{session.scopeLabel ?? 'Phạm vi được cấp'}</strong></div>
    <div className="context-item"><span className="context-label">Múi giờ</span><strong>Asia/Ho_Chi_Minh</strong></div>
    <div className="context-item"><span className="context-label">Cutoff</span><strong>{cutoff}</strong></div>
    <div className="context-item"><span className="context-label">Độ mới</span><span className={`freshness-chip freshness-${freshness.toLowerCase().replace(' ', '-')}`}><span className="status-mark" aria-hidden="true">{freshness === 'Live' ? '●' : freshness === 'Chưa tải' ? '○' : '!'}</span>{freshness}</span></div>
    <div className="context-item context-user"><span className="context-label">Tài khoản</span><strong>{session.username ?? 'Chưa đăng nhập'}</strong><small>{role} · {status}</small></div>
  </div>
}
