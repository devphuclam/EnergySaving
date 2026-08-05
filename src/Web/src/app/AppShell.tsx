import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useWebGateways } from '../gateways/GatewayContext'
import type { AuthSession } from '../gateways/webGateways'
import { WorkspaceGatewayError, type WorkspaceStatusRequest } from '../features/setup/setupTypes'
import { workspaceStatusRequestFromSearch } from '../features/setup/setupTypes'
import { ContextBar } from '../components/context/ContextBar'
import { ForbiddenState } from '../components/feedback/ForbiddenState'
import { RetryState } from '../components/feedback/RetryState'
import { ConfirmDialog } from '../components/dialogs/ConfirmDialog'
import { hasUnsavedChanges, unsavedChangesMessage } from '../components/forms/UnsavedChangesGuard'
import { NavigationDrawer } from '../components/navigation/NavigationDrawer'
import { Rail } from '../components/navigation/Rail'
import { Sidebar } from '../components/navigation/Sidebar'
import {
  deriveRouteAccess,
  firstPermittedNavigationRoute,
  isNavigationRouteAvailable,
  navigationItems,
  resolveLanding,
  routeFromPath as navigationRouteFromPath,
  visibleNavigationItems,
  type LandingResolution,
  type NavigationRoute,
  type RouteAccess,
  type WorkspaceRoleMode,
} from '../components/navigation/NavigationModel'

export type WebRoute = NavigationRoute
export type NavigationMode = 'expanded' | 'rail' | 'drawer-open'

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
  navMode: NavigationMode
  landingResolved: boolean
  setupRequired: boolean
  landingPresentation?: LandingResolution
  /** Server-derived route availability; absent until the workspace status confirms scope. */
  routeAccess?: RouteAccess
}

export type AppShellTransition =
  | { type: 'session'; session: AuthSession }
  | { type: 'submitting' }
  | { type: 'signed-in'; session: AuthSession }
  | { type: 'signed-out' }
  | { type: 'navigate'; route: WebRoute }
  | { type: 'navigation-denied'; nextRoute?: NavigationRoute }
  | { type: 'nav-mode'; mode: NavigationMode }
  | { type: 'setup-required'; setupRequired: boolean }
  | { type: 'route-access'; access: RouteAccess }
  | { type: 'retry-workspace-status' }
  | { type: 'landing'; resolution: LandingResolution }

export const initialAppShellState: AppShellState = {
  route: 'configuration',
  session: { state: 'loading' },
  feedback: '',
  submitting: false,
  navMode: 'expanded',
  landingResolved: false,
  setupRequired: false,
}

/** The component and the package-policy-blocked behavior source share this exact state contract. */
export function transitionAppShell(state: AppShellState, event: AppShellTransition): AppShellState {
  if (event.type === 'session') return { ...state, session: event.session, submitting: false }
  if (event.type === 'submitting') return { ...state, session: { state: 'submitting' }, submitting: true, feedback: '' }
  if (event.type === 'signed-in') return {
    ...state,
    session: event.session,
    submitting: false,
    landingResolved: false,
    setupRequired: false,
    landingPresentation: undefined,
    routeAccess: undefined,
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
    landingResolved: false,
    setupRequired: false,
    landingPresentation: undefined,
    routeAccess: undefined,
    feedback: 'Đã đăng xuất.',
  }
  if (event.type === 'nav-mode') return { ...state, navMode: event.mode }
  if (event.type === 'setup-required') return { ...state, setupRequired: event.setupRequired }
  if (event.type === 'route-access') return { ...state, routeAccess: event.access }
  if (event.type === 'retry-workspace-status') return { ...state, landingResolved: false, landingPresentation: undefined, routeAccess: undefined, setupRequired: false, feedback: '' }
  if (event.type === 'navigation-denied') return { ...state, landingPresentation: { kind: 'safe-forbidden', nextRoute: event.nextRoute } }
  if (event.type === 'landing') return {
    ...state,
    route: event.resolution.kind === 'route' ? event.resolution.route : state.route,
    landingResolved: true,
    landingPresentation: event.resolution,
  }
  return { ...state, route: event.route, landingPresentation: undefined }
}

/**
 * Fail-closed mapping of a workspace-status failure to an existing session presentation. A failed
 * workspace status is never converted into a normal priority route.
 */
export function workspaceStatusFailureSession(error: unknown): AuthSession {
  if (error instanceof WorkspaceGatewayError && error.status === 401) return { state: 'expired' }
  if (error instanceof WorkspaceGatewayError && error.status === 403) return { state: 'forbidden' }
  return { state: 'error' }
}

/** Contract: >=1280px expanded sidebar, 768-1279px rail, <768px rail retained. */
export function viewportNavigationMode(width = window.innerWidth): NavigationMode {
  return width >= 1280 ? 'expanded' : 'rail'
}

/**
 * Popstate cancellation target: a back/forward attempt must be undone back to the last URL this
 * app committed (captured before popstate fired), never to the already-popped URL the browser is
 * showing. Programmatic navigation has not changed the URL yet, so nothing needs restoring.
 */
export function navigationCancellationRestore(fromHistory: boolean, lastCommittedHref: string | undefined): string | undefined {
  return fromHistory ? lastCommittedHref : undefined
}

type PendingNavigation = {
  route: WebRoute
  request?: WorkspaceStatusRequest
  fromHistory: boolean
  previousHref?: string
  message: string
}

export function AppShell({ children }: AppShellProps) {
  const gateways = useWebGateways()
  const [state, setState] = useState<AppShellState>(() => ({
    ...initialAppShellState,
    route: navigationRouteFromPath(window.location.pathname) ?? 'configuration',
    navMode: viewportNavigationMode(),
  }))
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [locationKey, setLocationKey] = useState(() => window.location.href)
  const [pendingNavigation, setPendingNavigation] = useState<PendingNavigation | null>(null)
  const lastCommittedHrefRef = useRef(window.location.href)
  const effectiveAccess: RouteAccess = state.routeAccess ?? deriveRouteAccess({ setupRequired: false })
  const visibleItems = useMemo(() => visibleNavigationItems(effectiveAccess), [effectiveAccess])

  useEffect(() => { document.documentElement.lang = 'vi' }, [])

  function performNavigation(route: WebRoute, request?: WorkspaceStatusRequest, fromHistory = false) {
    if (!fromHistory) {
      const query = new URLSearchParams()
      if (request && !('invalidSearch' in request)) {
        if (request.mode) query.set('mode', request.mode)
        if (request.selectedSiteId) query.set('selectedSiteId', request.selectedSiteId)
      }
      const suffix = query.toString() ? `?${query.toString()}` : ''
      window.history.pushState({}, '', `/${route}${suffix}`)
    }
    lastCommittedHrefRef.current = window.location.href
    setLocationKey(window.location.href)
    setState(current => transitionAppShell(current, { type: 'navigate', route }))
  }

  /** Canonical guarded navigation used by every entry path: brand, sidebar, rail, drawer,
   * popstate, route callbacks, session restoration and programmatic callers. */
  function requestNavigation(route: WebRoute, request?: WorkspaceStatusRequest, fromHistory = false) {
    if (state.session.state !== 'ready' || !state.routeAccess) return
    const access = state.routeAccess
    if (!isNavigationRouteAvailable(route, access)) {
      setState(current => transitionAppShell(current, {
        type: 'navigation-denied',
        nextRoute: firstPermittedNavigationRoute(access),
      }))
      return
    }
    if (hasUnsavedChanges()) {
      setPendingNavigation({
        route,
        request,
        fromHistory,
        previousHref: navigationCancellationRestore(fromHistory, lastCommittedHrefRef.current),
        message: unsavedChangesMessage() ?? 'Bạn có thay đổi chưa lưu. Hãy lưu hoặc hủy trước khi rời trang.',
      })
      return
    }
    performNavigation(route, request, fromHistory)
  }

  useEffect(() => {
    function handlePopState() {
      setLocationKey(window.location.href)
      const route = navigationRouteFromPath(window.location.pathname)
      if (!route || !state.routeAccess || !isNavigationRouteAvailable(route, state.routeAccess)) {
        setState(current => transitionAppShell(current, {
          type: 'navigation-denied',
          nextRoute: firstPermittedNavigationRoute(state.routeAccess ?? deriveRouteAccess({ setupRequired: false })),
        }))
        return
      }
      requestNavigation(route, undefined, true)
    }
    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [state.session.state, state.routeAccess])

  useEffect(() => {
    function handleResize() {
      const next = viewportNavigationMode()
      setState(current => transitionAppShell(current, { type: 'nav-mode', mode: next }))
    }
    window.addEventListener('resize', handleResize)
    return () => window.removeEventListener('resize', handleResize)
  }, [])

  useEffect(() => {
    void gateways.auth.getSession()
      .then(session => setState(current => transitionAppShell(current, { type: 'session', session })))
      .catch(() => setState(current => transitionAppShell(current, { type: 'session', session: { state: 'error' } })))
  }, [gateways.auth])

  useEffect(() => {
    if (state.session.state !== 'ready' || state.landingResolved) return
    const pathname = window.location.pathname
    const request = workspaceStatusRequestFromSearch(window.location.search)
    const isRoot = pathname === '/' || pathname === ''
    const requestedRoute = isRoot && request && !('invalidSearch' in request) ? '/setup' : pathname
    const resolve = (roleMode: WorkspaceRoleMode, setupRequired: boolean) => {
      const access = deriveRouteAccess({ capabilities: state.session.capabilities, roleMode, setupRequired })
      const enabledRoutes = navigationItems.filter(item => access[item.route]).map(item => item.route)
      const resolution = resolveLanding({
        deepLink: isRoot ? (requestedRoute === '/setup' ? requestedRoute : undefined) : requestedRoute,
        enabledRoutes,
        dashboardPermitted: access.dashboard,
        setupRequired,
      })
      setState(current => transitionAppShell(current, { type: 'route-access', access }))
      setState(current => transitionAppShell(current, { type: 'setup-required', setupRequired }))
      setState(current => transitionAppShell(current, { type: 'landing', resolution }))
      if (isRoot && resolution.kind === 'route') {
        const suffix = requestedRoute === '/setup' && request && !('invalidSearch' in request) ? window.location.search : ''
        window.history.replaceState({}, '', `/${resolution.route}${suffix}`)
        lastCommittedHrefRef.current = window.location.href
        setLocationKey(window.location.href)
      }
    }
    void gateways.workspace.getStatus(request).then(workspace => {
      resolve(workspace.roleMode, workspace.landing === 'SetupWizard' || workspace.landing === 'ContinueSetup')
    }).catch(error => {
      const failureSession = workspaceStatusFailureSession(error)
      if (failureSession.state === 'expired' || failureSession.state === 'forbidden') {
        setState(current => transitionAppShell(current, { type: 'session', session: failureSession }))
        return
      }
      // Dependency and unexpected failures fail closed on root and non-root entries with a
      // retry-able blocked presentation. A failed workspace status never fabricates availability.
      setState(current => transitionAppShell(current, { type: 'landing', resolution: { kind: 'blocked' } }))
    })
  }, [gateways.workspace, locationKey, state.landingResolved, state.session.state])

  useEffect(() => {
    if (state.session.state === 'invalid-credentials') document.getElementById('sign-in-username')?.focus()
  }, [state.session.state])

  useEffect(() => {
    if (state.session.state !== 'ready' || !state.landingResolved) return
    const handle = window.requestAnimationFrame(() => {
      const heading = document.querySelector<HTMLElement>('[data-route-title], main h1')
      if (heading) { heading.tabIndex = -1; heading.focus({ preventScroll: true }) }
      else document.getElementById('main-content')?.focus({ preventScroll: true })
    })
    return () => window.cancelAnimationFrame(handle)
  }, [locationKey, state.landingResolved, state.route, state.session.state])

  async function signIn() {
    if (!username.trim() || !password) {
      setState(current => transitionAppShell(current, { type: 'signed-in', session: { state: 'invalid-credentials' } }))
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

  const authenticated = state.session.state === 'ready'
  const scope = authenticated ? state.session.scopeLabel ?? 'Phạm vi được cấp' : state.session.state === 'loading' ? 'Đang tải phạm vi' : 'Chưa có phạm vi'
  const activeRoute = state.route
  const forbiddenNextRoute = state.landingPresentation?.kind === 'safe-forbidden' ? state.landingPresentation.nextRoute : undefined
  const closeDrawer = () => setState(current => transitionAppShell(current, { type: 'nav-mode', mode: 'rail' }))
  const openDrawer = () => setState(current => transitionAppShell(current, { type: 'nav-mode', mode: 'drawer-open' }))

  return <div className="app-shell">
    <a className="skip-link" href="#main-content">Bỏ qua điều hướng, đến nội dung chính</a>
    <header className="topbar">
      <a className="brand" href="/" onClick={event => {
        event.preventDefault()
        const home = firstPermittedNavigationRoute(effectiveAccess)
        if (home) requestNavigation(home)
      }}><span className="brand-mark" aria-hidden="true">I</span><span>IDEA Utility Monitoring</span></a>
      <div className="session-controls"><span className="scope-pill" aria-label="Phạm vi hiện tại">{scope}</span>
        {authenticated ? <><span className="session-user" aria-label="Người dùng đã đăng nhập">{state.session.username ?? 'Người dùng'}</span><button className="button button-quiet" type="button" onClick={() => void signOut()}>Đăng xuất</button></> : <div className="sign-in-form" aria-label="Biểu mẫu đăng nhập">
          <label className="sign-in-label" htmlFor="sign-in-username">Tên đăng nhập</label><input id="sign-in-username" className="text-input sign-in-input" type="text" autoComplete="username" value={username} onChange={event => setUsername(event.target.value)} aria-describedby={state.session.state === 'invalid-credentials' ? 'sign-in-error' : undefined} />
          <label className="sign-in-label" htmlFor="sign-in-password">Mật khẩu</label><input id="sign-in-password" className="text-input sign-in-input" type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} aria-describedby={state.session.state === 'invalid-credentials' ? 'sign-in-error' : undefined} />
          <button className="button button-primary" type="button" onClick={() => void signIn()} disabled={state.session.state === 'loading' || state.submitting}>{state.submitting ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
        </div>}
      </div>
    </header>
    {authenticated && <ContextBar session={state.session} />}
    <div className="layout">
      {state.navMode === 'expanded' ? <Sidebar items={visibleItems} activeRoute={activeRoute} onNavigate={requestNavigation} onCollapse={() => setState(current => transitionAppShell(current, { type: 'nav-mode', mode: 'rail' }))} /> : <Rail items={visibleItems} activeRoute={activeRoute} onNavigate={requestNavigation} onExpand={openDrawer} />}
      <NavigationDrawer open={state.navMode === 'drawer-open'} items={visibleItems} activeRoute={activeRoute} onNavigate={requestNavigation} onClose={closeDrawer} />
      <main className="content" id="main-content" tabIndex={-1}>
        <div className="feedback" role="status" aria-live="polite">{state.feedback}</div>
        {!authenticated && <section className="notice notice-info" aria-label="Thông báo xác thực"><strong>{state.session.state === 'loading' ? 'Đang tải phiên làm việc.' : state.session.state === 'submitting' ? 'Đang đăng nhập.' : state.session.state === 'error' ? 'Không thể kết nối máy chủ.' : 'Đăng nhập để quản lý không gian làm việc.'}</strong><span>Mọi truy vấn và thay đổi đều được kiểm tra phạm vi tại máy chủ.</span></section>}
        {state.session.state === 'invalid-credentials' && <section id="sign-in-error" className="notice notice-warning" role="alert">Tên đăng nhập hoặc mật khẩu không đúng.</section>}
        {state.session.state === 'forbidden' && <section className="notice notice-warning" role="alert">Phiên không được phép. Hãy đăng nhập lại.</section>}
        {state.session.state === 'expired' && <section className="notice notice-warning" role="alert">Phiên đã hết hạn. Hãy đăng nhập lại.</section>}
        {state.session.state === 'error' && <section className="notice notice-warning" role="alert">Lỗi phiên. Kiểm tra kết nối rồi đăng nhập lại.</section>}
        {authenticated && state.landingResolved && state.landingPresentation?.kind === 'safe-forbidden' && <ForbiddenState message="Đường dẫn không còn được phép trong phạm vi hiện tại." action={forbiddenNextRoute ? <button className="button button-secondary" type="button" onClick={() => requestNavigation(forbiddenNextRoute)}>Tiếp tục trong phạm vi được cấp</button> : undefined} />}
        {authenticated && state.landingResolved && state.landingPresentation?.kind === 'safe-no-authorized-capability' && <ForbiddenState title="Chưa có phạm vi được cấp" message="Không có capability nào trong phiên này được phép hiển thị." />}
        {authenticated && state.landingResolved && state.landingPresentation?.kind === 'blocked' && <RetryState message="Không thể xác nhận trạng thái không gian làm việc." onRetry={() => setState(current => transitionAppShell(current, { type: 'retry-workspace-status' }))} />}
        {authenticated && (!state.landingPresentation || state.landingPresentation.kind === 'route') ? children(state.route, requestNavigation, state.session, locationKey) : null}
      </main>
    </div>
    {pendingNavigation && <ConfirmDialog
      open
      title="Bạn có thay đổi chưa lưu"
      description={pendingNavigation.message}
      confirmLabel="Rời trang"
      cancelLabel="Ở lại"
      onConfirm={() => {
        const pending = pendingNavigation
        setPendingNavigation(null)
        if (pending) performNavigation(pending.route, pending.request, pending.fromHistory)
      }}
      onCancel={() => {
        const pending = pendingNavigation
        setPendingNavigation(null)
        if (pending?.fromHistory && pending.previousHref) {
          window.history.pushState({}, '', pending.previousHref)
          setLocationKey(window.location.href)
        }
      }}
    />}
  </div>
}
