import { useEffect, useState, type ReactNode } from 'react'
import { useWebGateways } from '../gateways/GatewayContext'
import type { AuthSession } from '../gateways/webGateways'

export type WebRoute = 'configuration' | 'simulator' | 'telemetry' | 'audit'

export type AppShellProps = {
  children: (route: WebRoute) => ReactNode
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
      ? 'Signed in. Your authorized scope is ready.'
      : event.session.state === 'invalid-credentials'
        ? 'Invalid username or password.'
        : 'Sign-in could not be completed.',
  }
  if (event.type === 'signed-out') return {
    ...state,
    session: { state: 'expired' },
    submitting: false,
    feedback: 'Signed out.',
  }
  return { ...state, route: event.route }
}

export function AppShell({ children }: AppShellProps) {
  const gateways = useWebGateways()
  const [state, setState] = useState(initialAppShellState)
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')

  useEffect(() => {
    void gateways.auth.getSession()
      .then(session => setState(current => transitionAppShell(current, { type: 'session', session })))
      .catch(() => setState(current => transitionAppShell(current, { type: 'session', session: { state: 'error' } })))
  }, [gateways.auth])

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

  function navigate(route: WebRoute) {
    setState(current => transitionAppShell(current, { type: 'navigate', route }))
  }

  const authenticated = state.session.state === 'ready'
  const scope = authenticated
    ? state.session.scopeLabel ?? 'Authorized scope'
    : state.session.state === 'loading' ? 'Loading scope' : 'No authorized scope'

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#configuration" onClick={() => navigate('configuration')}>
          <span className="brand-mark" aria-hidden="true">I</span>
          <span>IDEA Utility Monitoring</span>
        </a>
        <div className="session-controls">
          <span className="scope-pill" aria-label="Current scope">{scope}</span>
          {authenticated ? <>
            <span className="session-user" aria-label="Signed-in user">
              {state.session.username ?? 'User'} · {state.session.scopeLabel ?? 'scope'}
              {state.session.isAdministrator ? ' · Admin' : ''}
            </span>
            <button className="button button-quiet" type="button" onClick={() => void signOut()}>Sign out</button>
          </> : <div className="sign-in-form">
            <input className="text-input sign-in-input" type="text" placeholder="Username" value={username}
              onChange={event => setUsername(event.target.value)} aria-label="Username" />
            <input className="text-input sign-in-input" type="password" placeholder="Password" value={password}
              onChange={event => setPassword(event.target.value)} aria-label="Password" />
            <button className="button button-primary" type="button" onClick={() => void signIn()}
              disabled={state.session.state === 'loading' || state.submitting}>
              {state.submitting ? 'Signing in…' : 'Sign in'}
            </button>
          </div>}
        </div>
      </header>
      <div className="layout">
        <nav className="sidebar" aria-label="Primary navigation">
          <p className="nav-heading">Workspace</p>
          {(['configuration', 'simulator', 'telemetry', 'audit'] as WebRoute[]).map(item =>
            <button className={`nav-link${state.route === item ? ' active' : ''}`} type="button" key={item}
              aria-current={state.route === item ? 'page' : undefined} onClick={() => navigate(item)}>
              {item === 'configuration' ? 'Configuration' : item === 'simulator' ? 'Simulator' : item === 'telemetry' ? 'Latest & health' : 'Audit review'}
            </button>)}
          <div className="sidebar-note"><span className="status-dot" aria-hidden="true" /><span>Provider-neutral mode</span></div>
        </nav>
        <main className="content" id="main-content">
          <div className="feedback" role="status" aria-live="polite">{state.feedback}</div>
          {!authenticated && <section className="notice notice-info" aria-label="Authentication notice">
            <strong>{state.session.state === 'loading' ? 'Loading your session.'
              : state.session.state === 'submitting' ? 'Signing in.'
                : state.session.state === 'error' ? 'Could not reach the server.'
                  : 'Sign in to manage this workspace.'}</strong>
            <span>Queries and mutations are scope-checked server-side.</span>
          </section>}
          {state.session.state === 'invalid-credentials' && <section className="notice notice-warning" role="alert">Invalid username or password.</section>}
          {state.session.state === 'forbidden' && <section className="notice notice-warning" role="alert">Your session is forbidden. Sign in again to continue.</section>}
          {state.session.state === 'expired' && <section className="notice notice-warning" role="alert">Your session is expired. Sign in again to continue.</section>}
          {state.session.state === 'error' && <section className="notice notice-warning" role="alert">Session error. Check your connection and sign in again.</section>}
          {children(state.route)}
        </main>
      </div>
    </div>
  )
}
