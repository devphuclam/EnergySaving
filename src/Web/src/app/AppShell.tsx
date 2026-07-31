import { useEffect, useState, type ReactNode } from 'react'
import { useWebGateways } from '../gateways/GatewayContext'
import type { AuthSession } from '../gateways/webGateways'
import type { WorkspaceStatusRequest } from '../features/setup/setupTypes'
import { workspaceStatusRequestFromSearch } from '../features/setup/setupTypes'

export type WebRoute = 'setup' | 'dashboard' | 'configuration' | 'simulator' | 'telemetry' | 'audit'

const webRoutes: WebRoute[] = ['setup', 'dashboard', 'configuration', 'simulator', 'telemetry', 'audit']

function routeFromPath(pathname: string): WebRoute {
  const route = pathname.slice(1) as WebRoute
  return webRoutes.includes(route) ? route : 'configuration'
}

export type AppShellProps = {
  children: (
    route: WebRoute,
    navigate: (route: WebRoute, request?: WorkspaceStatusRequest) => void,
    session: AuthSession,
    locationKey: string,
  ) => ReactNode
}

export type AppShellState = {
  route: WebRoute
  session: AuthSession
  feedback: string
  submitting: boolean
}

export type AppShellTransition =
  | { type: 'session'; session: AuthSession }
  | { type: 'submitting' }
  | { type: 'signed-in'; session: AuthSession }
  | { type: 'signed-out' }
  | { type: 'navigate'; route: WebRoute }

export const initialAppShellState: AppShellState = {
  route: 'configuration',
  session: { state: 'loading' },
  feedback: '',
  submitting: false,
}

/** The component and the package-policy-blocked behavior source share this exact state contract. */
export function transitionAppShell(state: AppShellState, event: AppShellTransition): AppShellState {
  if (event.type === 'session') return { ...state, session: event.session, submitting: false }
  if (event.type === 'submitting') return { ...state, session: { state: 'submitting' }, submitting: true, feedback: '' }
  if (event.type === 'signed-in') return {
    ...state,
    session: event.session,
    submitting: false,
    feedback: event.session.state === 'ready'
      ? 'Đăng nhập thành công. Phạm vi được cấp đã sẵn sàng.'
      : event.session.state === 'invalid-credentials'
        ? 'Tên đăng nhập hoặc mật khẩu không đúng.'
        : 'Không thể hoàn tất đăng nhập.',
  }
  if (event.type === 'signed-out') return {
    ...state,
    session: { state: 'expired' },
    submitting: false,
    feedback: 'Đã đăng xuất.',
  }
  return { ...state, route: event.route }
}

export function AppShell({ children }: AppShellProps) {
  const gateways = useWebGateways()
  const [state, setState] = useState(() => ({
    ...initialAppShellState,
    route: routeFromPath(window.location.pathname),
  }))
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [locationKey, setLocationKey] = useState(() => window.location.href)

  useEffect(() => {
    function handlePopState() {
      setLocationKey(window.location.href)
      setState(current => transitionAppShell(current, {
        type: 'navigate',
        route: routeFromPath(window.location.pathname),
      }))
    }
    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  useEffect(() => {
    void gateways.auth.getSession()
      .then(session => setState(current => transitionAppShell(current, { type: 'session', session })))
      .catch(() => setState(current => transitionAppShell(current, { type: 'session', session: { state: 'error' } })))
  }, [gateways.auth])

  useEffect(() => {
    if (state.session.state !== 'ready') return
    const pathname = window.location.pathname
    const resolveInitialLanding = pathname === '/' || pathname === ''
    if (!resolveInitialLanding) return
    const request = workspaceStatusRequestFromSearch(window.location.search)
    void gateways.workspace.getStatus(request).then(workspace => {
      if (resolveInitialLanding) {
        const route: WebRoute = request || workspace.landing !== 'Dashboard'
          ? 'setup'
          : 'dashboard'
        setState(current => transitionAppShell(current, { type: 'navigate', route }))
      }
    }).catch(() => {
      if (resolveInitialLanding) setState(current => transitionAppShell(current, { type: 'navigate', route: 'setup' }))
    })
  }, [gateways.workspace, state.session.state, locationKey])

  async function signIn() {
    if (!username.trim() || !password) {
      setState(current => transitionAppShell(current, {
        type: 'signed-in',
        session: { state: 'invalid-credentials' },
      }))
      return
    }
    setState(current => transitionAppShell(current, { type: 'submitting' }))
    const session = await gateways.auth.signIn({ username, password })
    setState(current => transitionAppShell(current, { type: 'signed-in', session }))
  }

  async function signOut() {
    await gateways.auth.signOut()
    setPassword('')
    setState(current => transitionAppShell(current, { type: 'signed-out' }))
  }

  function navigate(route: WebRoute, request?: WorkspaceStatusRequest) {
    const query = new URLSearchParams()
    if (request && !('invalidSearch' in request)) {
      if (request.mode) query.set('mode', request.mode)
      if (request.selectedSiteId) query.set('selectedSiteId', request.selectedSiteId)
    }
    const suffix = query.toString() ? `?${query.toString()}` : ''
    window.history.pushState({}, '', `/${route}${suffix}`)
    setLocationKey(window.location.href)
    setState(current => transitionAppShell(current, { type: 'navigate', route }))
  }

  const authenticated = state.session.state === 'ready'
  const scope = authenticated
    ? state.session.scopeLabel ?? 'Phạm vi được cấp'
    : state.session.state === 'loading' ? 'Đang tải phạm vi' : 'Chưa có phạm vi'

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#configuration" onClick={() => navigate('configuration')}>
          <span className="brand-mark" aria-hidden="true">I</span>
          <span>IDEA Utility Monitoring</span>
        </a>
        <div className="session-controls">
          <span className="scope-pill" aria-label="Phạm vi hiện tại">{scope}</span>
          {authenticated ? <>
            <span className="session-user" aria-label="Người dùng đã đăng nhập">
              {state.session.username ?? 'Người dùng'} · {state.session.scopeLabel ?? 'phạm vi'}
              {state.session.isAdministrator ? ' · Admin' : ''}
            </span>
            <button className="button button-quiet" type="button" onClick={() => void signOut()}>Đăng xuất</button>
          </> : <div className="sign-in-form">
            <input className="text-input sign-in-input" type="text" placeholder="Tên đăng nhập" value={username}
              onChange={event => setUsername(event.target.value)} aria-label="Tên đăng nhập" />
            <input className="text-input sign-in-input" type="password" placeholder="Mật khẩu" value={password}
              onChange={event => setPassword(event.target.value)} aria-label="Mật khẩu" />
            <button className="button button-primary" type="button" onClick={() => void signIn()}
              disabled={state.session.state === 'loading' || state.submitting}>
              {state.submitting ? 'Đang đăng nhập…' : 'Đăng nhập'}
            </button>
          </div>}
        </div>
      </header>
      <div className="layout">
        <nav className="sidebar" aria-label="Primary navigation">
          <p className="nav-heading">Không gian làm việc</p>
          {(['setup', 'dashboard', 'configuration', 'simulator', 'telemetry', 'audit'] as WebRoute[]).map(item =>
            <button className={`nav-link${state.route === item ? ' active' : ''}`} type="button" key={item}
              aria-current={state.route === item ? 'page' : undefined} onClick={() => navigate(item)}>
              {item === 'setup' ? 'Thiết lập' : item === 'dashboard' ? 'Vận hành' : item === 'configuration' ? 'Cấu hình' : item === 'simulator' ? 'Mô phỏng' : item === 'telemetry' ? 'Dữ liệu & tình trạng' : 'Nhật ký'}
            </button>)}
          <div className="sidebar-note"><span className="status-dot" aria-hidden="true" /><span>Chế độ độc lập nhà cung cấp</span></div>
        </nav>
        <main className="content" id="main-content">
          <div className="feedback" role="status" aria-live="polite">{state.feedback}</div>
          {!authenticated && <section className="notice notice-info" aria-label="Authentication notice">
            <strong>{state.session.state === 'loading' ? 'Đang tải phiên làm việc.'
              : state.session.state === 'submitting' ? 'Đang đăng nhập.'
                : state.session.state === 'error' ? 'Không thể kết nối máy chủ.'
                  : 'Đăng nhập để quản lý không gian làm việc.'}</strong>
            <span>Mọi truy vấn và thay đổi đều được kiểm tra phạm vi tại máy chủ.</span>
          </section>}
          {state.session.state === 'invalid-credentials' && <section className="notice notice-warning" role="alert">Tên đăng nhập hoặc mật khẩu không đúng.</section>}
          {state.session.state === 'forbidden' && <section className="notice notice-warning" role="alert">Phiên không được phép. Hãy đăng nhập lại.</section>}
          {state.session.state === 'expired' && <section className="notice notice-warning" role="alert">Phiên đã hết hạn. Hãy đăng nhập lại.</section>}
          {state.session.state === 'error' && <section className="notice notice-warning" role="alert">Lỗi phiên. Kiểm tra kết nối rồi đăng nhập lại.</section>}
          {authenticated ? children(state.route, navigate, state.session, locationKey) : null}
        </main>
      </div>
    </div>
  )
}
